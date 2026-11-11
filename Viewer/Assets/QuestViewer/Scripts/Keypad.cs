using System;
using UnityEngine;

/// A small three-button panel you can place anywhere (pinch to move it). Press buttons with a fingertip
/// or controller tip: HOLO (hologram mode), HUD (Visor / Minimal / Off), LIGHT (Shaded / Flat).
[RequireComponent(typeof(Grabbable))]
public class Keypad : MonoBehaviour
{
    static readonly Color Accent = new Color(0.35f, 0.9f, 1f);

    KeypadButton holo, hud, lighting;
    AudioSource audioSource;
    AudioClip click;

    void Awake()
    {
        var baseMat = NewLit(new Color(0.08f, 0.09f, 0.11f), 0.6f);
        var capMat = NewLit(new Color(0.2f, 0.22f, 0.26f), 0.5f);

        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(plate.GetComponent<Collider>());
        plate.name = "Plate";
        plate.transform.SetParent(transform, false);
        plate.transform.localScale = new Vector3(0.15f, 0.06f, 0.012f);
        plate.GetComponent<Renderer>().sharedMaterial = baseMat;

        var grab = gameObject.AddComponent<BoxCollider>();
        grab.size = new Vector3(0.15f, 0.06f, 0.012f);

        holo = KeypadButton.Create(transform, new Vector3(-0.047f, 0f, 0f), "HOLO", capMat, ViewModes.ToggleHologram, this);
        hud = KeypadButton.Create(transform, new Vector3(0f, 0f, 0f), "HUD", capMat, ViewModes.CycleHud, this);
        lighting = KeypadButton.Create(transform, new Vector3(0.047f, 0f, 0f), "LIGHT", capMat, ViewModes.ToggleLighting, this);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 0.3f;
        audioSource.playOnAwake = false;
        click = MakeClick();
    }

    void OnEnable() { ViewModes.Changed += Refresh; Refresh(); }
    void OnDisable() => ViewModes.Changed -= Refresh;

    void Refresh()
    {
        if (holo == null) return;
        holo.SetState(ViewModes.Hologram ? "ON" : "OFF", ViewModes.Hologram);
        hud.SetState(ViewModes.Hud.ToString().ToUpperInvariant().Replace("MINIMAL", "MIN"), ViewModes.Hud != HudMode.Off);
        lighting.SetState(ViewModes.Lighting.ToString().ToUpperInvariant(), ViewModes.Lighting == LightingMode.Shaded);
    }

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

    internal static Color AccentColor => Accent;
}

/// One keypad button: pressed by anything with a Poker collider.
public class KeypadButton : MonoBehaviour
{
    const float Travel = 0.006f;
    const float Cooldown = 0.35f;

    Action action;
    Keypad pad;
    Transform cap;
    TextMesh stateText;
    Renderer led;
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
        trigger.size = new Vector3(0.036f, 0.036f, 0.03f);
        trigger.center = new Vector3(0f, 0f, 0.016f);

        var capGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(capGo.GetComponent<Collider>());
        capGo.name = "Cap";
        b.cap = capGo.transform;
        b.cap.SetParent(go.transform, false);
        b.cap.localScale = new Vector3(0.036f, 0.036f, 0.012f);
        b.cap.localPosition = new Vector3(0f, 0f, 0.012f);
        capGo.GetComponent<Renderer>().sharedMaterial = capMat;

        var title = MakeText(b.cap, label, new Vector3(0f, 0.004f, 0.51f), 0.0011f, Color.white);
        title.fontStyle = FontStyle.Bold;
        b.stateText = MakeText(b.cap, "", new Vector3(0f, -0.2f, 0.51f), 0.0008f, new Color(1f, 1f, 1f, 0.7f));

        var ledGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(ledGo.GetComponent<Collider>());
        ledGo.name = "Led";
        ledGo.transform.SetParent(b.cap, false);
        ledGo.transform.localPosition = new Vector3(0f, 0.36f, 0.51f);
        ledGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        ledGo.transform.localScale = new Vector3(0.5f, 0.07f, 1f);
        b.ledMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        b.led = ledGo.GetComponent<Renderer>();
        b.led.sharedMaterial = b.ledMat;
        return b;
    }

    static TextMesh MakeText(Transform parent, string text, Vector3 localPos, float size, Color color)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        // The cap is scaled; undo that so the text keeps its own proportions.
        go.transform.localScale = new Vector3(1f / parent.localScale.x, 1f / parent.localScale.y, 1f / parent.localScale.z);
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
        // Press animation and LED (bright flash on press, steady accent when on, dim when off).
        float t = Time.time - pressedAt;
        float press = t < 0.08f ? t / 0.08f : Mathf.Clamp01(1f - (t - 0.08f) / 0.15f);
        cap.localPosition = new Vector3(0f, 0f, 0.012f - Travel * press);

        var accent = Keypad.AccentColor;
        var c = on ? accent : accent * 0.18f;
        if (t < 0.35f) c = Color.Lerp(Color.white, c, t / 0.35f);
        c.a = 1f;
        ledMat.color = c;
    }

    void OnDestroy()
    {
        if (ledMat != null) Destroy(ledMat);
    }
}
