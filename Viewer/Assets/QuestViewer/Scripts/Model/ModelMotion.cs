using UnityEngine;

/// Exploded view: parts slide outward from the centre. In exploded view you can pinch a single part and pull it
/// out; when you let go it glides back to its place. Every part eases towards its target each frame, so
/// exploding, collapsing and returning are all smooth.
[RequireComponent(typeof(ModelHost), typeof(Grabbable))]
public class ModelMotion : MonoBehaviour
{
    public float explodeSeconds = 0.6f;
    [Tooltip("How quickly parts glide to their place (1/seconds).")]
    public float settleSpeed = 9f;

    ModelHost host;
    Grabbable grabbable;
    float explode;
    float explodeVelocity;

    void Awake()
    {
        host = GetComponent<ModelHost>();
        grabbable = GetComponent<Grabbable>();
    }

    void OnEnable() => ModelHost.ModelLoaded += OnLoaded;
    void OnDisable() => ModelHost.ModelLoaded -= OnLoaded;

    void OnLoaded(ModelHost h)
    {
        if (h != host) return;
        // New versions appear at the current explosion amount, without gliding in.
        foreach (var p in host.Parts)
            if (p != null) p.transform.localPosition = p.Home + p.ExplodeOffset * explode;
    }

    void Update()
    {
        explode = Mathf.SmoothDamp(explode, ViewModes.Explode ? 1f : 0f, ref explodeVelocity, explodeSeconds * 0.35f);

        bool dragging = false;
        float k = 1f - Mathf.Exp(-settleSpeed * Time.deltaTime);
        foreach (var p in host.Parts)
        {
            if (p == null) continue;
            if (p.Dragger != null) { dragging = true; continue; }
            var t = p.transform;
            t.localPosition = Vector3.Lerp(t.localPosition, p.Home + p.ExplodeOffset * explode, k);
            t.localRotation = Quaternion.Slerp(t.localRotation, p.HomeRotation, k);
        }

        // Hold the model still while inspecting or while a part is being pulled out.
        grabbable.SetFrozen(ViewModes.Inspect || dragging);
    }
}
