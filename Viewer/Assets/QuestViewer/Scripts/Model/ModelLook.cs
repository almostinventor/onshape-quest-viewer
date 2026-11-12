using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// How the object is drawn. Each loaded material becomes a "Part" material (colour, metalness, smoothness;
/// section cut, clay and depth occlusion are handled in the shader). On top of that:
///   Hologram mode > X-ray style > Solid / Clay;  inspect isolation keeps one part solid and the rest X-ray.
/// New content materialises with a hologram scan; replaced content fades out as a ghost.
public class ModelLook : MonoBehaviour
{
    public Material partTemplate;
    public Material xrayTemplate;
    public Material hologramTemplate;
    public float scanSeconds = 1.1f;
    public float sweepPeriod = 2.6f;
    public float ghostSeconds = 4f;

    static readonly int BandY = Shader.PropertyToID("_BandY");
    static readonly int BandStrength = Shader.PropertyToID("_BandStrength");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    static readonly int HighlightId = Shader.PropertyToID("_Highlight");
    static readonly int FadeId = Shader.PropertyToID("_Fade");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    readonly Dictionary<Renderer, Material[]> solid = new Dictionary<Renderer, Material[]>();
    readonly List<Material> owned = new List<Material>();
    Material hologram, xray;
    float scanStart = -100f;
    Renderer isolated;
    Renderer highlighted;
    MaterialPropertyBlock block;

    void Awake()
    {
        block = new MaterialPropertyBlock();
        if (hologramTemplate != null) hologram = new Material(hologramTemplate);
        if (xrayTemplate != null) xray = new Material(xrayTemplate);
    }

    void OnEnable() => ViewModes.Changed += Refresh;
    void OnDisable() => ViewModes.Changed -= Refresh;

    void OnDestroy()
    {
        if (hologram != null) Destroy(hologram);
        if (xray != null) Destroy(xray);
        foreach (var m in owned) if (m != null) Destroy(m);
    }

    /// Converts and remembers the materials of everything under this object. Call after its content changes.
    public void Capture()
    {
        foreach (var m in owned) if (m != null) Destroy(m);
        owned.Clear();
        solid.Clear();
        isolated = highlighted = null;
        var converted = new Dictionary<Material, Material>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.GetComponent<TextMesh>() != null || r.GetComponentInParent<GhostMarker>() != null) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                if (!converted.TryGetValue(mats[i], out var c)) converted[mats[i]] = c = Convert(mats[i]);
                mats[i] = c;
            }
            solid[r] = mats;
        }
        Refresh();
    }

    Material Convert(Material src)
    {
        if (partTemplate == null) return src;
        var m = new Material(partTemplate) { name = src.name + " (part)" };
        var color = src.HasProperty("baseColorFactor") ? src.GetColor("baseColorFactor")
            : src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor")
            : src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
        m.SetColor(ColorId, color);
        if (src.HasProperty("metallicFactor")) m.SetFloat(MetallicId, src.GetFloat("metallicFactor"));
        if (src.HasProperty("roughnessFactor")) m.SetFloat(SmoothnessId, 1f - src.GetFloat("roughnessFactor"));
        else if (src.HasProperty("_Smoothness")) m.SetFloat(SmoothnessId, src.GetFloat("_Smoothness"));
        owned.Add(m);
        return m;
    }

    public void ScanIn()
    {
        scanStart = Time.time;
        StopCoroutine(nameof(EndScan));
        StartCoroutine(nameof(EndScan));
        Refresh();
    }

    IEnumerator EndScan()
    {
        yield return new WaitForSeconds(scanSeconds);
        Refresh();
    }

    bool Scanning => Time.time - scanStart < scanSeconds;

    /// Keep one part solid and show the rest as X-ray (null = show all normally).
    public void Isolate(Renderer r)
    {
        isolated = r;
        Refresh();
    }

    public Renderer Isolated => isolated;

    /// Tint one part (null = none).
    public void Highlight(Renderer r)
    {
        if (highlighted == r) return;
        if (highlighted != null) highlighted.SetPropertyBlock(null);
        highlighted = r;
        if (r != null)
        {
            block.Clear();
            block.SetColor(HighlightId, new Color(0.25f, 0.75f, 1f, 0.45f));
            r.SetPropertyBlock(block);
        }
    }

    void Refresh()
    {
        foreach (var kv in solid)
        {
            var r = kv.Key;
            if (r == null) continue;
            Material over = null;
            if ((ViewModes.Hologram || Scanning) && hologram != null) over = hologram;
            else if (isolated != null && r != isolated && xray != null) over = xray;
            else if (ViewModes.Style == LookStyle.XRay && xray != null) over = xray;

            if (over == null) r.sharedMaterials = kv.Value;
            else
            {
                var mats = new Material[kv.Value.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = over;
                r.sharedMaterials = mats;
            }
        }
    }

    void LateUpdate()
    {
        if (hologram == null || !(ViewModes.Hologram || Scanning)) return;
        if (!TryWorldBounds(out var b)) return;
        float t = Time.time - scanStart;
        bool scanning = Scanning;
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

    /// Turns 'old' (previous content) into a warm translucent ghost that fades out, then destroys it.
    public void Ghost(Transform old, Action onDone)
    {
        old.gameObject.AddComponent<GhostMarker>();
        foreach (var c in old.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        if (xrayTemplate == null) { Destroy(old.gameObject); onDone?.Invoke(); return; }
        var ghost = new Material(xrayTemplate);
        ghost.SetColor(ColorId, new Color(1f, 0.85f, 0.55f, 1f));
        ghost.SetFloat(AlphaId, 0.08f);
        foreach (var r in old.GetComponentsInChildren<Renderer>(true))
        {
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = ghost;
            r.sharedMaterials = mats;
        }
        StartCoroutine(FadeGhost(old, ghost, onDone));
    }

    IEnumerator FadeGhost(Transform old, Material ghost, Action onDone)
    {
        float t0 = Time.time;
        while (Time.time - t0 < ghostSeconds && old != null)
        {
            ghost.SetFloat(FadeId, 1f - (Time.time - t0) / ghostSeconds);
            yield return null;
        }
        if (old != null) Destroy(old.gameObject);
        Destroy(ghost);
        onDone?.Invoke();
    }
}

/// Marks a fading previous version so it isn't treated as current content.
public class GhostMarker : MonoBehaviour { }
