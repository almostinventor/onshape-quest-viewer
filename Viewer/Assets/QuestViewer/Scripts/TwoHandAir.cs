using UnityEngine;

/// Pinch in empty air with both hands to scale the model in place, like Iron Man: hands apart = bigger,
/// together = smaller (locks to 100%), without grabbing it. Does nothing in FIXED scale mode.
public class TwoHandAir : MonoBehaviour
{
    public PinchGrabber left;
    public PinchGrabber right;
    [Tooltip("Scaled by the gesture: the model when one is loaded, otherwise the test cube.")]
    public Grabbable model;
    public ModelHost modelHost;
    public Grabbable placeholder;

    bool active;
    float startDist;
    Grabbable target;

    void LateUpdate()
    {
        bool both = left != null && right != null && left.IsPinching && right.IsPinching && !left.IsHolding && !right.IsHolding;
        float dist = both ? Vector3.Distance(left.PinchPose.position, right.PinchPose.position) : 0f;

        if (both && !active)
        {
            active = true;
            startDist = Mathf.Max(dist, 0.02f);
            target = modelHost != null && modelHost.HasModel ? model : placeholder != null && placeholder.gameObject.activeInHierarchy ? placeholder : null;
            if (target != null) target.BeginRemoteScale(); // ignored in FIXED scale mode
        }
        else if (!both && active)
        {
            active = false;
            if (target != null) target.EndRemoteScale();
            target = null;
        }

        if (active && target != null) target.UpdateRemoteScale(dist / startDist);
    }
}
