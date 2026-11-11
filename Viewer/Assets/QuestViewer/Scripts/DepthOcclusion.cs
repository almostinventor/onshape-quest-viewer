using System.Collections.Generic;
using Meta.XR.EnvironmentDepth;
using UnityEngine;

/// Real objects in front of the model hide it (Meta Depth API, standalone Quest only).
/// Requests the spatial data permission, then renders models with Meta's occlusion shader,
/// keeping each part's colour, metalness and smoothness.
public class DepthOcclusion : MonoBehaviour
{
    public EnvironmentDepthManager depthManager;
    [Tooltip("Meta's EnvironmentDepth/OcclusionLit material (also keeps the shader in builds).")]
    public Material occlusionTemplate;
    public GameObject[] alsoApplyTo;

    static DepthOcclusion instance;
    readonly List<Material> created = new List<Material>();

    public static bool Active => instance != null && instance.isActiveAndEnabled && Application.platform == RuntimePlatform.Android
                                 && EnvironmentDepthManager.IsSupported;

    static readonly int BaseColorFactor = Shader.PropertyToID("baseColorFactor");
    static readonly int MetallicFactor = Shader.PropertyToID("metallicFactor");
    static readonly int RoughnessFactor = Shader.PropertyToID("roughnessFactor");
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int Color = Shader.PropertyToID("_Color");
    static readonly int Metallic = Shader.PropertyToID("_Metallic");
    static readonly int Glossiness = Shader.PropertyToID("_Glossiness");

    void Awake() => instance = this;

    void Start()
    {
        if (Application.platform != RuntimePlatform.Android || !EnvironmentDepthManager.IsSupported)
        {
            if (depthManager != null) depthManager.enabled = false;
            return;
        }
        if (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.Scene))
            OVRPermissionsRequester.Request(new[] { OVRPermissionsRequester.Permission.Scene });
        foreach (var go in alsoApplyTo) if (go != null) Apply(go);
    }

    /// Swaps the materials under 'root' to occlusion-aware ones. No-op where depth isn't available.
    public static void Apply(GameObject root)
    {
        if (!Active || instance.occlusionTemplate == null) return;
        var map = new Dictionary<Material, Material>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.GetComponent<TextMesh>() != null) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i];
                if (src == null) continue;
                if (!map.TryGetValue(src, out var dst))
                {
                    dst = instance.Convert(src);
                    map[src] = dst;
                }
                mats[i] = dst;
            }
            r.sharedMaterials = mats;
        }
    }

    Material Convert(Material src)
    {
        var m = new Material(occlusionTemplate) { name = src.name + " (occlusion)" };
        var color = src.HasProperty(BaseColorFactor) ? src.GetColor(BaseColorFactor)
            : src.HasProperty(BaseColor) ? src.GetColor(BaseColor)
            : src.HasProperty(Color) ? src.GetColor(Color) : UnityEngine.Color.white;
        m.SetColor(Color, color);
        if (src.HasProperty(MetallicFactor)) m.SetFloat(Metallic, src.GetFloat(MetallicFactor));
        if (src.HasProperty(RoughnessFactor)) m.SetFloat(Glossiness, 1f - src.GetFloat(RoughnessFactor));
        created.Add(m);
        return m;
    }

    /// Frees converted materials that are no longer used (call after replacing a model).
    public static void ReleaseUnused()
    {
        if (instance == null) return;
        var used = new HashSet<Material>();
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            foreach (var m in r.sharedMaterials) if (m != null) used.Add(m);
        instance.created.RemoveAll(m =>
        {
            if (used.Contains(m)) return false;
            Destroy(m);
            return true;
        });
    }
}
