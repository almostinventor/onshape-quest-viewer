using UnityEngine;
using UnityEngine.XR.Hands;

/// Physics colliders on the finger joints and palm, so an open hand or pointing finger pushes floating objects.
/// Pushing pauses while the hand closes for a pinch or grip, while it holds something, and briefly after a release.
/// The index tip also presses keypad buttons.
[RequireComponent(typeof(TrackedHand))]
public class FingerColliders : MonoBehaviour
{
    public float jointRadius = 0.0085f;
    public float palmRadius = 0.022f;

    static readonly XRHandJointID[] Joints =
    {
        XRHandJointID.ThumbProximal, XRHandJointID.ThumbDistal, XRHandJointID.ThumbTip,
        XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip,
        XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip,
        XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip,
        XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip,
        XRHandJointID.Palm,
    };

    [Tooltip("Seconds after letting go before this hand pushes again.")]
    public float releaseGrace = 0.4f;

    TrackedHand hand;
    Grabber grabber;
    Rigidbody[] bodies;
    Collider[] colliders;
    GameObject poker;

    void Awake()
    {
        hand = GetComponent<TrackedHand>();
        grabber = GetComponent<Grabber>();
        int layer = LayerMask.NameToLayer(Grabber.HandsLayerName);
        if (layer >= 0) Physics.IgnoreLayerCollision(layer, layer);
        Time.fixedDeltaTime = 1f / 72f; // physics in step with the display for smooth pushing

        bodies = new Rigidbody[Joints.Length];
        colliders = new Collider[Joints.Length];
        for (int i = 0; i < Joints.Length; i++)
        {
            var go = new GameObject("Push_" + Joints[i]);
            if (layer >= 0) go.layer = layer;
            var col = go.AddComponent<SphereCollider>();
            col.radius = Joints[i] == XRHandJointID.Palm ? palmRadius : jointRadius;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            bodies[i] = rb;
            colliders[i] = col;

            if (Joints[i] == XRHandJointID.IndexTip)
            {
                // Separate trigger for pressing buttons; stays active even while pushing is paused.
                poker = new GameObject("Poker");
                poker.transform.SetParent(go.transform, false);
                if (layer >= 0) poker.layer = layer;
                var t = poker.AddComponent<SphereCollider>();
                t.isTrigger = true;
                t.radius = 0.008f;
                poker.AddComponent<Poker>();
            }
        }
    }

    void FixedUpdate()
    {
        bool tracked = hand.IsTracked;
        // A pinch or grip always wins: no pushing while this hand holds something, or just after letting go.
        bool holding = grabber != null && (grabber.IsHolding || Time.time - grabber.LastReleaseTime < releaseGrace);
        bool push = tracked && !hand.IsClosing && !holding;
        for (int i = 0; i < Joints.Length; i++)
        {
            colliders[i].enabled = push;
            if (!tracked || !hand.TryGetJoint(Joints[i], out var p)) continue;
            bodies[i].MovePosition(p.position);
            bodies[i].MoveRotation(p.rotation);
        }
        if (poker != null) poker.SetActive(tracked);
    }

    void OnDestroy()
    {
        if (bodies == null) return;
        foreach (var b in bodies) if (b != null) Destroy(b.gameObject);
    }
}

/// Marks a collider that can press keypad buttons (index fingertips, controller tips).
public class Poker : MonoBehaviour { }
