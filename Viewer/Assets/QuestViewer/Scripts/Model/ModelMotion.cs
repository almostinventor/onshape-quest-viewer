using UnityEngine;

/// Exploded view and turntable for the model.
///   Explode: parts slide outward from the centre (animated); new versions arrive exploded if the mode is on.
///   Turntable: slow rotation about the vertical axis while nobody holds the model.
[RequireComponent(typeof(ModelHost), typeof(Grabbable))]
public class ModelMotion : MonoBehaviour
{
    public float explodeSeconds = 0.6f;
    public float turntableDegreesPerSecond = 20f;

    ModelHost host;
    Grabbable grabbable;
    float explode;          // 0 = assembled, 1 = exploded (eased)
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
        ApplyExplode(); // place new parts at the current explosion amount
    }

    void Update()
    {
        float target = ViewModes.Explode ? 1f : 0f;
        if (!Mathf.Approximately(explode, target))
        {
            explode = Mathf.SmoothDamp(explode, target, ref explodeVelocity, explodeSeconds * 0.35f);
            if (Mathf.Abs(explode - target) < 0.001f) explode = target;
            ApplyExplode();
        }

        // The turntable keeps the model still otherwise (no drifting while it spins).
        grabbable.SetFrozen(ViewModes.Turntable || ViewModes.Inspect);
        if (ViewModes.Turntable && !grabbable.IsHeld && host.HasModel)
            transform.Rotate(Vector3.up, turntableDegreesPerSecond * Time.deltaTime, Space.World);
    }

    void ApplyExplode()
    {
        foreach (var p in host.Parts)
            if (p != null) p.transform.localPosition = p.Home + p.ExplodeOffset * explode;
    }
}
