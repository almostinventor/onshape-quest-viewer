using System;
using UnityEngine;

/// Clap your hands to toggle the exploded view: palms meet quickly while facing each other.
public class ClapDetector : MonoBehaviour
{
    public TrackedHand left;
    public TrackedHand right;
    [Tooltip("Palm distance (m) that counts as a clap.")]
    public float contactDistance = 0.08f;
    [Tooltip("Minimum closing speed (m/s) just before contact.")]
    public float minSpeed = 0.5f;
    public float cooldown = 1.2f;

    public static event Action Clapped;

    float lastDist = 1f;
    float closingSpeed;
    float lastClap = -10f;

    void Update()
    {
        if (left == null || right == null || !left.IsTracked || !right.IsTracked) { lastDist = 1f; return; }

        var l = left.GripPose;
        var r = right.GripPose;
        float d = Vector3.Distance(l.position, r.position);
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        closingSpeed = Mathf.Lerp(closingSpeed, (lastDist - d) / dt, 0.5f);

        // Palms facing each other: their palm normals point in opposite directions.
        bool facing = Vector3.Dot(l.rotation * Vector3.up, r.rotation * Vector3.up) < -0.4f;
        bool open = !left.IsGripping && !right.IsGripping && !left.IsPinching && !right.IsPinching;

        if (d < contactDistance && lastDist >= contactDistance && closingSpeed > minSpeed && facing && open
            && Time.time - lastClap > cooldown)
        {
            lastClap = Time.time;
            Clapped?.Invoke();
            ViewModes.ToggleExplode();
        }
        lastDist = d;
    }
}
