using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// Point your index finger up (other fingers curled) and draw a circle with it to start the turntable,
/// spinning the way you swirled. Swirl again to stop it.
public class SwirlDetector : MonoBehaviour
{
    public TrackedHand left;
    public TrackedHand right;
    [Tooltip("Full turn needed, in degrees.")]
    public float turnDegrees = 330f;
    [Tooltip("Minimum circle radius (m).")]
    public float minRadius = 0.015f;
    public float windowSeconds = 2.5f;
    public float cooldown = 1.5f;

    struct Sample { public float t; public Vector3 p; }

    readonly Queue<Sample> leftPath = new Queue<Sample>();
    readonly Queue<Sample> rightPath = new Queue<Sample>();
    float lastTrigger = -10f;

    void Update()
    {
        Track(left, leftPath);
        Track(right, rightPath);
    }

    void Track(TrackedHand hand, Queue<Sample> path)
    {
        if (hand == null || !IsPointingUp(hand, out var tip))
        {
            path.Clear();
            return;
        }

        float now = Time.time;
        path.Enqueue(new Sample { t = now, p = tip });
        while (path.Count > 0 && now - path.Peek().t > windowSeconds) path.Dequeue();
        if (path.Count < 12 || now - lastTrigger < cooldown) return;

        // Centre of the recent path, in the horizontal plane.
        var centre = Vector3.zero;
        foreach (var s in path) centre += s.p;
        centre /= path.Count;

        float total = 0f, radius = 0f;
        Vector3? prev = null;
        foreach (var s in path)
        {
            var d = Vector3.ProjectOnPlane(s.p - centre, Vector3.up);
            radius += d.magnitude;
            if (prev.HasValue) total += Vector3.SignedAngle(prev.Value, d, Vector3.up);
            prev = d;
        }
        radius /= path.Count;

        if (Mathf.Abs(total) >= turnDegrees && radius >= minRadius)
        {
            lastTrigger = now;
            path.Clear();
            ViewModes.SetTurntable(!ViewModes.Turntable, Mathf.Sign(total));
            Status.Info(ViewModes.Turntable ? "Rotate on" : "Rotate off", 1.5f);
        }
    }

    /// Index finger extended and pointing up, middle/ring/little fingers curled.
    static bool IsPointingUp(TrackedHand hand, out Vector3 tip)
    {
        tip = default;
        if (!hand.IsTracked || hand.IndexCurl < 0.09f || hand.GripCurl > 0.075f) return false;
        if (!hand.TryGetJoint(XRHandJointID.IndexTip, out var t) || !hand.TryGetJoint(XRHandJointID.IndexProximal, out var b)) return false;
        var dir = t.position - b.position;
        if (dir.magnitude < 0.04f || Vector3.Dot(dir.normalized, Vector3.up) < 0.6f) return false;
        tip = t.position;
        return true;
    }
}
