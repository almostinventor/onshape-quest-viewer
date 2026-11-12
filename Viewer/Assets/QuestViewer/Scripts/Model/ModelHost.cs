using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

/// The grabbable "Model" object. Loads a GLB at runtime and hot-swaps its content: the Model object itself is kept,
/// so the new version appears where you left it (same pose and scale); the very first model appears in front of you.
/// The previous version fades out as a ghost so changes are easy to spot.
[RequireComponent(typeof(Grabbable), typeof(BoxCollider), typeof(SpawnInFront))]
public class ModelHost : MonoBehaviour
{
    [Tooltip("Hidden once a model is loaded (the 100 mm test cube).")]
    public GameObject placeholder;
    [Tooltip("How far parts move apart in the exploded view, relative to their distance from the centre.")]
    public float explodeAmount = 0.9f;

    public string CurrentName { get; private set; }
    /// True size in mm, in Onshape's axes (X, Y, Z = width, depth, height).
    public Vector3 SizeMm { get; private set; }
    public bool HasModel => content != null;
    public IReadOnlyList<ModelPart> Parts => parts;
    public Transform Content => content;

    /// Raised on the main thread after a new model is in the scene.
    public static event Action<ModelHost> ModelLoaded;

    const int MaxPartColliders = 96;

    Grabbable grabbable;
    BoxCollider box;
    SpawnInFront spawn;
    ModelLook look;
    Transform content;
    GltfImport current;
    readonly List<ModelPart> parts = new List<ModelPart>();
    bool busy;
    bool everLoaded;

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        box = GetComponent<BoxCollider>();
        spawn = GetComponent<SpawnInFront>();
        look = GetComponent<ModelLook>();
        box.enabled = false; // nothing to grab until a model is loaded
    }

    /// Main thread only. partInfoJson: {"partId": {"m": kg, "v": m3}, ...} from Onshape (optional).
    /// Returns a one-line summary; throws on failure (the old model stays).
    public async Task<string> LoadGlbAsync(byte[] glb, string displayName, string partInfoJson = null)
    {
        if (busy) throw new InvalidOperationException("Already loading a model.");
        busy = true;
        try
        {
            var import = new GltfImport();
            if (!await import.Load(glb))
            {
                import.Dispose();
                throw new Exception("Not a valid glTF.");
            }

            // Root (keeps pose and scale) -> Content (recentred) -> glTF scene.
            var newContent = new GameObject("Content").transform;
            newContent.SetParent(transform, false);
            if (!await import.InstantiateMainSceneAsync(newContent))
            {
                Destroy(newContent.gameObject);
                import.Dispose();
                throw new Exception("The model could not be created.");
            }

            // Old version becomes a fading ghost; then swap in the new one.
            var old = content;
            var oldImport = current;
            content = newContent;
            current = import;
            if (old != null)
            {
                if (look != null) look.Ghost(old, () => oldImport?.Dispose());
                else { Destroy(old.gameObject); oldImport?.Dispose(); }
            }

            // Recentre on the bounding-box centre.
            var bounds = LocalBounds(content);
            content.localPosition -= bounds.center;
            box.center = Vector3.zero;
            box.size = Vector3.Max(bounds.size, Vector3.one * 0.01f);

            BuildParts(glb, partInfoJson);

            // Per-part convex colliders so fingers push the real shape; a single box for very large models.
            bool perPart = parts.Count > 0 && parts.Count <= MaxPartColliders;
            if (perPart)
                foreach (var p in parts)
                    if (p.GetComponent<MeshFilter>() is { } f && f.sharedMesh != null) p.gameObject.AddComponent<MeshCollider>().convex = true;
            box.enabled = !perPart;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.ResetCenterOfMass();

            if (look != null)
            {
                look.Capture();
                look.ScanIn();
            }

            if (placeholder != null && placeholder.activeSelf)
            {
                var g = placeholder.GetComponent<Grabbable>();
                if (g != null) g.ReleaseAll();
                placeholder.SetActive(false);
            }
            if (!everLoaded) spawn.Respawn();
            everLoaded = true;

            CurrentName = displayName;
            SizeMm = new Vector3(bounds.size.x, bounds.size.z, bounds.size.y) * 1000f;
            var summary = $"{displayName}: {SizeMm.x:0.#} x {SizeMm.y:0.#} x {SizeMm.z:0.#} mm";
            Status.Ok("Loaded\n" + displayName, 2f);
            ModelLoaded?.Invoke(this);
            return summary;
        }
        finally
        {
            busy = false;
        }
    }

    /// Creates a ModelPart per mesh object, with Onshape names/IDs from the GLB and mass/volume from partInfo.
    void BuildParts(byte[] glb, string partInfoJson)
    {
        parts.Clear();
        var nodeIds = ReadNodePartIds(glb);         // node name -> part IDs in node order
        var info = partInfoJson != null ? SafeParse(partInfoJson) : null;
        var used = new Dictionary<string, int>();
        var centre = Vector3.zero;                    // model centre in Model-local space after recentring

        foreach (var r in content.GetComponentsInChildren<MeshRenderer>(true))
        {
            var part = r.gameObject.AddComponent<ModelPart>();
            part.Renderer = r;
            part.PartName = r.gameObject.name;
            used.TryGetValue(part.PartName, out int n);
            used[part.PartName] = n + 1;
            if (nodeIds.TryGetValue(part.PartName, out var ids) && n < ids.Count) part.PartId = ids[n];

            if (part.PartId != null && MiniJson.Get(info, part.PartId) is { } pi)
            {
                part.MassKg = (float)MiniJson.Num(MiniJson.Get(pi, "m"));
                part.VolumeM3 = (float)MiniJson.Num(MiniJson.Get(pi, "v"));
            }

            var b = RendererBoundsIn(r, transform);
            part.SizeMm = new Vector3(b.size.x, b.size.z, b.size.y) * 1000f;
            part.Home = r.transform.localPosition;
            part.HomeRotation = r.transform.localRotation;
            // Push outwards from the model centre (in the part's parent space).
            var dirModel = (b.center - centre) * explodeAmount;
            var parent = r.transform.parent;
            part.ExplodeOffset = parent.InverseTransformVector(transform.TransformVector(dirModel));
            parts.Add(part);
        }
    }

    /// glTF node name -> PTC_onshape_metadata part IDs, in node order (names can repeat).
    static Dictionary<string, List<string>> ReadNodePartIds(byte[] glb)
    {
        var map = new Dictionary<string, List<string>>();
        try
        {
            int len = BitConverter.ToInt32(glb, 12);
            var root = MiniJson.Parse(Encoding.UTF8.GetString(glb, 20, len));
            if (MiniJson.AsList(MiniJson.Get(root, "nodes")) is { } nodes)
                foreach (var node in nodes)
                {
                    if (!(MiniJson.Get(node, "name") is string name)) continue;
                    var ids = MiniJson.AsList(MiniJson.Get(MiniJson.Get(MiniJson.Get(node, "extensions"), "PTC_onshape_metadata"), "id"));
                    if (ids == null || ids.Count == 0 || !(ids[0] is string id)) continue;
                    if (!map.TryGetValue(name, out var list)) map[name] = list = new List<string>();
                    list.Add(id);
                }
        }
        catch (Exception e) { Debug.LogWarning("[ModelHost] Could not read part IDs: " + e.Message); }
        return map;
    }

    static object SafeParse(string json)
    {
        try { return MiniJson.Parse(json); }
        catch (Exception) { return null; }
    }

    Bounds LocalBounds(Transform root)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            var rb = RendererBoundsIn(r, transform);
            if (!any) { b = rb; any = true; }
            else b.Encapsulate(rb);
        }
        return b;
    }

    /// Bounds of one renderer in 'space' (local, unscaled).
    static Bounds RendererBoundsIn(Renderer r, Transform space)
    {
        var lb = r.localBounds;
        var m = space.worldToLocalMatrix * r.transform.localToWorldMatrix;
        var b = new Bounds(m.MultiplyPoint3x4(lb.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            b.Encapsulate(m.MultiplyPoint3x4(corner));
        }
        return b;
    }

    void OnDestroy() => current?.Dispose();
}
