using System.Collections;
using UnityEngine;

/// Let go of the object just above a real table (within maxDrop) and it settles down onto the surface.
[RequireComponent(typeof(Grabbable))]
public class TableSnap : MonoBehaviour
{
    public float maxDrop = 0.12f;
    public float settleSeconds = 0.35f;

    Grabbable grabbable;

    void Awake() => grabbable = GetComponent<Grabbable>();
    void OnEnable() => Grabbable.Released += OnReleased;
    void OnDisable() => Grabbable.Released -= OnReleased;

    void OnReleased(Grabbable g)
    {
        if (g != grabbable || !TryBottom(out var bottom)) return;
        if (!PlaneTracker.TryFindSurfaceBelow(bottom, maxDrop, out float surfaceY)) return;
        StopAllCoroutines();
        StartCoroutine(Settle(surfaceY - bottom.y + 0.001f));
    }

    IEnumerator Settle(float dy)
    {
        grabbable.SetFrozen(true);
        var start = transform.position;
        var end = start + Vector3.up * dy;
        for (float t = 0; t < settleSeconds; t += Time.deltaTime)
        {
            if (grabbable.IsHeld) yield break;
            float k = 1f - Mathf.Pow(1f - t / settleSeconds, 3f);
            transform.position = Vector3.Lerp(start, end, k);
            yield return null;
        }
        if (grabbable.IsHeld) yield break;
        transform.position = end;
        grabbable.SetFrozen(ViewModes.Inspect);
        grabbable.Stop();
        Status.Info("On the table", 1.5f);
    }

    /// Lowest point of the object (centre of its bounding box bottom).
    bool TryBottom(out Vector3 bottom)
    {
        bottom = default;
        bool any = false;
        var b = new Bounds();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r.GetComponentInParent<GhostMarker>() != null) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any) return false;
        bottom = new Vector3(b.center.x, b.min.y, b.center.z);
        return true;
    }
}
