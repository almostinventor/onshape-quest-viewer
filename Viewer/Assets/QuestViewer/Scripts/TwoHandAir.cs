using UnityEngine;

/// Pinch in empty air with both hands:
///   MEASURE mode on:  tape measure between your pinches (mm); stays visible a few seconds after letting go.
///   MEASURE mode off: scale the model in place, like Iron Man: hands apart = bigger, together = smaller
///                     (locks to 100%), without grabbing it.
public class TwoHandAir : MonoBehaviour
{
    public PinchGrabber left;
    public PinchGrabber right;
    public Transform head;
    public Material lineMaterial;
    [Tooltip("Scaled by the gesture: the model when one is loaded, otherwise the test cube.")]
    public Grabbable model;
    public ModelHost modelHost;
    public Grabbable placeholder;
    public float lingerSeconds = 4f;

    LineRenderer line;
    TextMesh text;
    float lastMeasure = -100f;
    bool active;
    bool scaling;
    float startDist;
    Grabbable target;

    void Awake()
    {
        var lineGo = new GameObject("TapeLine");
        line = lineGo.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.startWidth = line.endWidth = 0.0015f;
        line.numCapVertices = 4;
        line.useWorldSpace = true;
        line.sharedMaterial = lineMaterial;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var textGo = new GameObject("TapeLabel");
        text = textGo.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textGo.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
        text.anchor = TextAnchor.LowerCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 64;
        text.characterSize = 0.0035f;
        text.color = new Color(1f, 0.85f, 0.3f);
    }

    void LateUpdate()
    {
        bool both = left != null && right != null && left.IsPinching && right.IsPinching && left.Held == null && right.Held == null;
        var a = both ? left.PinchPose.position : Vector3.zero;
        var b = both ? right.PinchPose.position : Vector3.zero;
        float dist = Vector3.Distance(a, b);

        if (both && !active)
        {
            active = true;
            startDist = Mathf.Max(dist, 0.02f);
            scaling = !ViewModes.Measure;
            target = modelHost != null && modelHost.HasModel ? model : placeholder != null && placeholder.gameObject.activeInHierarchy ? placeholder : null;
            if (scaling && target != null) target.BeginRemoteScale();
        }
        else if (!both && active)
        {
            active = false;
            if (scaling && target != null) target.EndRemoteScale();
            target = null;
        }

        if (active && scaling && target != null) target.UpdateRemoteScale(dist / startDist);

        if (active && !scaling)
        {
            lastMeasure = Time.time;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            text.text = $"{dist * 1000f:0} mm";
            var mid = (a + b) * 0.5f + Vector3.up * 0.015f;
            var toHead = head != null ? Vector3.ProjectOnPlane(mid - head.position, Vector3.up) : Vector3.forward;
            text.transform.SetPositionAndRotation(mid, Quaternion.LookRotation(toHead.sqrMagnitude > 1e-4f ? toHead : Vector3.forward));
        }

        bool show = Time.time - lastMeasure < lingerSeconds && ViewModes.Measure;
        line.enabled = show;
        text.gameObject.SetActive(show);
    }
}
