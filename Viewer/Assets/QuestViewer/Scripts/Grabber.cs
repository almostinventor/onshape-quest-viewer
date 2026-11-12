using UnityEngine;

/// Something that can hold a Grabbable: a hand (pinch or grip) or a controller.
public abstract class Grabber : MonoBehaviour
{
    /// Layer of the finger/controller colliders; excluded from grab searches.
    public const string HandsLayerName = "Hands";

    /// Pose the held object follows.
    public abstract Pose GrabPose { get; }
    public Grabbable Held { get; protected set; }
    /// When this grabber last let go of something (finger pushing waits a moment after a release).
    public float LastReleaseTime { get; private set; } = -10f;

    /// Called by Grabbable when it's taken away (respawned or replaced).
    internal virtual void Drop(Grabbable g)
    {
        if (Held != g) return;
        Held = null;
        LastReleaseTime = Time.time;
    }

    protected bool TryGrab(Vector3 at, float radius)
    {
        var target = FindTarget(at, radius);
        if (target == null) return false;
        Held = target;
        target.Grab(this);
        return true;
    }

    protected void ReleaseHeld()
    {
        if (Held == null) return;
        Held.Release(this);
        Held = null;
        LastReleaseTime = Time.time;
    }

    protected static Grabbable FindTarget(Vector3 p, float radius)
    {
        int mask = ~LayerMask.GetMask(HandsLayerName);
        Physics.SyncTransforms();
        Grabbable best = null;
        float bestDist = float.MaxValue;
        foreach (var col in Physics.OverlapSphere(p, radius, mask, QueryTriggerInteraction.Ignore))
        {
            var g = col.GetComponentInParent<Grabbable>();
            if (g == null || !col.enabled) continue;
            float d = Vector3.Distance(col.ClosestPoint(p), p);
            if (d < bestDist) { best = g; bestDist = d; }
        }
        return best;
    }
}
