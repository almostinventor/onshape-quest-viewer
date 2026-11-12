using UnityEngine;

/// Something that can hold a Grabbable: a hand (pinch or grip) or a controller.
/// In exploded view it can also pull out a single part (which glides back when released).
public abstract class Grabber : MonoBehaviour
{
    /// Layer of the finger/controller colliders; excluded from grab searches.
    public const string HandsLayerName = "Hands";

    /// Pose the held object follows.
    public abstract Pose GrabPose { get; }
    public Grabbable Held { get; protected set; }
    /// The single part being pulled out in exploded view, or null.
    public ModelPart DraggedPart { get; private set; }
    /// Holding anything (a whole object or a part).
    public bool IsHolding => Held != null || DraggedPart != null;
    /// When this grabber last let go of something (finger pushing waits a moment after a release).
    public float LastReleaseTime { get; private set; } = -10f;

    Vector3 partOffPos;
    Quaternion partOffRot;

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

    /// Exploded view only: start pulling out the part at 'at'.
    protected bool TryDragPart(Vector3 at, float radius)
    {
        if (!ViewModes.Explode) return false;
        var part = FindPart(at, radius);
        if (part == null || part.Dragger != null) return false;
        DraggedPart = part;
        part.Dragger = this;
        var p = GrabPose;
        var inv = Quaternion.Inverse(p.rotation);
        partOffPos = inv * (part.transform.position - p.position);
        partOffRot = inv * part.transform.rotation;
        return true;
    }

    protected void ReleasePart()
    {
        if (DraggedPart == null) return;
        if (DraggedPart.Dragger == this) DraggedPart.Dragger = null;
        DraggedPart = null;
        LastReleaseTime = Time.time;
    }

    protected virtual void LateUpdate()
    {
        if (DraggedPart == null) return;
        if (DraggedPart.Dragger != this) { DraggedPart = null; return; } // e.g. the model was replaced
        var p = GrabPose;
        DraggedPart.transform.SetPositionAndRotation(p.position + p.rotation * partOffPos, p.rotation * partOffRot);
    }

    protected static Grabbable FindTarget(Vector3 p, float radius)
    {
        Physics.SyncTransforms();
        Grabbable best = null;
        float bestDist = float.MaxValue;
        foreach (var col in Physics.OverlapSphere(p, radius, Mask, QueryTriggerInteraction.Ignore))
        {
            var g = col.GetComponentInParent<Grabbable>();
            if (g == null || !col.enabled) continue;
            float d = Vector3.Distance(col.ClosestPoint(p), p);
            if (d < bestDist) { best = g; bestDist = d; }
        }
        return best;
    }

    protected static ModelPart FindPart(Vector3 p, float radius)
    {
        Physics.SyncTransforms();
        ModelPart best = null;
        float bestDist = float.MaxValue;
        foreach (var col in Physics.OverlapSphere(p, radius, Mask, QueryTriggerInteraction.Ignore))
        {
            var part = col.GetComponent<ModelPart>();
            if (part == null || !col.enabled) continue;
            float d = Vector3.Distance(col.ClosestPoint(p), p);
            if (d < bestDist) { best = part; bestDist = d; }
        }
        return best;
    }

    static int Mask => ~LayerMask.GetMask(HandsLayerName);
}
