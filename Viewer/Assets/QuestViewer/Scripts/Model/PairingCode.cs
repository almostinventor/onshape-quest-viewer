using System.Security.Cryptography;
using UnityEngine;

/// A random 6-character code generated once per install. The browser extension must send it with every request,
/// so only the extension you paired can talk to this viewer.
public static class PairingCode
{
    const string Key = "pairing.code";
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O or 1/I

    static string cached;

    /// Read on the main thread once (PlayerPrefs is main-thread only); after that it's a plain string.
    public static string Value => cached ??= LoadOrCreate();

    static string LoadOrCreate()
    {
        var code = PlayerPrefs.GetString(Key, "");
        if (code.Length == 6) return code;
        var bytes = new byte[6];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        var chars = new char[6];
        for (int i = 0; i < 6; i++) chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        code = new string(chars);
        PlayerPrefs.SetString(Key, code);
        PlayerPrefs.Save();
        return code;
    }

    /// Constant-time comparison (doesn't reveal how many characters matched).
    public static bool Matches(string candidate)
    {
        var code = cached;
        if (code == null || candidate == null) return false;
        candidate = candidate.Trim().ToUpperInvariant();
        if (candidate.Length != code.Length) return false;
        int diff = 0;
        for (int i = 0; i < code.Length; i++) diff |= code[i] ^ candidate[i];
        return diff == 0;
    }
}
