using UnityEngine;

/// Helpers for HUD elements that float at a fixed angle in your view and follow the head smoothly.
public static class HeadLocked
{
    /// Pose 'upDegrees' above (and 'rightDegrees' right of) the view centre at 'distance' metres, facing the viewer.
    public static Pose Target(Transform head, float upDegrees, float distance, float rightDegrees = 0f)
    {
        var dir = head.rotation * Quaternion.Euler(-upDegrees, rightDegrees, 0f) * Vector3.forward;
        var pos = head.position + dir * distance;
        return new Pose(pos, Quaternion.LookRotation(dir, head.up));
    }

    /// Moves 't' towards the target with exponential smoothing ('speed' ~ 1/seconds).
    public static void Follow(Transform t, Pose target, float speed, bool snap = false)
    {
        float k = snap ? 1f : 1f - Mathf.Exp(-speed * Time.deltaTime);
        t.SetPositionAndRotation(Vector3.Lerp(t.position, target.position, k), Quaternion.Slerp(t.rotation, target.rotation, k));
    }

    static Material textMaterial;
    static Material lineMaterial;

    /// Always-on-top text for the HUD.
    public static TextMesh CreateText(string name, float characterSize, TextAnchor anchor, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMesh>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        go.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial(text.font);
        text.anchor = anchor;
        text.alignment = TextAlignment.Center;
        text.fontSize = 64;
        text.characterSize = characterSize;
        return text;
    }

    static Material TextMaterial(Font font)
    {
        if (textMaterial != null) return textMaterial;
        var template = Resources.Load<Material>("Hud/HudText");
        textMaterial = template != null ? new Material(template) : new Material(font.material);
        textMaterial.mainTexture = font.material.mainTexture;
        return textMaterial;
    }

    /// Always-on-top line (colour via LineRenderer start/end colours).
    public static LineRenderer CreateLine(string name, Transform parent, float width)
    {
        if (lineMaterial == null)
        {
            var template = Resources.Load<Material>("Hud/HudLine");
            lineMaterial = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        }
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.sharedMaterial = lineMaterial;
        line.startWidth = line.endWidth = width;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.TransformZ;
        return line;
    }
}
