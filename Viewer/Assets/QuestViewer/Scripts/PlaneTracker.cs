using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

/// Keeps track of your room's horizontal surfaces (tables, desks, floor) from the Quest's room setup.
/// Standalone Quest only; does nothing where plane detection isn't available.
public class PlaneTracker : MonoBehaviour
{
    [Tooltip("OVRCameraRig/TrackingSpace: plane poses are relative to this.")]
    public Transform trackingSpace;

    static PlaneTracker instance;
    static readonly List<XRPlaneSubsystem> subsystems = new List<XRPlaneSubsystem>();
    readonly Dictionary<TrackableId, BoundedPlane> planes = new Dictionary<TrackableId, BoundedPlane>();
    XRPlaneSubsystem subsystem;

    void Awake() => instance = this;

    void Update()
    {
        if (subsystem == null)
        {
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count == 0) return;
            subsystem = subsystems[0];
            try
            {
                subsystem.requestedPlaneDetectionMode = PlaneDetectionMode.Horizontal;
                if (!subsystem.running) subsystem.Start();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[PlaneTracker] Plane detection unavailable: " + e.Message);
                enabled = false;
                return;
            }
        }
        if (!subsystem.running) return;

        using (var changes = subsystem.GetChanges(Allocator.Temp))
        {
            foreach (var p in changes.added) planes[p.trackableId] = p;
            foreach (var p in changes.updated) planes[p.trackableId] = p;
            foreach (var id in changes.removed) planes.Remove(id);
        }
    }

    /// Highest upward-facing surface under 'point' within 'maxDrop' metres. Returns its world height.
    public static bool TryFindSurfaceBelow(Vector3 point, float maxDrop, out float surfaceY)
    {
        surfaceY = float.NegativeInfinity;
        if (instance == null || instance.planes.Count == 0) return false;
        bool found = false;
        foreach (var plane in instance.planes.Values)
        {
            if (plane.alignment != PlaneAlignment.HorizontalUp) continue;
            var pose = instance.trackingSpace != null ? TransformPose(instance.trackingSpace, plane.pose) : plane.pose;
            var local = Quaternion.Inverse(pose.rotation) * (point - pose.position);
            var ext = plane.extents;
            if (Mathf.Abs(local.x) > ext.x + 0.02f || Mathf.Abs(local.z) > ext.y + 0.02f) continue;
            float h = pose.position.y;
            float drop = point.y - h;
            if (drop < -0.01f || drop > maxDrop || h <= surfaceY) continue;
            surfaceY = h;
            found = true;
        }
        return found;
    }

    static Pose TransformPose(Transform t, Pose p) => new Pose(t.TransformPoint(p.position), t.rotation * p.rotation);
}
