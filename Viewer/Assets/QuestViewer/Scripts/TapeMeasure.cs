using UnityEngine;

/// Pinch in empty air with both hands to measure the distance between your pinches (in mm).
/// The last measurement stays visible for a few seconds after letting go.
public class TapeMeasure : MonoBehaviour
{
    public PinchGrabber left;
    public PinchGrabber right;
    public Transform head;
    public Material lineMaterial;
    public float lingerSeconds = 4f;

    LineRenderer line;
    TextMesh text;
    float lastActive = -100f;

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
        bool measuring = left != null && right != null &&
                         left.IsPinching && right.IsPinching && left.Held == null && right.Held == null;
        if (measuring)
        {
            lastActive = Time.time;
            var a = left.PinchPose.position;
            var b = right.PinchPose.position;
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            text.text = $"{Vector3.Distance(a, b) * 1000f:0} mm";

            var mid = (a + b) * 0.5f + Vector3.up * 0.015f;
            var toHead = head != null ? Vector3.ProjectOnPlane(mid - head.position, Vector3.up) : Vector3.forward;
            text.transform.SetPositionAndRotation(mid, Quaternion.LookRotation(toHead.sqrMagnitude > 1e-4f ? toHead : Vector3.forward));
        }

        bool show = Time.time - lastActive < lingerSeconds;
        line.enabled = show;
        text.gameObject.SetActive(show);
    }
}
