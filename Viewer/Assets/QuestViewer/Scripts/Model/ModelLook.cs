using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// How an object is drawn: its normal materials, or the hologram (ViewModes.Hologram).
/// New content "materializes" with a short hologram scan from bottom to top.
public class ModelLook : MonoBehaviour
{
    [Tooltip("Hologram material (QuestViewer/Hologram).")]
    public Material hologramTemplate;
    public float scanSeconds = 1.1f;
    public float sweepPeriod = 2.6f;

    static readonly int BandY = Shader.PropertyToID("_BandY");
    static readonly int BandStrength = Shader.PropertyToID("_BandStrength");

    readonly Dictionary<Renderer, Material[]> solid = new Dictionary<Renderer, Material[]>();
    Material hologram;
    float scanStart = -100f;
    bool showingHologram;

    void Awake()
    {
        if (hologramTemplate != null) hologram = new Material(hologramTemplate);
    }

    void OnEnable() => ViewModes.Changed += Refresh;
    void OnDisable() => ViewModes.Changed -= Refresh;
    void OnDestroy() { if (hologram != null) Destroy(hologram); }

    /// Remember the current (solid) materials under this object. Call after its content changes.
    public void Capture()
    {
        solid.Clear();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.GetComponent<TextMesh>() != null) continue;
            solid[r] = r.sharedMaterials;
        }
        showingHologram = false;
        Refresh();
    }

    /// Play the materialize scan (any mode).
    public void ScanIn()
    {
        scanStart = Time.time;
        StopAllCoroutines();
        StartCoroutine(ScanRoutine());
    }

    IEnumerator ScanRoutine()
    {
        Show(true);
        yield return new WaitForSeconds(scanSeconds);
        Refresh();
    }

    void Refresh()
    {
        bool scanning = Time.time - scanStart < scanSeconds;
        Show(ViewModes.Hologram || scanning);
    }

    void Show(bool holo)
    {
        if (hologram == null || holo == showingHologram) return;
        showingHologram = holo;
        foreach (var kv in solid)
        {
            if (kv.Key == null) continue;
            if (holo)
            {
                var mats = new Material[kv.Value.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = hologram;
                kv.Key.sharedMaterials = mats;
            }
            else kv.Key.sharedMaterials = kv.Value;
        }
    }

    void LateUpdate()
    {
        if (!showingHologram || hologram == null) return;
        if (!TryWorldBounds(out var b)) return;

        float t = Time.time - scanStart;
        bool scanning = t < scanSeconds;
        float phase = scanning ? t / scanSeconds : Mathf.Repeat(Time.time / sweepPeriod, 1f);
        float pad = b.extents.y * 0.15f + 0.01f;
        hologram.SetFloat(BandY, Mathf.Lerp(b.min.y - pad, b.max.y + pad, phase));
        hologram.SetFloat(BandStrength, scanning ? 2.4f : 1.4f);
    }

    bool TryWorldBounds(out Bounds b)
    {
        b = default;
        bool any = false;
        foreach (var r in solid.Keys)
        {
            if (r == null || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any;
    }
}
