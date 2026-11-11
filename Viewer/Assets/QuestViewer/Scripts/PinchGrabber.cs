using System;
using UnityEngine;

/// Hand interactions:
///   pinch an object            grab it precisely (with the other hand too: scale/rotate)
///   close your hand around it  grab it with a whole-hand grip
///   double-pinch an object     reset it to 100% scale
///   double-pinch in empty air  bring the model back in front of you
/// (Pushing with fingers is done by FingerColliders.)
[RequireComponent(typeof(TrackedHand))]
public class PinchGrabber : Grabber
{
    [Tooltip("How close (m) the pinch point must be to an object to grab it.")]
    public float grabRadius = 0.03f;
    [Tooltip("How close (m) the palm must be to an object for a whole-hand grip.")]
    public float gripRadius = 0.06f;
    [Tooltip("Max seconds between two pinches to count as a double-pinch.")]
    public float doublePinchTime = 0.4f;

    public static event Action AirDoublePinch;

    public TrackedHand Hand { get; private set; }
    /// Pinching (whether or not anything is held). Used by the tape measure.
    public bool IsPinching { get; private set; }
    public bool IsGripping { get; private set; }

    enum Mode { None, Pinch, Grip }
    Mode mode;
    float lastPinchTime = -10f;
    Grabbable lastTarget;

    public override Pose GrabPose => mode == Mode.Grip ? Hand.GripPose : Hand.PinchPose;
    public Pose PinchPose => Hand.PinchPose;

    void Awake() => Hand = GetComponent<TrackedHand>();

    void Update()
    {
        // While tracking is lost (e.g. hands occluding each other) keep the current state instead of dropping.
        if (!Hand.IsTracked) return;

        bool pinch = Hand.IsPinching;
        bool grip = Hand.IsGripping;
        bool pinchStarted = pinch && !IsPinching;
        bool gripStarted = grip && !IsGripping;
        IsPinching = pinch;
        IsGripping = grip;

        if (mode == Mode.None)
        {
            if (pinchStarted) OnPinchStart();
            else if (gripStarted && TryGrab(Hand.GripPose.position, gripRadius)) mode = Mode.Grip;
        }
        else if ((mode == Mode.Pinch && !pinch) || (mode == Mode.Grip && !grip))
        {
            ReleaseHeld();
            mode = Mode.None;
        }
    }

    void OnPinchStart()
    {
        var p = Hand.PinchPose.position;
        var target = FindTarget(p, grabRadius);
        bool isDouble = Time.time - lastPinchTime < doublePinchTime && target == lastTarget;
        lastPinchTime = Time.time;
        lastTarget = target;

        if (target != null)
        {
            Held = target;
            target.Grab(this);
            mode = Mode.Pinch;
            if (isDouble && !target.IsTrueSize) target.ResetScale();
        }
        else if (isDouble)
        {
            AirDoublePinch?.Invoke();
        }
    }

    internal override void Drop(Grabbable g)
    {
        base.Drop(g);
        if (Held == null) mode = Mode.None;
    }
}
