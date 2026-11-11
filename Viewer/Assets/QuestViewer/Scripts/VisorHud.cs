using UnityEngine;

/// Helmet-style visor at the edges of your view. Mostly empty; it reacts to events:
/// boot-up, incoming data (scan sweep), model arrival (bracket pulse), scaling (readout and true-size lock),
/// errors (red flash) and mode changes. Visor mode shows everything, Minimal only the readouts, Off nothing.
public class VisorHud : MonoBehaviour
{
    public Transform head;
    public Grabbable model;
    public float distance = 1.0f;
    public float halfWidthDegrees = 24f;
    public float halfHeightDegrees = 17f;

    static readonly Color Accent = new Color(0.35f, 0.9f, 1f, 1f);
    static readonly Color Green = new Color(0.45f, 1f, 0.55f, 1f);
    static readonly Color Red = new Color(1f, 0.4f, 0.35f, 1f);

    Transform root;
    LineRenderer[] brackets;
    LineRenderer sweep;
    TextMesh centerText, bottomText;
    float w, h, arm;

    float bootAt = -100f, incomingAt = -100f, incomingUntil = -100f, arrivedAt = -100f, errorAt = -100f, lockAt = -100f, modeAt = -100f;
    string incomingName = "", modeText = "";
    bool placed;

    void Awake()
    {
        root = new GameObject("Visor").transform;
        w = Mathf.Tan(halfWidthDegrees * Mathf.Deg2Rad) * distance;
        h = Mathf.Tan(halfHeightDegrees * Mathf.Deg2Rad) * distance;
        arm = 0.06f * distance;

        brackets = new LineRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            brackets[i] = HeadLocked.CreateLine("Bracket" + i, root, 0.0025f * distance);
            brackets[i].positionCount = 3;
        }
        sweep = HeadLocked.CreateLine("Sweep", root, 0.0015f * distance);
        sweep.positionCount = 2;

        centerText = HeadLocked.CreateText("VisorCenter", 0.0034f * distance, TextAnchor.MiddleCenter, root);
        centerText.transform.localPosition = new Vector3(0f, h * 0.55f, 0f);
        bottomText = HeadLocked.CreateText("VisorBottom", 0.0032f * distance, TextAnchor.MiddleCenter, root);
        bottomText.transform.localPosition = new Vector3(0f, -h * 0.82f, 0f);
    }

    void OnEnable()
    {
        ModelServer.DownloadStarted += OnIncoming;
        ModelServer.Failed += OnFailed;
        ModelHost.ModelLoaded += OnLoaded;
        Grabbable.SnapChanged += OnSnap;
        ViewModes.Changed += OnModeChanged;
    }

    void OnDisable()
    {
        ModelServer.DownloadStarted -= OnIncoming;
        ModelServer.Failed -= OnFailed;
        ModelHost.ModelLoaded -= OnLoaded;
        Grabbable.SnapChanged -= OnSnap;
        ViewModes.Changed -= OnModeChanged;
    }

    void Start() => bootAt = Time.time + 0.6f; // give tracking a moment so the boot animation is seen

    void OnIncoming(string name) { incomingName = name; incomingAt = Time.time; incomingUntil = Time.time + 30f; }
    void OnFailed(string _) { errorAt = Time.time; incomingUntil = Time.time; }
    void OnLoaded(ModelHost _) { arrivedAt = Time.time; incomingUntil = Time.time; }
    void OnSnap(Grabbable g, bool snapped) { if (g == model && snapped) lockAt = Time.time; }

    void OnModeChanged()
    {
        modeAt = Time.time;
        modeText = $"HOLO {(ViewModes.Hologram ? "ON" : "OFF")}   //   HUD {ViewModes.Hud.ToString().ToUpperInvariant()}   //   LIGHT {ViewModes.Lighting.ToString().ToUpperInvariant()}";
    }

    void LateUpdate()
    {
        var mode = ViewModes.Hud;
        bool visor = mode == HudMode.Visor;
        root.gameObject.SetActive(mode != HudMode.Off && head != null);
        if (mode == HudMode.Off || head == null) { placed = false; return; }

        HeadLocked.Follow(root, HeadLocked.Target(head, 0f, distance), 10f, !placed);
        placed = true;

        float now = Time.time;
        UpdateBrackets(visor, now);
        UpdateSweep(visor, now);
        UpdateTexts(visor, now);
    }

    void UpdateBrackets(bool visor, float now)
    {
        // Boot: arms grow in, staggered per corner. Arrival: pulse outwards. Colour: lock green, error red.
        float boot = now - bootAt;
        float pulse = Bump(now - arrivedAt, 0.5f) * 0.05f;
        var color = Accent;
        color.a = 0.7f;
        float lockT = now - lockAt;
        if (lockT < 0.9f) color = Color.Lerp(Green, color, lockT / 0.9f);
        float errT = now - errorAt;
        if (errT < 1.2f && Mathf.Repeat(errT, 0.4f) < 0.22f) color = Red;
        float arriveT = now - arrivedAt;
        if (arriveT < 0.4f) color = Color.Lerp(Color.white, color, arriveT / 0.4f);

        for (int i = 0; i < 4; i++)
        {
            var b = brackets[i];
            b.enabled = visor && boot > 0f;
            if (!b.enabled) continue;
            float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((boot - i * 0.08f) / 0.45f));
            float sx = (i % 2 == 0) ? -1f : 1f;
            float sy = (i < 2) ? 1f : -1f;
            var corner = new Vector3(sx * w * (1f + pulse), sy * h * (1f + pulse), 0f);
            float a = arm * grow;
            b.SetPosition(0, corner + new Vector3(-sx * a, 0f, 0f));
            b.SetPosition(1, corner);
            b.SetPosition(2, corner + new Vector3(0f, -sy * a, 0f));
            b.startColor = b.endColor = color;
        }
    }

    void UpdateSweep(bool visor, float now)
    {
        bool active = visor && now < incomingUntil;
        sweep.enabled = active;
        if (!active) return;
        float y = Mathf.Lerp(h, -h, Mathf.Repeat((now - incomingAt) / 0.9f, 1f));
        sweep.SetPosition(0, new Vector3(-w * 0.92f, y, 0f));
        sweep.SetPosition(1, new Vector3(w * 0.92f, y, 0f));
        var c = Accent;
        c.a = 0.55f;
        sweep.startColor = sweep.endColor = c;
    }

    void UpdateTexts(bool visor, float now)
    {
        // Centre-top: incoming / complete (visor only).
        string center = "";
        var centerColor = Accent;
        if (visor && now < incomingUntil)
        {
            bool blink = Mathf.Repeat(now * 2.5f, 1f) < 0.6f;
            center = (blink ? "INCOMING DATA  >  " : "                  ") + incomingName.ToUpperInvariant();
        }
        else if (visor && now - arrivedAt < 1.4f)
        {
            center = "LINK COMPLETE";
            centerColor = Green;
        }
        centerText.text = center;
        centerText.color = centerColor;

        // Bottom: scale readout and lock (all non-off modes), mode change, boot line.
        string bottom = "";
        var bottomColor = Accent;
        if (model != null && model.IsScaling)
        {
            bool locked = model.IsTrueSize;
            bottom = locked ? "TRUE SIZE LOCK   100%" : $"SCALE   {model.Scale * 100f:0}%";
            bottomColor = locked ? Green : Accent;
        }
        else if (now - lockAt < 1.2f)
        {
            bottom = "TRUE SIZE LOCK   100%";
            bottomColor = Green;
        }
        else if (now - modeAt < 1.8f)
        {
            bottom = modeText;
        }
        else if (visor && now - bootAt > 0f && now - bootAt < 3f)
        {
            const string boot = "ONSHAPE LINK  //  ONLINE";
            int chars = Mathf.Clamp((int)((now - bootAt - 0.3f) * 28f), 0, boot.Length);
            bottom = boot.Substring(0, chars) + (chars < boot.Length ? "_" : "");
        }
        bottomText.text = bottom;
        bottomText.color = bottomColor;
    }

    /// 0 -> 1 -> 0 over 'duration' seconds after t = 0.
    static float Bump(float t, float duration) => t < 0f || t > duration ? 0f : Mathf.Sin(t / duration * Mathf.PI);

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }
}
