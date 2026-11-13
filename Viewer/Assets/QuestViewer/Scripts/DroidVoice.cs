using System.Collections.Generic;
using UnityEngine;

/// Marvin's voice: little droid beeps and whistles, synthesised in code (no sound files), different every time.
/// Reply() is his answer to "thanks" (a random mood plus a line for the keypad display); Answer() is his short
/// reply to a command.
public static class DroidVoice
{
    const int Rate = 44100;

    enum Mood { Happy, Shy, Excited, Proud }

    public enum Reaction
    {
        Okay,   // neutral "got it"
        On,     // something switched on: rising
        Off,    // something switched off: falling
        OnIt,   // eager, for sending the model
        Sad,    // couldn't do it
    }

    public static AudioClip Answer(Reaction reaction)
    {
        var buf = new List<float>(Rate / 2);
        float j = Random.Range(0.92f, 1.08f); // a little different every time
        switch (reaction)
        {
            case Reaction.On:
                Tone(buf, 1100f * j, 2200f * j, 0.09f);
                Gap(buf, 0.025f);
                Tone(buf, 2600f * j, 2700f * j, 0.05f);
                break;
            case Reaction.Off:
                Tone(buf, 2200f * j, 1500f * j, 0.08f);
                Gap(buf, 0.03f);
                Tone(buf, 1300f * j, 900f * j, 0.11f);
                break;
            case Reaction.OnIt:
                for (int i = 0; i < 3; i++)
                {
                    Tone(buf, (1300f + i * 350f) * j, (1400f + i * 350f) * j, 0.045f);
                    Gap(buf, 0.02f);
                }
                Gap(buf, 0.03f);
                Tone(buf, 1200f * j, 2900f * j, 0.17f, vibRate: 20f, vibDepth: 0.05f);
                break;
            case Reaction.Sad:
                Tone(buf, 1800f * j, 700f * j, 0.45f, vibRate: 7f, vibDepth: 0.04f, amp: 0.8f);
                Gap(buf, 0.08f);
                Tone(buf, 620f * j, 560f * j, 0.07f, amp: 0.5f);
                break;
            default:
                Tone(buf, 1400f * j, 1500f * j, 0.05f);
                Gap(buf, 0.03f);
                Tone(buf, 1900f * j, 2300f * j, 0.07f);
                break;
        }
        Normalize(buf, 0.5f);
        var clip = AudioClip.Create("Marvin_" + reaction, buf.Count, 1, Rate, false);
        clip.SetData(buf.ToArray(), 0);
        return clip;
    }

    public static (AudioClip clip, string text) Reply()
    {
        var mood = (Mood)Random.Range(0, 4);
        var buf = new List<float>(Rate);
        string text;
        switch (mood)
        {
            case Mood.Happy:   Happy(buf);   text = "Marvin: happy beeps"; break;
            case Mood.Shy:     Shy(buf);     text = "Marvin blushes"; break;
            case Mood.Excited: Excited(buf); text = "Marvin is thrilled!"; break;
            default:           Proud(buf);   text = "Marvin beams"; break;
        }
        Normalize(buf, 0.6f);
        var clip = AudioClip.Create("Marvin_" + mood, buf.Count, 1, Rate, false);
        clip.SetData(buf.ToArray(), 0);
        return (clip, text);
    }

    /// A few bouncy rising chirps, then a high trill that lifts at the end.
    static void Happy(List<float> b)
    {
        int n = Random.Range(3, 6);
        for (int i = 0; i < n; i++)
        {
            float f = Random.Range(900f, 1500f) * (1f + i * 0.08f);
            Tone(b, f, f * Random.Range(1.5f, 2.2f), Random.Range(0.06f, 0.11f));
            Gap(b, Random.Range(0.025f, 0.05f));
        }
        Tone(b, 2300f, 2900f, 0.22f, vibRate: 22f, vibDepth: 0.07f);
    }

    /// Bashful: a soft falling "aww", two shy little blips, then a hopeful upward "hm?".
    static void Shy(List<float> b)
    {
        Tone(b, Random.Range(2000f, 2400f), Random.Range(1100f, 1400f), 0.32f, vibRate: 6f, vibDepth: 0.03f, amp: 0.7f);
        Gap(b, 0.09f);
        Tone(b, 1000f, 1000f, 0.045f, amp: 0.45f);
        Gap(b, 0.05f);
        Tone(b, 920f, 900f, 0.045f, amp: 0.4f);
        Gap(b, 0.11f);
        Tone(b, 880f, Random.Range(1500f, 1800f), 0.19f, amp: 0.6f);
    }

    /// Rapid jittery beeps, then a big swooping whistle.
    static void Excited(List<float> b)
    {
        int n = Random.Range(6, 10);
        for (int i = 0; i < n; i++)
        {
            float f = Random.Range(1500f, 3200f);
            Tone(b, f, f * Random.Range(0.95f, 1.1f), Random.Range(0.035f, 0.055f));
            Gap(b, 0.015f);
        }
        Gap(b, 0.04f);
        Tone(b, 800f, Random.Range(3000f, 3600f), 0.26f, vibRate: 18f, vibDepth: 0.05f);
    }

    /// A pleased warbling trill, then a satisfied "doo-dee".
    static void Proud(List<float> b)
    {
        Tone(b, 1800f, 1900f, 0.22f, vibRate: 28f, vibDepth: 0.12f);
        Gap(b, 0.06f);
        Tone(b, 1600f, 1200f, 0.12f);
        Gap(b, 0.03f);
        Tone(b, 1200f, Random.Range(1900f, 2300f), 0.16f);
    }

    /// One glide from f0 to f1 (exponential), with optional vibrato; soft attack and release.
    static void Tone(List<float> b, float f0, float f1, float seconds, float vibRate = 0f, float vibDepth = 0f, float amp = 1f)
    {
        int n = Mathf.Max(1, (int)(seconds * Rate));
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float u = i / (float)n;
            float f = f0 * Mathf.Pow(f1 / f0, u);
            if (vibRate > 0f) f *= 1f + vibDepth * Mathf.Sin(2f * Mathf.PI * vibRate * t);
            phase += 2f * Mathf.PI * f / Rate;
            float env = Mathf.Min(1f, t / 0.006f) * Mathf.Min(1f, (seconds - t) / 0.025f);
            // A touch of second and third harmonic gives it a buzzy, electronic voice.
            float s = Mathf.Sin(phase) + 0.22f * Mathf.Sin(2f * phase) + 0.08f * Mathf.Sin(3f * phase);
            b.Add(s * env * amp);
        }
    }

    static void Gap(List<float> b, float seconds)
    {
        int n = (int)(seconds * Rate);
        for (int i = 0; i < n; i++) b.Add(0f);
    }

    static void Normalize(List<float> b, float peak)
    {
        float max = 1e-4f;
        foreach (var s in b) max = Mathf.Max(max, Mathf.Abs(s));
        float k = peak / max;
        for (int i = 0; i < b.Count; i++) b[i] *= k;
    }
}
