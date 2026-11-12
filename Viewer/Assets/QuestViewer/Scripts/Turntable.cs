using UnityEngine;

/// ROTATE mode: the object spins slowly about the vertical axis. It's driven through physics, so you can still push
/// it while it spins (it settles back into the spin), and grabbing it pauses the spin. Starts and stops smoothly.
[RequireComponent(typeof(Rigidbody), typeof(Grabbable))]
public class Turntable : MonoBehaviour
{
    public float degreesPerSecond = 20f;
    [Tooltip("Seconds to reach full speed / to stop.")]
    public float ramp = 0.8f;
    [Tooltip("How firmly the spin is held against pushes (higher = returns to the spin faster).")]
    public float stiffness = 4f;

    Rigidbody body;
    Grabbable grabbable;
    float speed;        // current spin speed (rad/s), eased
    float speedVelocity;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();
    }

    void FixedUpdate()
    {
        bool spinning = ViewModes.Turntable && !grabbable.IsHeld && !body.isKinematic;
        float target = spinning ? degreesPerSecond * Mathf.Deg2Rad * ViewModes.TurntableDirection : 0f;
        speed = Mathf.SmoothDamp(speed, target, ref speedVelocity, ramp * 0.4f, Mathf.Infinity, Time.fixedDeltaTime);
        if (body.isKinematic || (!spinning && Mathf.Abs(speed) < 1e-3f)) return;

        // Steer the vertical spin towards the turntable speed; pushes on other axes damp out as usual.
        var w = body.angularVelocity;
        float k = 1f - Mathf.Exp(-stiffness * Time.fixedDeltaTime);
        // The rigidbody's damping slows the spin each step; compensate so the spin holds its speed.
        float wanted = speed * (1f + body.angularDamping * Time.fixedDeltaTime);
        w.y = Mathf.Lerp(w.y, wanted, k * 4f);
        body.angularVelocity = w;
    }
}
