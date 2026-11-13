using System;
using System.Collections.Concurrent;
using UnityEngine;

/// Offline voice commands for Marvin:
///   "Marvin, send this to the Quest" / "Marvin, could you send the model"
///        recentres the model at 100% and asks the Chrome extension to send the Onshape tab you used last
///   "Marvin, hologram / style / light / explode / rotate / cut / fixed / inspect / reset"
///        press that keypad button
///   "Thanks Marvin"  Marvin answers with droid beeps
/// Standalone Quest: Vosk with a small English model (StreamingAssets/vosk-model), limited to these phrases.
/// Editor / PC: Windows' built-in speech recognition. Audio never leaves the device.
public class VoiceCommands : MonoBehaviour
{
    public ModelServer server;
    public Keypad keypad;
    public ModelHost host;
    [Tooltip("Ignore the same command again within this many seconds.")]
    public float cooldown = 2f;
    [Tooltip("After \"send\", wait this long before recentring a model that's already there.")]
    public float recenterDelay = 2f;

    const string Send = "send", Thanks = "thanks";

    /// Everything the recognizers listen for, and what it means: "send", "thanks", or a keypad button label.
    static readonly (string phrase, string command)[] Phrases =
    {
        ("marvin send this to the quest", Send),
        ("marvin send this to quest", Send),
        ("marvin send it to the quest", Send),
        ("marvin send to the quest", Send),
        ("marvin send to quest", Send),
        ("marvin could you send the model", Send),
        ("marvin send the model", Send),
        ("marvin hologram", "HOLO"),
        ("marvin style", "STYLE"),
        ("marvin light", "LIGHT"),
        ("marvin lighting", "LIGHT"),
        ("marvin explode", "EXPLODE"),
        ("marvin rotate", "ROTATE"),
        ("marvin cut", "CUT"),
        ("marvin section cut", "CUT"),
        ("marvin fixed", "FIXED"),
        ("marvin fixed scale", "FIXED"),
        ("marvin fix scale", "FIXED"),
        ("marvin inspect", "INSPECT"),
        ("marvin reset", "RESET"),
        ("marvin recenter", "RESET"),
        ("thanks marvin", Thanks),
        ("thank you marvin", Thanks),
        ("marvin thanks", Thanks),
        ("marvin thank you", Thanks),
    };

    readonly ConcurrentQueue<string> heard = new ConcurrentQueue<string>();
    readonly ConcurrentQueue<string> problems = new ConcurrentQueue<string>();
    string lastCommand;
    float lastTime = -10f;
    IDisposable recognizer;

    void Start()
    {
        var phrases = Array.ConvertAll(Phrases, p => p.phrase);
#if UNITY_ANDROID && !UNITY_EDITOR
        recognizer = new VoskRecognizer(phrases, heard.Enqueue, problems.Enqueue);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        recognizer = WindowsRecognizer.Create(phrases, heard.Enqueue, problems.Enqueue);
#else
        Debug.Log("[Voice] No speech recognizer on this platform.");
#endif
    }

    void OnDestroy()
    {
        recognizer?.Dispose();
        recognizer = null;
    }

    void OnApplicationPause(bool paused)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        (recognizer as VoskRecognizer)?.Pause(paused);
#endif
    }

    void Update()
    {
        while (problems.TryDequeue(out var p))
        {
            Debug.LogWarning("[Voice] " + p);
            Status.Error("Voice commands off\n" + p);
        }
        while (heard.TryDequeue(out var text)) Handle(text);
    }

    void Handle(string text)
    {
        string command = null;
        foreach (var p in Phrases)
            if (p.phrase == text) { command = p.command; break; }
        if (command == null) return;
        if (command == lastCommand && Time.unscaledTime - lastTime < cooldown) return;
        lastCommand = command;
        lastTime = Time.unscaledTime;
        Debug.Log($"[Voice] Heard \"{text}\" -> {command}");

        // Marvin answers every command with his beeps.
        switch (command)
        {
            case Send:
                if (server != null && server.RequestSend())
                {
                    Say(DroidVoice.Answer(DroidVoice.Reaction.OnIt));
                    Status.Busy("Asking Chrome to send…");
                    // A model already in view: give it a moment, then bring it back in front of you at 100%.
                    // (The very first model appears in front of you by itself.)
                    if (host != null && host.HasModel)
                    {
                        StopCoroutine(nameof(RecenterLater));
                        StartCoroutine(nameof(RecenterLater));
                    }
                }
                else
                {
                    Say(DroidVoice.Answer(DroidVoice.Reaction.Sad));
                    Status.Error("Chrome isn't connected\nOpen Onshape in Chrome with the extension");
                }
                break;
            case Thanks:
                var (clip, mood) = DroidVoice.Reply();
                Say(clip);
                Status.Ok(mood, 2.5f);
                break;
            default:
                if (keypad == null || !keypad.Press(command))
                {
                    Debug.LogWarning("[Voice] No keypad button " + command);
                    Say(DroidVoice.Answer(DroidVoice.Reaction.Sad));
                    break;
                }
                var reaction = command == "STYLE" || command == "LIGHT" || command == "RESET" ? DroidVoice.Reaction.Okay
                    : keypad.IsOn(command) ? DroidVoice.Reaction.On : DroidVoice.Reaction.Off;
                Say(DroidVoice.Answer(reaction));
                break;
        }
    }

    System.Collections.IEnumerator RecenterLater()
    {
        yield return new WaitForSeconds(recenterDelay);
        if (keypad != null) keypad.Recenter();
    }

    void Say(AudioClip clip)
    {
        if (keypad != null) keypad.Say(clip);
        Destroy(clip, clip.length + 1f);
    }
}

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
/// Windows speech recognition, limited to the phrases. Uses the default microphone (over Link that can be
/// the headset's, if Windows has it as the default recording device).
sealed class WindowsRecognizer : IDisposable
{
    UnityEngine.Windows.Speech.KeywordRecognizer keywords;

    public static WindowsRecognizer Create(string[] phrases, Action<string> onText, Action<string> onProblem)
    {
        try
        {
            var r = new WindowsRecognizer { keywords = new UnityEngine.Windows.Speech.KeywordRecognizer(phrases) };
            r.keywords.OnPhraseRecognized += e => onText(e.text);
            r.keywords.Start();
            Debug.Log("[Voice] Windows speech recognition listening.");
            return r;
        }
        catch (Exception e)
        {
            onProblem("Windows speech recognition unavailable: " + e.Message);
            return null;
        }
    }

    public void Dispose()
    {
        if (keywords == null) return;
        if (keywords.IsRunning) keywords.Stop();
        keywords.Dispose();
        keywords = null;
    }
}
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
/// Vosk (org.vosk, Apache 2.0) through its Android library. The model is copied from the APK's assets
/// to app storage on first start (StorageService.unpack), then a SpeechService listens on the microphone.
sealed class VoskRecognizer : IDisposable
{
    const float SampleRate = 16000f;
    readonly string grammar;
    readonly Action<string> onText;
    readonly Action<string> onProblem;
    AndroidJavaObject service;
    bool disposed;

    public VoskRecognizer(string[] phrases, Action<string> onText, Action<string> onProblem)
    {
        this.onText = onText;
        this.onProblem = onProblem;
        var quoted = Array.ConvertAll(phrases, p => "\"" + p + "\"");
        grammar = "[" + string.Join(",", quoted) + ",\"[unk]\"]";

        const string mic = UnityEngine.Android.Permission.Microphone;
        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(mic)) Unpack();
        else
        {
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => Unpack();
            callbacks.PermissionDenied += _ => onProblem("Microphone permission denied");
            UnityEngine.Android.Permission.RequestUserPermission(mic, callbacks);
        }
    }

    void Unpack()
    {
        if (disposed) return;
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var storage = new AndroidJavaClass("org.vosk.android.StorageService"))
            {
                var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                storage.CallStatic("unpack", activity, "vosk-model", "vosk",
                    new Callback(Listen), new Callback(e => onProblem("Model: " + Message(e))));
            }
        }
        catch (Exception e) { onProblem(e.Message); }
    }

    void Listen(AndroidJavaObject model)
    {
        if (disposed) return;
        try
        {
            var recognizer = new AndroidJavaObject("org.vosk.Recognizer", model, SampleRate, grammar);
            service = new AndroidJavaObject("org.vosk.android.SpeechService", recognizer, SampleRate);
            service.Call<bool>("startListening", new Listener(this));
            Debug.Log("[Voice] Vosk listening.");
        }
        catch (Exception e) { onProblem(e.Message); }
    }

    public void Pause(bool paused)
    {
        try { service?.Call("setPause", paused); } catch (Exception) { }
    }

    public void Dispose()
    {
        disposed = true;
        try
        {
            service?.Call<bool>("stop");
            service?.Call("shutdown");
        }
        catch (Exception) { }
        service = null;
    }

    static string Message(AndroidJavaObject e)
    {
        try { return e?.Call<string>("getMessage") ?? "unknown error"; } catch (Exception) { return "unknown error"; }
    }

    /// Vosk results are JSON like {"text": "marvin explode"} or {"partial": "marvin"}.
    void Result(string json, string key)
    {
        if (string.IsNullOrEmpty(json)) return;
        string marker = "\"" + key + "\"";
        int k = json.IndexOf(marker, StringComparison.Ordinal);
        if (k < 0) return;
        int start = json.IndexOf('"', json.IndexOf(':', k + marker.Length) + 1);
        int end = start < 0 ? -1 : json.IndexOf('"', start + 1);
        if (end <= start) return;
        var text = json.Substring(start + 1, end - start - 1).Trim();
        if (text.Length > 0) onText(text);
    }

    sealed class Callback : AndroidJavaProxy
    {
        readonly Action<AndroidJavaObject> action;
        public Callback(Action<AndroidJavaObject> action) : base("org.vosk.android.StorageService$Callback") { this.action = action; }
        void onComplete(AndroidJavaObject result) => action(result);
    }

    sealed class Listener : AndroidJavaProxy
    {
        readonly VoskRecognizer owner;
        public Listener(VoskRecognizer owner) : base("org.vosk.android.RecognitionListener") { this.owner = owner; }
        // Partial results fire as soon as the whole phrase is recognized; the final one repeats it (cooldown).
        void onPartialResult(string hypothesis) => owner.Result(hypothesis, "partial");
        void onResult(string hypothesis) => owner.Result(hypothesis, "text");
        void onFinalResult(string hypothesis) => owner.Result(hypothesis, "text");
        void onError(AndroidJavaObject exception) => owner.onProblem(Message(exception));
        void onTimeout() { }
    }
}
#endif
