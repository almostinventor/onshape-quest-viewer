using UnityEngine;

/// Short status messages (loading, errors, hints) near the top of your view, below the model info.
public class Hud : MonoBehaviour
{
    public Transform head;
    public float upDegrees = 10f;
    public float distance = 0.8f;

    static Hud instance;
    TextMesh text;
    string message;
    float until;
    bool busy;
    bool placed;

    static readonly Color White = Color.white;
    static readonly Color Green = new Color(0.45f, 1f, 0.55f);
    static readonly Color Red = new Color(1f, 0.45f, 0.4f);

    public static void Info(string msg, float seconds = 3f) => instance?.Set(msg, White, seconds, false);
    public static void Ok(string msg, float seconds = 3f) => instance?.Set(msg, Green, seconds, false);
    public static void Error(string msg, float seconds = 6f) => instance?.Set(msg, Red, seconds, false);
    /// Stays up (with animated dots) until the next message.
    public static void Busy(string msg) => instance?.Set(msg, White, float.MaxValue, true);

    void Awake()
    {
        instance = this;
        text = HeadLocked.CreateText("Hud", 0.0032f, TextAnchor.UpperCenter);
    }

    void Set(string msg, Color color, float seconds, bool isBusy)
    {
        if (!string.IsNullOrEmpty(msg)) Debug.Log("[Hud] " + msg.Replace('\n', ' '));
        message = msg;
        text.color = color;
        busy = isBusy;
        until = seconds >= float.MaxValue ? float.MaxValue : Time.unscaledTime + seconds;
    }

    void LateUpdate()
    {
        bool show = !string.IsNullOrEmpty(message) && Time.unscaledTime < until && head != null;
        text.gameObject.SetActive(show);
        if (!show) { placed = false; return; }
        text.text = busy ? message + new string('.', 1 + (int)(Time.unscaledTime * 3f) % 3) : message;
        HeadLocked.Follow(text.transform, HeadLocked.Target(head, upDegrees, distance), 8f, !placed);
        placed = true;
    }
}
