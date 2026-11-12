using System;
using UnityEngine;

public enum LookStyle { Solid, Clay, XRay }
public enum LightingMode { Shaded, Flat }

/// The user-switchable modes (keypad, controller buttons, gestures).
/// Look settings (hologram, style, lighting) are remembered between sessions; tool modes start off.
public static class ViewModes
{
    public static bool Hologram { get; private set; }
    public static LookStyle Style { get; private set; }
    public static LightingMode Lighting { get; private set; }
    public static bool Explode { get; private set; }
    public static bool Turntable { get; private set; }
    /// Spin direction about the vertical axis: +1 or -1 (same sense as Unity's rotation about +Y).
    public static float TurntableDirection { get; private set; } = 1f;
    public static bool Cut { get; private set; }
    public static bool Measure { get; private set; }
    public static bool Inspect { get; private set; }

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Load()
    {
        Hologram = PlayerPrefs.GetInt("mode.hologram", 0) == 1;
        Style = (LookStyle)Mathf.Clamp(PlayerPrefs.GetInt("mode.style", 0), 0, 2);
        Lighting = (LightingMode)Mathf.Clamp(PlayerPrefs.GetInt("mode.lighting", 0), 0, 1);
        Explode = Turntable = Cut = Measure = Inspect = false;
    }

    public static void ToggleHologram() { Hologram = !Hologram; Save(); }
    public static void CycleStyle() { Style = (LookStyle)(((int)Style + 1) % 3); Save(); }
    public static void ToggleLighting() { Lighting = Lighting == LightingMode.Shaded ? LightingMode.Flat : LightingMode.Shaded; Save(); }
    public static void ToggleExplode() { Explode = !Explode; Save(); }
    public static void ToggleTurntable() { Turntable = !Turntable; Save(); }
    public static void SetTurntable(bool on, float direction)
    {
        Turntable = on;
        if (direction != 0f) TurntableDirection = Mathf.Sign(direction);
        Save();
    }
    public static void ToggleCut() { Cut = !Cut; Save(); }
    public static void ToggleMeasure() { Measure = !Measure; Save(); }
    public static void ToggleInspect() { Inspect = !Inspect; Save(); }

    static void Save()
    {
        PlayerPrefs.SetInt("mode.hologram", Hologram ? 1 : 0);
        PlayerPrefs.SetInt("mode.style", (int)Style);
        PlayerPrefs.SetInt("mode.lighting", (int)Lighting);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
