using System;
using UnityEngine;

/// A placeable keypad (pinch to move it) with a small display and nine buttons, pressed with a fingertip or
/// controller tip:
///   HOLO     hologram look           STYLE    solid / clay / X-ray     LIGHT   shaded / flat
///   EXPLODE  exploded view           ROTATE   turntable                CUT     section cut
///   MEASURE  two-hand tape measure   INSPECT  touch parts for info     RESET   back in front of you at 100%
/// The display shows the scale and short status messages.
[RequireComponent(typeof(Grabbable))]
public class Keypad : MonoBehaviour
{
    public ModelHost host;
    public Grabbable model;
    public Grabbable placeholder;

    internal static readonly Color Accent = new Color(0.35f, 0.9f, 1f);
    static readonly Color Green = new Color(0.45f, 1f, 0.55f);
    static readonly Color Red = new Color(1f, 0.45f, 0.4f);

    const float Pitch = 0.043f;
    const float DisplayY = 0.05f;

    KeypadButton holo, style, lighting, explode, turntable, cut, measure, inspect, reset;
    TextMesh scaleText, infoText;
    ModelServer server;
    AudioSource audioSource;
    AudioClip click;

    void Awake()
    {
        var baseMat = NewLit(new Color(0.07f, 0.08f, 0.1f), 0.6f);
        var capMat = NewLit(new Color(0.2f, 0.22f, 0.26f), 0.5f);

        // Plate covering the display and the 3x3 grid.
        float top = DisplayY + 0.024f, bottom = -2 * Pitch - 0.024f;
        var plateSize = new Vector3(3 * Pitch + 0.012f, top - bottom, 0.012f);
        var plateCenter = new Vector3(0f, (top + bottom) * 0.5f, 0f);
        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(plate.GetComponent<Collider>());
        plate.name = "Plate";
        plate.transform.SetParent(transform, false);
        plate.transform.localPosition = plateCenter;
        plate.transform.localScale = plateSize;
        plate.GetComponent<Renderer>().sharedMaterial = baseMat;
        var grab = gameObject.AddComponent<BoxCollider>();
        grab.center = plateCenter;
        grab.size = plateSize;

        // Display: a dark screen with the scale and a status line.
        var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(screen.GetComponent<Collider>());
        screen.name = "Screen";
        screen.transform.SetParent(transform, false);
        screen.transform.localPosition = new Vector3(0f, DisplayY, 0.0062f);
        screen.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        screen.transform.localScale = new Vector3(3 * Pitch - 0.004f, 0.04f, 1f);
        screen.GetComponent<Renderer>().sharedMaterial = NewUnlit(new Color(0.01f, 0.03f, 0.04f));
        scaleText = KeypadButton.MakeText(transform, "100%", new Vector3(0f, DisplayY + 0.007f, 0.0066f), 0.0022f, Accent);
        scaleText.fontStyle = FontStyle.Bold;
        infoText = KeypadButton.MakeText(transform, "", new Vector3(0f, DisplayY - 0.011f, 0.0066f), 0.00075f, Color.white);

        holo = Button(0, 0, "HOLO", ViewModes.ToggleHologram, capMat);
        style = Button(1, 0, "STYLE", ViewModes.CycleStyle, capMat);
        lighting = Button(2, 0, "LIGHT", ViewModes.ToggleLighting, capMat);
        explode = Button(0, 1, "EXPLODE", ViewModes.ToggleExplode, capMat);
        turntable = Button(1, 1, "ROTATE", ViewModes.ToggleTurntable, capMat);
        cut = Button(2, 1, "CUT", ViewModes.ToggleCut, capMat);
        measure = Button(0, 2, "MEASURE", ViewModes.ToggleMeasure, capMat);
        inspect = Button(1, 2, "INSPECT", ViewModes.ToggleInspect, capMat);
        reset = Button(2, 2, "RESET", ResetObject, capMat);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 0.3f;
        audioSource.playOnAwake = false;
        click = MakeClick();
    }

    KeypadButton Button(int col, int row, string label, Action action, Material capMat) =>
        KeypadButton.Create(transform, new Vector3((col - 1) * Pitch, -row * Pitch, 0f), label, capMat, action, this);

    void OnEnable() { ViewModes.Changed += Refresh; Refresh(); }
    void OnDisable() => ViewModes.Changed -= Refresh;

    void Refresh()
    {
        if (holo == null) return;
        holo.SetState(ViewModes.Hologram ? "ON" : "OFF", ViewModes.Hologram);
        style.SetState(ViewModes.Style == LookStyle.XRay ? "X-RAY" : ViewModes.Style.ToString().ToUpperInvariant(), ViewModes.Style != LookStyle.Solid);
        lighting.SetState(ViewModes.Lighting.ToString().ToUpperInvariant(), ViewModes.Lighting == LightingMode.Flat);
        explode.SetState(ViewModes.Explode ? "ON" : "OFF", ViewModes.Explode);
        turntable.SetState(ViewModes.Turntable ? "ON" : "OFF", ViewModes.Turntable);
        cut.SetState(ViewModes.Cut ? "ON" : "OFF", ViewModes.Cut);
        measure.SetState(ViewModes.Measure ? "ON" : "OFF", ViewModes.Measure);
        inspect.SetState(ViewModes.Inspect ? "ON" : "OFF", ViewModes.Inspect);
        reset.SetState("", false);
    }

    Grabbable Target => host != null && host.HasModel ? model : placeholder;

    void ResetObject()
    {
        var t = Target;
        if (t == null) return;
        t.ReleaseAll();
        t.ResetScale();
        var spawn = t.GetComponent<SpawnInFront>();
        if (spawn != null) spawn.Respawn();
    }

    void LateUpdate()
    {
        var t = Target;
        float scale = t != null ? t.Scale : 1f;
        bool trueSize = t == null || t.IsTrueSize;
        scaleText.text = $"{scale * 100f:0}%";
        scaleText.color = trueSize ? Green : Accent;

        string info;
        var color = new Color(1f, 1f, 1f, 0.8f);
        if (Status.Visible)
        {
            info = Status.Message;
            if (Status.Type == Status.Kind.Error) color = Red;
            else if (Status.Type == Status.Kind.Ok) color = Green;
            if (Status.Type == Status.Kind.Busy) info += new string('.', 1 + (int)(Time.unscaledTime * 3f) % 3);
        }
        else if (ViewModes.Inspect) info = "INSPECT\ntouch a part";
        else if (ViewModes.Measure) info = "MEASURE\npinch with both hands";
        else if (ViewModes.Cut) info = "CUT\npinch the frame to move it";
        else
        {
            if (server == null) server = FindAnyObjectByType<ModelServer>();
            string pairing = server != null ? server.PairingInfo.Replace("\n", "  ") : "";
            info = host != null && host.HasModel ? host.CurrentName + "\n" + pairing : server != null ? server.PairingInfo : "";
        }
        infoText.text = Fit(info, 28);
        infoText.color = color;
    }

    /// Keeps the display tidy: at most two lines of 'width' characters.
    static string Fit(string s, int width)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var lines = s.Split('\n');
        string a = Trim(lines[0], width);
        string b = lines.Length > 1 ? Trim(lines[1], width) : "";
        return b.Length > 0 ? a + "\n" + b : a;
    }

    static string Trim(string s, int width) => s.Length <= width ? s : s.Substring(0, width - 1) + "…";

    internal void PlayClick(float pitch)
    {
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(click, 0.6f);
    }

    static Material NewLit(Color c, float smoothness)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = c };
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    internal static Material NewUnlit(Color c) => new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = c };

    /// A short synthetic "blip": two quick tones with a fast decay.
    static AudioClip MakeClick()
    {
        const int rate = 44100;
        int n = rate / 16;
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t * 55f);
            float f = t < 0.018f ? 1900f : 2600f;
            data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.5f;
        }
        var clip = AudioClip.Create("KeypadClick", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

/// One keypad button: pressed by anything with a Poker collider.
public class KeypadButton : MonoBehaviour
{
    const float Size = 0.037f;
    const float Travel = 0.006f;
    const float Cooldown = 0.35f;

    Action action;
    Keypad pad;
    Transform cap;
    TextMesh stateText;
    Material ledMat;
    float pressedAt = -10f;
    bool on;

    public static KeypadButton Create(Transform parent, Vector3 pos, string label, Material capMat, Action action, Keypad pad)
    {
        var go = new GameObject("Button_" + label);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var b = go.AddComponent<KeypadButton>();
        b.action = action;
        b.pad = pad;

        var trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(Size, Size, 0.03f);
        trigger.center = new Vector3(0f, 0f, 0.016f);

        var capGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(capGo.GetComponent<Collider>());
        capGo.name = "Cap";
        b.cap = capGo.transform;
        b.cap.SetParent(go.transform, false);
        b.cap.localScale = new Vector3(Size, Size, 0.012f);
        b.cap.localPosition = new Vector3(0f, 0f, 0.012f);
        capGo.GetComponent<Renderer>().sharedMaterial = capMat;

        var face = new GameObject("Face").transform;
        face.SetParent(go.transform, false);
        b.faceRoot = face;
        var title = MakeText(face, label, new Vector3(0f, 0.002f, 0.0185f), 0.0008f, Color.white);
        title.fontStyle = FontStyle.Bold;
        b.stateText = MakeText(face, "", new Vector3(0f, -0.008f, 0.0185f), 0.0006f, new Color(1f, 1f, 1f, 0.65f));

        var ledGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(ledGo.GetComponent<Collider>());
        ledGo.name = "Led";
        ledGo.transform.SetParent(face, false);
        ledGo.transform.localPosition = new Vector3(0f, 0.0125f, 0.0185f);
        ledGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        ledGo.transform.localScale = new Vector3(0.018f, 0.0026f, 1f);
        b.ledMat = Keypad.NewUnlit(Color.black);
        ledGo.GetComponent<Renderer>().sharedMaterial = b.ledMat;
        return b;
    }

    Transform faceRoot;

    internal static TextMesh MakeText(Transform parent, string text, Vector3 localPos, float size, Color color)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // face the user (local +Z)
        var t = go.AddComponent<TextMesh>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        go.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
        t.text = text;
        t.anchor = TextAnchor.MiddleCenter;
        t.alignment = TextAlignment.Center;
        t.fontSize = 64;
        t.characterSize = size;
        t.color = color;
        return t;
    }

    public void SetState(string text, bool active)
    {
        stateText.text = text;
        on = active;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<Poker>() == null || Time.time - pressedAt < Cooldown) return;
        pressedAt = Time.time;
        pad.PlayClick(on ? 0.9f : 1.1f);
        action?.Invoke();
    }

    void Update()
    {
        // Press animation (cap and its labels move together) and LED.
        float t = Time.time - pressedAt;
        float press = t < 0.08f ? t / 0.08f : Mathf.Clamp01(1f - (t - 0.08f) / 0.15f);
        cap.localPosition = new Vector3(0f, 0f, 0.012f - Travel * press);
        faceRoot.localPosition = new Vector3(0f, 0f, -Travel * press);

        var c = on ? Keypad.Accent : Keypad.Accent * 0.15f;
        if (t < 0.35f) c = Color.Lerp(Color.white, c, t / 0.35f);
        c.a = 1f;
        ledMat.color = c;
    }

    void OnDestroy()
    {
        if (ledMat != null) Destroy(ledMat);
    }
}
