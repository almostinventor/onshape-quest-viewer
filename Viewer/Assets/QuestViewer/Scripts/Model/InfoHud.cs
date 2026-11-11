using UnityEngine;

/// Model name, true size and scale, shown at the top centre of your view (not on the model).
/// Locking onto 100% flashes "100% · true size" in green.
public class InfoHud : MonoBehaviour
{
    public Transform head;
    public ModelHost host;
    public Grabbable model;
    public float upDegrees = 17f;
    public float distance = 0.8f;

    static readonly Color Dim = new Color(1f, 1f, 1f, 0.85f);
    static readonly Color Green = new Color(0.45f, 1f, 0.55f);
    static readonly Color VisorColor = new Color(0.55f, 0.95f, 1f, 0.95f);

    TextMesh text;
    float flashUntil = -1f;
    bool placed;

    void Awake()
    {
        text = HeadLocked.CreateText("InfoHud", 0.0032f, TextAnchor.LowerCenter);
        text.color = Dim;
    }

    void OnEnable() => Grabbable.SnapChanged += OnSnap;
    void OnDisable() => Grabbable.SnapChanged -= OnSnap;

    void OnSnap(Grabbable g, bool snapped)
    {
        if (g == model && snapped) flashUntil = Time.time + 1.5f;
    }

    void LateUpdate()
    {
        bool show = host != null && host.HasModel && head != null && ViewModes.Hud != HudMode.Off;
        text.gameObject.SetActive(show);
        if (!show) { placed = false; return; }

        var s = host.SizeMm;
        bool flash = Time.time < flashUntil;
        string scale = model.IsTrueSize ? (flash ? "100% · true size" : "") : $"shown at {model.Scale * 100f:0}%";
        string parts = host.PartCount > 1 ? $"   ·   {host.PartCount} parts" : "";
        text.text = $"{host.CurrentName}   {s.x:0.#} × {s.y:0.#} × {s.z:0.#} mm{parts}" + (scale.Length > 0 ? "\n" + scale : "");
        text.color = flash ? Green : ViewModes.Hud == HudMode.Visor ? VisorColor : Dim;

        HeadLocked.Follow(text.transform, HeadLocked.Target(head, upDegrees, distance), 8f, !placed);
        placed = true;
    }
}
