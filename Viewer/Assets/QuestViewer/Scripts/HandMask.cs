using UnityEngine;
using UnityEngine.InputSystem;

/// Renders the hand meshes with the passthrough "hole" material, so your real hands are visible and correctly
/// cover virtual objects. PC keyboard: [ and ] make the mask 1 mm thinner / wider.
public class HandMask : MonoBehaviour
{
    public OVRMeshRenderer[] handRenderers;
    public Material holeMaterial;

    static readonly int InflateId = Shader.PropertyToID("_Inflate");
    SkinnedMeshRenderer[] meshes;

    void Start() => meshes = new SkinnedMeshRenderer[handRenderers.Length];

    void LateUpdate()
    {
        // OVRMeshRenderer creates its SkinnedMeshRenderer once hand data arrives, so apply the material every frame.
        for (int i = 0; i < handRenderers.Length; i++)
        {
            if (handRenderers[i] == null) continue;
            if (meshes[i] == null) meshes[i] = handRenderers[i].GetComponent<SkinnedMeshRenderer>();
            if (meshes[i] != null && meshes[i].sharedMaterial != holeMaterial) meshes[i].sharedMaterial = holeMaterial;
        }

        var kb = Keyboard.current;
        if (kb == null || holeMaterial == null) return;
        if (kb.leftBracketKey.wasPressedThisFrame || kb.rightBracketKey.wasPressedThisFrame)
        {
            float step = kb.rightBracketKey.wasPressedThisFrame ? 0.001f : -0.001f;
            float v = Mathf.Clamp(holeMaterial.GetFloat(InflateId) + step, -0.008f, 0.012f);
            holeMaterial.SetFloat(InflateId, v);
            Hud.Info($"Hand mask {v * 1000f:+0;-0;0} mm", 2f);
        }
    }
}
