using UnityEngine;
using UnityEngine.XR.Hands;

/// INSPECT mode: touch a part with an index fingertip to highlight it and show its name, size and mass on the
/// keypad display. Touch the same part twice quickly to isolate it (the rest turns X-ray); again to un-isolate.
/// The model holds still while inspecting.
public class PartInspector : MonoBehaviour
{
    public TrackedHand left;
    public TrackedHand right;
    public ModelHost host;
    public ModelLook look;
    public float touchRadius = 0.006f;
    public float doubleTouchTime = 0.6f;

    ModelPart selected;
    float selectedAt = -10f;
    ModelPart lastTouchL, lastTouchR;
    bool wasOn;

    void Update()
    {
        if (!ViewModes.Inspect || host == null || !host.HasModel)
        {
            if (wasOn) Clear();
            wasOn = false;
            return;
        }
        wasOn = true;
        lastTouchL = Poke(left, lastTouchL);
        lastTouchR = Poke(right, lastTouchR);
    }

    ModelPart Poke(TrackedHand hand, ModelPart previous)
    {
        if (hand == null || !hand.TryGetJoint(XRHandJointID.IndexTip, out var tip)) return null;
        var touching = PartAt(tip.position);
        if (touching != null && touching != previous) OnTouch(touching);
        return touching;
    }

    ModelPart PartAt(Vector3 p)
    {
        int mask = ~LayerMask.GetMask(Grabber.HandsLayerName);
        ModelPart best = null;
        float bestDist = float.MaxValue;
        foreach (var col in Physics.OverlapSphere(p, touchRadius, mask, QueryTriggerInteraction.Ignore))
        {
            var part = col.GetComponent<ModelPart>();
            if (part == null || !part.transform.IsChildOf(host.transform)) continue;
            float d = Vector3.Distance(col.ClosestPoint(p), p);
            if (d < bestDist) { best = part; bestDist = d; }
        }
        return best;
    }

    void OnTouch(ModelPart part)
    {
        if (part == selected && Time.time - selectedAt < doubleTouchTime)
        {
            look.Isolate(look.Isolated == part.Renderer ? null : part.Renderer);
            selectedAt = -10f;
            return;
        }
        selected = part;
        selectedAt = Time.time;
        look.Highlight(part.Renderer);
        if (look.Isolated != null && look.Isolated != part.Renderer) look.Isolate(part.Renderer);
        Status.Info(part.Describe(), 10f);
    }

    void Clear()
    {
        selected = null;
        if (look != null)
        {
            look.Highlight(null);
            look.Isolate(null);
        }
    }
}
