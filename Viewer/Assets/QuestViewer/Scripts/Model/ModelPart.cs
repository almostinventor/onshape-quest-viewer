using UnityEngine;

/// One Onshape part inside the loaded model: name, part ID, true size, mass (when Onshape knows it),
/// and its home position for the exploded view.
public class ModelPart : MonoBehaviour
{
    public string PartName;
    public string PartId;
    /// True size in mm, in Onshape's axes (X, Y, Z).
    public Vector3 SizeMm;
    /// Kilograms, or NaN when the part has no material in Onshape.
    public float MassKg = float.NaN;
    /// Cubic metres, or NaN when unknown.
    public float VolumeM3 = float.NaN;
    /// Local position in the model before exploding.
    public Vector3 Home;
    /// Offset at full explosion (model-local).
    public Vector3 ExplodeOffset;
    public Renderer Renderer;

    public string Describe()
    {
        var s = $"{PartName}\n{SizeMm.x:0.#} x {SizeMm.y:0.#} x {SizeMm.z:0.#} mm";
        if (!float.IsNaN(MassKg) && MassKg > 0f) s += MassKg >= 1f ? $"   {MassKg:0.00} kg" : $"   {MassKg * 1000f:0.#} g";
        else if (!float.IsNaN(VolumeM3) && VolumeM3 > 0f) s += $"   {VolumeM3 * 1e6f:0.#} cm3";
        return s;
    }
}
