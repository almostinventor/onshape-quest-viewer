using System;
using UnityEngine;

/// Short status messages (receiving, loaded, errors, part info). Shown on the keypad display.
public static class Status
{
    public enum Kind { Info, Ok, Error, Busy }

    public static string Message { get; private set; } = "";
    public static Kind Type { get; private set; }
    public static float Until { get; private set; }

    public static event Action Changed;

    public static void Info(string msg, float seconds = 3f) => Set(msg, Kind.Info, seconds);
    public static void Ok(string msg, float seconds = 3f) => Set(msg, Kind.Ok, seconds);
    public static void Error(string msg, float seconds = 6f) => Set(msg, Kind.Error, seconds);
    /// Stays until the next message.
    public static void Busy(string msg) => Set(msg, Kind.Busy, float.MaxValue);

    public static bool Visible => !string.IsNullOrEmpty(Message) && Time.unscaledTime < Until;

    static void Set(string msg, Kind kind, float seconds)
    {
        if (!string.IsNullOrEmpty(msg)) Debug.Log($"[Status] {kind}: {msg.Replace('\n', ' ')}");
        Message = msg;
        Type = kind;
        Until = seconds >= float.MaxValue ? float.MaxValue : Time.unscaledTime + seconds;
        Changed?.Invoke();
    }
}
