using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

/// One tracked hand from Unity XR Hands. Detects an index-thumb pinch and a whole-hand grip,
/// and exposes every joint pose (for finger colliders).
public class TrackedHand : MonoBehaviour
{
    public Handedness handedness = Handedness.Left;
    [Tooltip("OVRCameraRig/TrackingSpace: joint poses are relative to this.")]
    public Transform trackingSpace;

    [Tooltip("Fingertip distance (m) that starts a pinch.")]
    public float pinchOn = 0.02f;
    [Tooltip("Fingertip distance (m) that ends a pinch.")]
    public float pinchOff = 0.035f;

    [Header("Smoothing (One Euro filter)")]
    [Tooltip("Smoothing when the hand is still (Hz). Lower = smoother but laggier.")]
    public float minCutoff = 1.5f;
    [Tooltip("How quickly smoothing relaxes as the hand moves faster.")]
    public float beta = 8f;

    [Tooltip("Average fingertip-to-palm distance (m) of middle, ring and little finger that starts a grip.")]
    public float gripOn = 0.055f;
    [Tooltip("...and that ends it.")]
    public float gripOff = 0.075f;

    public bool IsTracked { get; private set; }
    public bool IsPinching { get; private set; }
    public bool IsGripping { get; private set; }
    /// Thumb-index tip distance (m).
    public float PinchDistance { get; private set; } = 1f;
    /// Average middle/ring/little fingertip-to-palm distance (m).
    public float GripCurl { get; private set; } = 1f;
    /// Index fingertip-to-palm distance (m).
    public float IndexCurl { get; private set; } = 1f;
    /// The hand is closing for a pinch or grip (finger pushing pauses so grabbing doesn't knock things away).
    public bool IsClosing => PinchDistance < 0.05f || (GripCurl < 0.08f && IndexCurl < 0.085f);
    /// Midpoint between thumb and index tips, oriented like the palm. Keeps its last value while untracked.
    public Pose PinchPose { get; private set; }
    /// Palm centre, oriented like the palm.
    public Pose GripPose { get; private set; }

    static readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
    static readonly XRHandJointID[] CurlTips = { XRHandJointID.MiddleTip, XRHandJointID.RingTip, XRHandJointID.LittleTip };
    static bool originSet;
    readonly Pose[] joints = new Pose[XRHandJointID.EndMarker.ToIndex()];
    readonly bool[] jointValid = new bool[XRHandJointID.EndMarker.ToIndex()];

    /// World pose of a joint from the latest tracking update.
    public bool TryGetJoint(XRHandJointID id, out Pose pose)
    {
        int i = id.ToIndex();
        pose = joints[i];
        return IsTracked && jointValid[i];
    }

    void Update()
    {
        var hand = GetHand();
        IsTracked = hand.HasValue && hand.Value.isTracked;
        if (!IsTracked) return;

        var h = hand.Value;
        for (var id = XRHandJointID.BeginMarker; id < XRHandJointID.EndMarker; id++)
        {
            int i = id.ToIndex();
            jointValid[i] = h.GetJoint(id).TryGetPose(out var p);
            if (jointValid[i]) joints[i] = new Pose(trackingSpace.TransformPoint(p.position), trackingSpace.rotation * p.rotation);
        }

        if (h.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumb) &&
            h.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var index))
        {
            float d = Vector3.Distance(thumb.position, index.position);
            PinchDistance = d;
            IsPinching = IsPinching ? d < pinchOff : d < pinchOn;

            var rot = h.GetJoint(XRHandJointID.Palm).TryGetPose(out var palm) ? palm.rotation : index.rotation;
            PinchPose = Smooth(new Pose(trackingSpace.TransformPoint((thumb.position + index.position) * 0.5f), trackingSpace.rotation * rot));
        }

        if (h.GetJoint(XRHandJointID.Palm).TryGetPose(out var palmPose))
        {
            float curl = 0f;
            int n = 0;
            foreach (var tip in CurlTips)
            {
                if (!h.GetJoint(tip).TryGetPose(out var t)) continue;
                curl += Vector3.Distance(t.position, palmPose.position);
                n++;
            }
            if (n > 0)
            {
                curl /= n;
                GripCurl = curl;
                IsGripping = IsGripping ? curl < gripOff : curl < gripOn;
            }
            if (h.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var it))
                IndexCurl = Vector3.Distance(it.position, palmPose.position);
            GripPose = new Pose(trackingSpace.TransformPoint(palmPose.position), trackingSpace.rotation * palmPose.rotation);
        }
    }

    // One Euro filter: heavy smoothing for small, slow motion (jitter), little smoothing for fast motion (no lag).
    bool hasFiltered;
    Vector3 fPos, dPos;
    Quaternion fRot;
    float dAng;

    Pose Smooth(Pose raw)
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        if (!hasFiltered)
        {
            hasFiltered = true;
            fPos = raw.position;
            fRot = raw.rotation;
            dPos = Vector3.zero;
            dAng = 0f;
            return raw;
        }

        // Position
        var rawVel = (raw.position - fPos) / dt;
        dPos = Vector3.Lerp(dPos, rawVel, Alpha(1f, dt));
        fPos = Vector3.Lerp(fPos, raw.position, Alpha(minCutoff + beta * dPos.magnitude, dt));

        // Rotation (speed in radians/s drives the cutoff the same way)
        float angVel = Quaternion.Angle(fRot, raw.rotation) * Mathf.Deg2Rad / dt;
        dAng = Mathf.Lerp(dAng, angVel, Alpha(1f, dt));
        fRot = Quaternion.Slerp(fRot, raw.rotation, Alpha(minCutoff + 0.5f * dAng, dt));

        return new Pose(fPos, fRot);
    }

    static float Alpha(float cutoff, float dt)
    {
        float tau = 1f / (2f * Mathf.PI * cutoff);
        return 1f / (1f + tau / dt);
    }

    XRHand? GetHand()
    {
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var s in subsystems)
        {
            if (!s.running) continue;
            EnsureFloorOrigin();
            return handedness == Handedness.Left ? s.leftHand : s.rightHand;
        }
        return null;
    }

    /// The camera rig uses a floor-level origin; put Unity's XR input (used by XR Hands) on the same origin.
    static void EnsureFloorOrigin()
    {
        if (originSet) return;
        var inputs = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(inputs);
        foreach (var i in inputs)
            if (i.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor)) originSet = true;
    }
}
