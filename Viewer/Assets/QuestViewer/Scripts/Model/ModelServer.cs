using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// HTTP endpoint for the "Send to Quest" extension (port 8723). Every request must carry the pairing code
/// (header X-Pair-Code) shown on the keypad display; anything else gets 401.
///   GET  /status  -> {"ok":true,"model":"…","key":"…"}
///   POST /notify  {"state":"downloading|error|unchanged","message":"…"}  -> shows it in the headset
///   POST /model   binary glTF body, headers X-Model-Name / X-Model-Key / X-Hidden-Parts / X-Part-Info
///                 -> loads it, replies when shown
///   GET  /commands  long poll (up to 20 s) -> {"commands":["send"]}; how the viewer asks the extension to send
///                   (voice command). The extension keeps one open while an Onshape tab is open in Chrome.
/// On PC it listens on localhost only; on Quest on the local network, so Chrome on the PC can reach it.
public class ModelServer : MonoBehaviour
{
    public const int Port = 8723;
    public ModelHost host;

    /// Raised on the main thread when a download starts / something fails.
    public static event Action<string> DownloadStarted;
    public static event Action<string> Failed;

    MiniHttpServer server;
    readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();
    volatile string modelName;
    volatile string modelKey;
    volatile bool loading;
    string address;

    // Commands for the extension, picked up by GET /commands.
    const double PollSeconds = 20, CommandMaxAgeSeconds = 10;
    readonly object commandLock = new object();
    readonly System.Collections.Generic.List<(string name, DateTime at)> commands = new System.Collections.Generic.List<(string, DateTime)>();
    TaskCompletionSource<bool> commandSignal = NewSignal();
    int openPolls;
    long lastPollTicks;

    static TaskCompletionSource<bool> NewSignal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    /// True while the extension is waiting for commands (an Onshape tab is open in Chrome).
    public bool ExtensionListening =>
        Volatile.Read(ref openPolls) > 0
        || (DateTime.UtcNow - new DateTime(Interlocked.Read(ref lastPollTicks), DateTimeKind.Utc)).TotalSeconds < 5;

    /// Asks the extension to send the Onshape tab you used last. False if no extension is listening.
    public bool RequestSend()
    {
        if (!ExtensionListening) return false;
        lock (commandLock)
        {
            commands.Add(("send", DateTime.UtcNow));
            var s = commandSignal;
            commandSignal = NewSignal();
            s.TrySetResult(true);
        }
        return true;
    }

    [Serializable] class Notice { public string state; public string message; }

    void OnEnable()
    {
        bool lan = Application.platform == RuntimePlatform.Android;
        address = (lan ? LocalIPv4() ?? "?" : "127.0.0.1") + ":" + Port;
        _ = PairingCode.Value; // create/load on the main thread before requests arrive
        try
        {
            server = new MiniHttpServer(lan ? IPAddress.Any : IPAddress.Loopback, Port, Handle);
            server.Start();
            Debug.Log("[ModelServer] Listening on " + address);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ModelServer] Port {Port} unavailable: {e.Message}");
            server = null;
        }
    }

    void Start()
    {
        if (server == null) Status.Error($"Port {Port} in use");
        else Status.Info(PairingInfo, 60f);
    }

    /// What to enter in the extension's options.
    public string PairingInfo => $"{address}\npair code {PairingCode.Value}";

    void OnDisable()
    {
        server?.Dispose();
        server = null;
    }

    void Update()
    {
        while (mainThread.TryDequeue(out var a)) a();
    }

    Task<T> OnMainThread<T>(Func<Task<T>> work)
    {
        var tcs = new TaskCompletionSource<T>();
        mainThread.Enqueue(async () =>
        {
            try { tcs.SetResult(await work()); }
            catch (Exception e) { tcs.SetException(e); }
        });
        return tcs.Task;
    }

    async Task<MiniHttpServer.Response> Handle(MiniHttpServer.Request req)
    {
        if (!PairingCode.Matches(req.Header("X-Pair-Code")))
            return MiniHttpServer.Response.Json(401, "{\"ok\":false,\"pairing\":true,\"message\":\"Wrong or missing pairing code.\"}");

        if (req.Method == "GET" && req.Path == "/status")
        {
            return MiniHttpServer.Response.Json(200,
                $"{{\"ok\":true,\"model\":{Json(modelName)},\"key\":{Json(modelKey)},\"loading\":{(loading ? "true" : "false")}}}");
        }

        if (req.Method == "GET" && req.Path == "/commands")
            return MiniHttpServer.Response.Json(200, "{\"commands\":[" + string.Join(",", await WaitForCommands()) + "]}");

        if (req.Method == "POST" && req.Path == "/notify")
        {
            var n = JsonUtility.FromJson<Notice>(req.BodyText) ?? new Notice();
            mainThread.Enqueue(() =>
            {
                switch (n.state)
                {
                    case "downloading":
                        Status.Busy($"Receiving\n{n.message}");
                        DownloadStarted?.Invoke(n.message);
                        break;
                    case "unchanged":
                        Status.Ok($"Up to date\n{n.message}");
                        break;
                    case "error":
                        Status.Error(n.message);
                        Failed?.Invoke(n.message);
                        break;
                }
            });
            return MiniHttpServer.Response.Json(200, "{\"ok\":true}");
        }

        if (req.Method == "POST" && req.Path == "/model")
        {
            if (loading) return MiniHttpServer.Response.Json(409, "{\"ok\":false,\"message\":\"Already loading a model.\"}");
            loading = true;
            try
            {
                string name = Unescape(req.Header("X-Model-Name")) ?? "Onshape part";
                string key = Unescape(req.Header("X-Model-Key"));
                int.TryParse(req.Header("X-Hidden-Parts"), out int hidden);
                string partInfo = DecodeBase64(req.Header("X-Part-Info"));

                var summary = await OnMainThread(async () =>
                {
                    Status.Busy($"Loading\n{name}");
                    try { return await host.LoadGlbAsync(req.Body, name, partInfo); }
                    catch (Exception e)
                    {
                        Status.Error("Load failed\n" + e.Message);
                        Failed?.Invoke(e.Message);
                        throw;
                    }
                });

                modelName = name;
                modelKey = key;
                string note = hidden > 0 ? $", {hidden} hidden part{(hidden == 1 ? "" : "s")} skipped" : "";
                return MiniHttpServer.Response.Json(200, $"{{\"ok\":true,\"message\":{Json(summary + note)}}}");
            }
            catch (Exception e)
            {
                return MiniHttpServer.Response.Json(400, $"{{\"ok\":false,\"message\":{Json(e.Message)}}}");
            }
            finally
            {
                loading = false;
            }
        }

        return MiniHttpServer.Response.Json(404, "{\"ok\":false,\"message\":\"Use GET /status, GET /commands, POST /notify or POST /model.\"}");
    }

    /// Waits until there are commands or the poll time is up; returns them as JSON strings.
    async Task<string[]> WaitForCommands()
    {
        Interlocked.Increment(ref openPolls);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(PollSeconds);
            while (true)
            {
                Task signal;
                lock (commandLock)
                {
                    commands.RemoveAll(c => (DateTime.UtcNow - c.at).TotalSeconds > CommandMaxAgeSeconds);
                    if (commands.Count > 0)
                    {
                        var names = commands.ConvertAll(c => Json(c.name)).ToArray();
                        commands.Clear();
                        return names;
                    }
                    signal = commandSignal.Task;
                }
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero) return Array.Empty<string>();
                await Task.WhenAny(signal, Task.Delay(left));
            }
        }
        finally
        {
            Interlocked.Exchange(ref lastPollTicks, DateTime.UtcNow.Ticks);
            Interlocked.Decrement(ref openPolls);
        }
    }

    static string Unescape(string s) => string.IsNullOrEmpty(s) ? null : Uri.UnescapeDataString(s);

    static string DecodeBase64(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        catch (FormatException) { return null; }
    }

    static string Json(string s) =>
        s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";

    /// The address other devices on the network use to reach this one (no packets are sent).
    static string LocalIPv4()
    {
        try
        {
            using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                s.Connect("10.255.255.255", 1);
                return ((IPEndPoint)s.LocalEndPoint).Address.ToString();
            }
        }
        catch (Exception) { return null; }
    }
}
