using UnityEngine;
using UnityEngine.Rendering;

/// Even lighting for models over passthrough: a soft headlight that comes from roughly where you are
/// (so the side you look at is never black) plus uniform ambient light.
/// Flat mode: ambient only, every surface evenly lit with no shading.
public class SceneLighting : MonoBehaviour
{
    public Light keyLight;
    public Transform head;
    [Range(0, 2)] public float shadedAmbient = 0.55f;
    [Range(0, 2)] public float shadedKey = 0.75f;
    [Range(0, 2)] public float flatAmbient = 1.15f;

    void OnEnable()
    {
        ViewModes.Changed += Apply;
        Apply();
    }

    void OnDisable() => ViewModes.Changed -= Apply;

    void Apply()
    {
        bool flat = ViewModes.Lighting == LightingMode.Flat;
        SetAmbient(Color.white * (flat ? flatAmbient : shadedAmbient));
        if (keyLight != null)
        {
            keyLight.intensity = flat ? 0f : shadedKey;
            keyLight.enabled = !flat;
        }
    }

    static void SetAmbient(Color c)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = c;
        var sh = new SphericalHarmonicsL2();
        sh.AddAmbientLight(c);
        RenderSettings.ambientProbe = sh;
    }

    void LateUpdate()
    {
        // Light from slightly above and to the right of your view direction.
        if (keyLight != null && head != null)
            keyLight.transform.rotation = Quaternion.LookRotation(head.forward, Vector3.up) * Quaternion.Euler(25f, -20f, 0f);
    }
}
