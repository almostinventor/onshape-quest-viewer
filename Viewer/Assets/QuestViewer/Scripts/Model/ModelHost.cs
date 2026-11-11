using System;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

/// The grabbable "Model" object. Loads a GLB at runtime and hot-swaps its content: the Model object itself is kept,
/// so if you've held or placed it, the new version appears in the same pose (and scale); if you never touched it,
/// it appears in front of your head.
[RequireComponent(typeof(Grabbable), typeof(BoxCollider), typeof(SpawnInFront))]
public class ModelHost : MonoBehaviour
{
    [Tooltip("Hidden once a model is loaded (the 100 mm test cube).")]
    public GameObject placeholder;

    public string CurrentName { get; private set; }
    /// True size in mm, in Onshape's axes (X, Y, Z = width, depth, height).
    public Vector3 SizeMm { get; private set; }
    public bool HasModel => content != null;
    public float LoadedAt { get; private set; } = -100f;
    public int PartCount { get; private set; }

    /// Raised on the main thread after a new model is in the scene.
    public static event Action<ModelHost> ModelLoaded;

    const int MaxPartColliders = 96;

    Grabbable grabbable;
    BoxCollider box;
    SpawnInFront spawn;
    Transform content;
    GltfImport current;
    bool busy;

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
        box = GetComponent<BoxCollider>();
        spawn = GetComponent<SpawnInFront>();
        box.enabled = false; // nothing to grab until a model is loaded
    }

    /// Main thread only. Returns a one-line summary, throws on failure (the old model stays).
    public async Task<string> LoadGlbAsync(byte[] glb, string displayName)
    {
        if (busy) throw new InvalidOperationException("Already loading a model.");
        busy = true;
        try
        {
            var import = new GltfImport();
            if (!await import.Load(glb))
            {
                import.Dispose();
                throw new Exception("The file is not a valid glTF.");
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

            DepthOcclusion.Apply(newContent.gameObject);
            if (content != null) Destroy(content.gameObject);
            current?.Dispose();
            content = newContent;
            current = import;
            DepthOcclusion.ReleaseUnused();

            // Recentre on the bounding-box centre and fit the colliders.
            var bounds = LocalBounds(content);
            content.localPosition -= bounds.center;
            box.center = Vector3.zero;
            box.size = Vector3.Max(bounds.size, Vector3.one * 0.01f);
            // Per-part convex colliders so fingers push the real shape; a single box for very large models.
            var filters = content.GetComponentsInChildren<MeshFilter>();
            bool perPart = filters.Length > 0 && filters.Length <= MaxPartColliders;
            if (perPart)
                foreach (var f in filters)
                    if (f.sharedMesh != null) f.gameObject.AddComponent<MeshCollider>().convex = true;
            box.enabled = !perPart;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.ResetCenterOfMass();

            var look = GetComponent<ModelLook>();
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
            if (!grabbable.HasBeenHeld) spawn.Respawn();

            CurrentName = displayName;
            SizeMm = new Vector3(bounds.size.x, bounds.size.z, bounds.size.y) * 1000f;
            LoadedAt = Time.time;
            PartCount = filters.Length;
            var summary = $"{displayName}: {SizeMm.x:0.#} x {SizeMm.y:0.#} x {SizeMm.z:0.#} mm";
            Hud.Ok("Loaded " + displayName, 2f);
            ModelLoaded?.Invoke(this);
            return summary;
        }
        finally
        {
            busy = false;
        }
    }

    /// Bounds of all renderers under 'root', in this object's local (unscaled) space.
    Bounds LocalBounds(Transform root)
    {
        var toLocal = transform.worldToLocalMatrix;
        bool any = false;
        var b = new Bounds();
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            var lb = r.localBounds;
            var m = toLocal * r.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = m.MultiplyPoint3x4(corner);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    void OnDestroy() => current?.Dispose();
}
