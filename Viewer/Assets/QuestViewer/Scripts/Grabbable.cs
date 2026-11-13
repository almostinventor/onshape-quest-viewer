using System;
using System.Collections.Generic;
using UnityEngine;

/// Grabbable object (needs a Collider on itself or a child).
/// One holder: follows it. Two holders: scale with their distance (locks to 100%), rotate and move with both.
/// Remote scaling (two-hand air pinch) scales it in place without grabbing.
/// In FIXED scale mode (ViewModes.FixedScale) nothing scales it; two hands only move and rotate it.
/// With a Rigidbody it floats freely: fingers can push it, and it keeps a little of your throw when let go.
public class Grabbable : MonoBehaviour
{
    public bool allowScale = true;
    public float minScale = 0.05f;
    public float maxScale = 20f;
    [Tooltip("Within this fraction of 100% the scale locks to exactly 100%.")]
    public float snapIn = 0.05f;
    [Tooltip("The lock releases only beyond this fraction, so it doesn't flicker.")]
    public float snapOut = 0.12f;
    [Tooltip("How much of your hand speed is kept when you let go.")]
    public float throwScale = 0.5f;

    /// Raised when scaling locks onto (true) or leaves (false) exactly 100%.
    public static event Action<Grabbable, bool> SnapChanged;
    /// Raised when the last holder lets go.
    public static event Action<Grabbable> Released;

    bool CanScale => allowScale && !ViewModes.FixedScale;

    public bool HasBeenHeld { get; private set; }
    public bool IsHeld => holders.Count > 0;
    public bool IsScaling => holders.Count == 2 || remoteScaling;
    public float Scale { get; private set; } = 1f;
    public bool IsTrueSize => Mathf.Approximately(Scale, 1f);

    readonly List<Grabber> holders = new List<Grabber>(2);
    Vector3 baseScale;
    Rigidbody body;
    bool snapped = true;
    bool frozen;
    bool remoteScaling;
    float remoteStartScale;

    // One holder: pose relative to its grab pose.
    Vector3 offPos;
    Quaternion offRot;

    // Two holders: pose relative to the frame between both, captured when the second one joined.
    Vector3 relPos;
    Quaternion relRot;
    float startDist, startScale;

    // Velocity tracking for throws.
    Vector3 lastPos, velocity, angularVelocity;
    Quaternion lastRot;

    void Awake()
    {
        baseScale = transform.localScale;
        body = GetComponent<Rigidbody>();
    }

    public void Grab(Grabber hand)
    {
        if (holders.Contains(hand) || holders.Count >= 2) return;
        StopGrowIn();
        holders.Add(hand);
        HasBeenHeld = true;
        velocity = angularVelocity = Vector3.zero;
        lastPos = transform.position;
        lastRot = transform.rotation;
        UpdateKinematic(false);
        Rebase();
    }

    public void Release(Grabber hand)
    {
        if (!holders.Remove(hand)) return;
        if (holders.Count == 0)
        {
            UpdateKinematic(true);
            Released?.Invoke(this);
        }
        Rebase();
    }

    public void ReleaseAll()
    {
        foreach (var h in holders.ToArray()) h.Drop(this);
        holders.Clear();
        UpdateKinematic(false);
        Stop();
    }

    /// Stops any drifting or spinning.
    public void Stop()
    {
        if (body == null) return;
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        Physics.SyncTransforms();
    }

    /// Frozen objects ignore pushes (e.g. while inspecting or on the turntable).
    public void SetFrozen(bool value)
    {
        if (frozen == value) return;
        frozen = value;
        UpdateKinematic(false);
    }

    void UpdateKinematic(bool throwOnRelease)
    {
        if (body == null) return;
        bool kinematic = IsHeld || frozen || remoteScaling || growing != null;
        if (body.isKinematic == kinematic) return;
        body.isKinematic = kinematic;
        if (!kinematic)
        {
            body.linearVelocity = throwOnRelease ? velocity * throwScale : Vector3.zero;
            body.angularVelocity = throwOnRelease ? angularVelocity * throwScale : Vector3.zero;
        }
    }

    public void ResetScale()
    {
        StopGrowIn();
        SetScale(1f);
        SetSnapped(true);
        Rebase();
    }

    // ---- Grow-in animation (recentring): grows from almost nothing to exactly 100% ----

    Coroutine growing;

    public void GrowIn(float seconds)
    {
        if (!allowScale) return;
        ResetScale();
        growing = StartCoroutine(Grow(seconds));
        UpdateKinematic(false); // fingers don't push it around while it grows
    }

    System.Collections.IEnumerator Grow(float seconds)
    {
        const float from = 0.03f;
        float t0 = Time.time;
        while (Time.time - t0 < seconds)
        {
            float u = (Time.time - t0) / seconds;
            float eased = 1f - Mathf.Pow(1f - u, 3f); // fast at first, settling gently into 100%
            Scale = Mathf.Lerp(from, 1f, eased);
            transform.localScale = baseScale * Scale;
            yield return null;
        }
        growing = null;
        SetScale(1f);
        UpdateKinematic(false);
    }

    void OnDisable() => StopGrowIn();

    /// Ends the animation at 100% (e.g. when grabbed mid-way).
    void StopGrowIn()
    {
        if (growing == null) return;
        StopCoroutine(growing);
        growing = null;
        SetScale(1f);
        UpdateKinematic(false);
    }

    // ---- Remote (two-hand air pinch) scaling, scales in place about the object's centre ----

    public void BeginRemoteScale()
    {
        if (!CanScale || IsHeld) return;
        StopGrowIn();
        remoteScaling = true;
        remoteStartScale = Scale;
        UpdateKinematic(false);
    }

    /// ratio = current hand distance / distance when the gesture started.
    public void UpdateRemoteScale(float ratio)
    {
        if (!remoteScaling) return;
        SetScale(Snap(Mathf.Clamp(remoteStartScale * ratio, minScale, maxScale)));
    }

    public void EndRemoteScale()
    {
        if (!remoteScaling) return;
        remoteScaling = false;
        UpdateKinematic(false);
    }

    float Snap(float s)
    {
        float off = Mathf.Abs(s - 1f);
        SetSnapped(snapped ? off < snapOut : off < snapIn);
        return snapped ? 1f : s;
    }

    void SetSnapped(bool value)
    {
        if (snapped == value) return;
        snapped = value;
        SnapChanged?.Invoke(this, value);
    }

    void SetScale(float s)
    {
        Scale = s;
        transform.localScale = baseScale * s;
    }

    void Rebase()
    {
        if (holders.Count == 1)
        {
            var p = holders[0].GrabPose;
            var inv = Quaternion.Inverse(p.rotation);
            offPos = inv * (transform.position - p.position);
            offRot = inv * transform.rotation;
        }
        else if (holders.Count == 2)
        {
            Frame(out var pos, out var rot, out startDist);
            startDist = Mathf.Max(startDist, 0.01f);
            startScale = Scale;
            var inv = Quaternion.Inverse(rot);
            relPos = inv * (transform.position - pos);
            relRot = inv * transform.rotation;
        }
    }

    void Frame(out Vector3 pos, out Quaternion rot, out float dist)
    {
        var a = holders[0].GrabPose.position;
        var b = holders[1].GrabPose.position;
        var dir = b - a;
        dist = dir.magnitude;
        pos = (a + b) * 0.5f;
        var d = dist > 1e-4f ? dir / dist : Vector3.right;
        var up = Mathf.Abs(Vector3.Dot(d, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        rot = Quaternion.LookRotation(d, up);
    }

    void LateUpdate()
    {
        if (holders.Count == 1)
        {
            var p = holders[0].GrabPose;
            transform.SetPositionAndRotation(p.position + p.rotation * offPos, p.rotation * offRot);
        }
        else if (holders.Count == 2)
        {
            Frame(out var pos, out var rot, out var dist);
            float s = startScale;
            if (CanScale)
            {
                s = Snap(Mathf.Clamp(startScale * dist / startDist, minScale, maxScale));
                SetScale(s);
            }
            transform.SetPositionAndRotation(pos + rot * (relPos * (s / startScale)), rot * relRot);
        }

        if (IsHeld) TrackVelocity();
    }

    void TrackVelocity()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        var v = (transform.position - lastPos) / dt;
        (transform.rotation * Quaternion.Inverse(lastRot)).ToAngleAxis(out float angle, out var axis);
        if (angle > 180f) angle -= 360f;
        var w = float.IsNaN(axis.x) || float.IsInfinity(axis.x) ? Vector3.zero : axis * (angle * Mathf.Deg2Rad / dt);
        velocity = Vector3.Lerp(velocity, v, 0.5f);
        angularVelocity = Vector3.Lerp(angularVelocity, w, 0.5f);
        lastPos = transform.position;
        lastRot = transform.rotation;
    }
}
