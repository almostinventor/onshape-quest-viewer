using Meta.XR.EnvironmentDepth;
using UnityEngine;

/// Real objects in front of the model hide it (Meta Depth API, standalone Quest only).
/// Requests the spatial data permission; the model shaders read the depth when it's available.
public class DepthOcclusion : MonoBehaviour
{
    public EnvironmentDepthManager depthManager;

    void Start()
    {
        if (Application.platform != RuntimePlatform.Android || !EnvironmentDepthManager.IsSupported)
        {
            if (depthManager != null) depthManager.enabled = false;
            return;
        }
        if (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.Scene))
            OVRPermissionsRequester.Request(new[] { OVRPermissionsRequester.Permission.Scene });
    }
}
