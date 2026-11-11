using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// Places this object in front of the head (default 40 cm ahead, 10 cm below the eyes), facing the user.
/// Respawns when the headset is put back on (unless already handled) and, if recallable, on a double-pinch in
/// empty air, the A/X controller button or R on the PC keyboard.
public class SpawnInFront : MonoBehaviour
{
    public Transform head;
    public float distance = 0.40f;
    public float belowEyes = 0.10f;
    [Tooltip("Positive = to the right.")]
    public float sideways = 0f;
    [Tooltip("Tilt the object back by this many degrees (e.g. a keypad lying like a desk panel).")]
    public float tiltDegrees = 0f;
    public bool recallable = true;

    Grabbable grabbable;

    void Start()
    {
        grabbable = GetComponent<Grabbable>();
        if (head == null && Camera.main != null) head = Camera.main.transform;
        StartCoroutine(SpawnWhenTracked());
    }

    void OnEnable()
    {
        OVRManager.HMDMounted += OnMounted;
        if (!recallable) return;
        PinchGrabber.AirDoublePinch += Respawn;
        ControllerGrabber.RecallPressed += Respawn;
    }

    void OnDisable()
    {
        OVRManager.HMDMounted -= OnMounted;
        PinchGrabber.AirDoublePinch -= Respawn;
        ControllerGrabber.RecallPressed -= Respawn;
    }

    /// The head sits at the tracking origin (floor) until tracking data arrives, so wait for it.
    IEnumerator SpawnWhenTracked()
    {
        float until = Time.realtimeSinceStartup + 10f;
        while (head != null && head.localPosition.y < 0.3f && Time.realtimeSinceStartup < until) yield return null;
        yield return null;
        Respawn();
    }

    void OnMounted()
    {
        if (grabbable == null || !grabbable.HasBeenHeld) StartCoroutine(SpawnWhenTracked());
    }

    void Update()
    {
        if (recallable && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Respawn();
    }

    public void Respawn()
    {
        if (head == null || !isActiveAndEnabled) return;
        if (grabbable != null) grabbable.ReleaseAll();

        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        forward.Normalize();

        var right = Vector3.Cross(Vector3.up, forward);
        transform.position = head.position + forward * distance + right * sideways + Vector3.down * belowEyes;
        transform.rotation = Quaternion.LookRotation(-forward, Vector3.up) * Quaternion.Euler(-tiltDegrees, 0f, 0f);
        if (grabbable != null) grabbable.Stop();
    }
}
