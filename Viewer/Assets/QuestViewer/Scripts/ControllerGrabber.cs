using System;
using UnityEngine;

/// Touch controller interactions (on the controller anchor):
///   grip              grab (with the other controller or a hand: scale/rotate)
///   A / X             bring the model back in front of you
///   B / Y             exploded view on/off
///   thumbstick click  toggle hologram mode
/// The controller tip also pushes floating objects and presses keypad buttons.
public class ControllerGrabber : Grabber
{
    public OVRInput.Controller controller = OVRInput.Controller.LTouch;
    public float grabRadius = 0.06f;
    [Tooltip("Grab point relative to the controller anchor.")]
    public Vector3 grabOffset = new Vector3(0f, -0.01f, 0.02f);
    [Tooltip("Push/press point relative to the controller anchor.")]
    public Vector3 tipOffset = new Vector3(0f, -0.02f, 0.06f);

    public static event Action RecallPressed;

    bool gripping;
    Rigidbody tip;
    Collider tipCollider;
    GameObject tipPoker;

    public override Pose GrabPose => new Pose(transform.TransformPoint(grabOffset), transform.rotation);
    bool Connected => OVRInput.IsControllerConnected(controller) && (OVRInput.GetActiveController() & controller) != 0;

    void Awake()
    {
        int layer = LayerMask.NameToLayer(HandsLayerName);
        var go = new GameObject(name + "Tip");
        if (layer >= 0) go.layer = layer;
        tipCollider = go.AddComponent<SphereCollider>();
        ((SphereCollider)tipCollider).radius = 0.015f;
        tip = go.AddComponent<Rigidbody>();
        tip.isKinematic = true;
        tip.useGravity = false;
        tip.interpolation = RigidbodyInterpolation.Interpolate;

        tipPoker = new GameObject("Poker");
        tipPoker.transform.SetParent(go.transform, false);
        if (layer >= 0) tipPoker.layer = layer;
        var t = tipPoker.AddComponent<SphereCollider>();
        t.isTrigger = true;
        t.radius = 0.012f;
        tipPoker.AddComponent<Poker>();
    }

    void Update()
    {
        bool connected = Connected;
        tipCollider.enabled = connected && !gripping;
        tipPoker.SetActive(connected);
        if (!connected)
        {
            if (gripping) { gripping = false; ReleaseHeld(); }
            return;
        }

        float g = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controller);
        if (!gripping && g > 0.55f)
        {
            gripping = true;
            TryGrab(GrabPose.position, grabRadius);
        }
        else if (gripping && g < 0.35f)
        {
            gripping = false;
            ReleaseHeld();
        }

        if (OVRInput.GetDown(OVRInput.Button.One, controller)) RecallPressed?.Invoke();
        if (OVRInput.GetDown(OVRInput.Button.Two, controller)) ViewModes.ToggleExplode();
        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, controller)) ViewModes.ToggleHologram();
    }

    void FixedUpdate()
    {
        tip.MovePosition(transform.TransformPoint(tipOffset));
        tip.MoveRotation(transform.rotation);
    }

    void OnDestroy()
    {
        if (tip != null) Destroy(tip.gameObject);
    }
}
