using UnityEngine;

/// Section cut (CUT mode): a frame you can pinch and move/tilt. Everything on the side it faces (towards you
/// when it appears) is cut away, and cut faces show in a cross-section colour (see the Part shader).
[RequireComponent(typeof(Grabbable))]
public class CutPlane : MonoBehaviour
{
    public Transform head;
    public ModelHost host;
    public Transform placeholder;
    public Material frameMaterial;

    static readonly int CutPlaneId = Shader.PropertyToID("_QV_CutPlane");
    static readonly int CutOnId = Shader.PropertyToID("_QV_CutOn");

    LineRenderer frame, cross;
    BoxCollider box;
    bool wasOn;

    void Awake()
    {
        box = gameObject.AddComponent<BoxCollider>();
        frame = MakeLine("Frame", 5, 0.003f);
        cross = MakeLine("Cross", 2, 0.002f);
        Shader.SetGlobalFloat(CutOnId, 0f);
        SetVisible(false);
    }

    LineRenderer MakeLine(string name, int points, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var l = go.AddComponent<LineRenderer>();
        l.useWorldSpace = false;
        l.positionCount = points;
        l.startWidth = l.endWidth = width;
        l.sharedMaterial = frameMaterial;
        l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        l.alignment = LineAlignment.TransformZ;
        return l;
    }

    void OnEnable() => ViewModes.Changed += OnModes;
    void OnDisable() => ViewModes.Changed -= OnModes;

    void OnModes()
    {
        bool on = ViewModes.Cut;
        if (on && !wasOn) Place();
        if (!on) Shader.SetGlobalFloat(CutOnId, 0f);
        SetVisible(on);
        wasOn = on;
    }

    /// Through the middle of the object, facing you, sized to it.
    void Place()
    {
        var target = host != null && host.HasModel ? host.transform : placeholder;
        var bounds = WorldBounds(target);
        float size = Mathf.Clamp(bounds.size.magnitude * 0.8f, 0.12f, 1.5f);
        var toHead = head != null ? head.position - bounds.center : Vector3.back;
        toHead = Vector3.ProjectOnPlane(toHead, Vector3.up).normalized;
        if (toHead.sqrMagnitude < 1e-4f) toHead = Vector3.back;
        transform.SetPositionAndRotation(bounds.center, Quaternion.LookRotation(toHead, Vector3.up));

        float h = size * 0.5f;
        frame.SetPositions(new[] { new Vector3(-h, -h, 0), new Vector3(h, -h, 0), new Vector3(h, h, 0), new Vector3(-h, h, 0), new Vector3(-h, -h, 0) });
        cross.SetPositions(new[] { new Vector3(-h * 0.12f, 0, 0), new Vector3(h * 0.12f, 0, 0) });
        // Grab only near the frame edge area (a thin slab), so pinching the model still grabs the model.
        box.size = new Vector3(size, size, 0.01f);
    }

    static Bounds WorldBounds(Transform t)
    {
        var b = new Bounds(t != null ? t.position : Vector3.zero, Vector3.one * 0.1f);
        if (t == null) return b;
        bool any = false;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    void SetVisible(bool on)
    {
        frame.enabled = cross.enabled = on;
        box.enabled = on;
    }

    void LateUpdate()
    {
        if (!ViewModes.Cut) return;
        var n = transform.forward;
        Shader.SetGlobalVector(CutPlaneId, new Vector4(n.x, n.y, n.z, -Vector3.Dot(n, transform.position)));
        Shader.SetGlobalFloat(CutOnId, 1f);
    }
}
