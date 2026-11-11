using System;
using UnityEngine;

public enum HudMode { Off, Minimal, Visor }
public enum LightingMode { Shaded, Flat }

/// The user-switchable modes (keypad, controller buttons, keyboard). Remembered between sessions.
public static class ViewModes
{
    public static bool Hologram { get; private set; }
    public static HudMode Hud { get; private set; } = HudMode.Visor;
    public static LightingMode Lighting { get; private set; } = LightingMode.Shaded;

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Load()
    {
        Hologram = PlayerPrefs.GetInt("mode.hologram", 0) == 1;
        Hud = (HudMode)Mathf.Clamp(PlayerPrefs.GetInt("mode.hud", (int)HudMode.Visor), 0, 2);
        Lighting = (LightingMode)Mathf.Clamp(PlayerPrefs.GetInt("mode.lighting", 0), 0, 1);
    }

    public static void ToggleHologram() { Hologram = !Hologram; Save(); }
    public static void CycleHud() { Hud = (HudMode)(((int)Hud + 1) % 3); Save(); }
    public static void ToggleLighting() { Lighting = Lighting == LightingMode.Shaded ? LightingMode.Flat : LightingMode.Shaded; Save(); }

    static void Save()
    {
        PlayerPrefs.SetInt("mode.hologram", Hologram ? 1 : 0);
        PlayerPrefs.SetInt("mode.hud", (int)Hud);
        PlayerPrefs.SetInt("mode.lighting", (int)Lighting);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
