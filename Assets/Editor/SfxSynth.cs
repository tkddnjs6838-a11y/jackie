using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the game's sound effects procedurally and writes them into
/// Assets/Resources/Audio, where GameAudio picks them up by filename.
///
/// These are synthesised rather than sampled because the game needs a specific texture:
/// the monsters are rubbery balls, so hits want a soft, springy thud and deaths want a
/// water-balloon burst rather than metal or bone. Everything here is one function per
/// sound so a single sound can be re-tuned without touching the rest.
/// </summary>
public static class SfxSynth
{
    const int Rate = 44100;
    const string Folder = "Assets/Resources/Audio";

    [MenuItem("Tools/Wakkuball/Generate SFX")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(Folder);

        Write("Swing", Swing());
        Write("Hit", Hit());
        Write("EnemyDie", EnemyDie());
        Write("PlayerHurt", PlayerHurt());
        Write("PlayerDie", PlayerDie());
        Write("Dash", Dash());
        Write("Pickup", Pickup());
        Write("UpgradeOpen", UpgradeOpen());
        Write("UpgradePick", UpgradePick());
        Write("BossSpawn", BossSpawn());
        Write("BossDie", BossDie());
        Write("Victory", Victory());

        AssetDatabase.Refresh();
        Debug.Log("SfxSynth: wrote 12 clips to " + Folder);
    }

    // ---------------------------------------------------------------- sounds

    /// <summary>Air moving past a blade: noise with a swept filter, no tonal content.</summary>
    static float[] Swing()
    {
        int n = Len(0.22f);
        var buf = new float[n];
        var rnd = new System.Random(11);
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float p = i / (float)n;
            // cutoff rises as the blade accelerates through the arc, then drops
            float cutoff = Mathf.Lerp(0.06f, 0.55f, Mathf.Sin(p * Mathf.PI));
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * cutoff;
            // short fade in, long fade out, so it reads as a swipe and not a click
            float env = Mathf.Sin(p * Mathf.PI) * Mathf.Exp(-t * 4f);
            buf[i] = lp * env * 0.55f;
        }
        return buf;
    }

    /// <summary>
    /// Blade into a wax squishy. The creatures are wax stress toys, so this is deliberately
    /// dead: wax is dense and does not ring, meaning short decays, a low damped body, and no
    /// springy tail. A rubber-ball version of this sounded wrong — too bouncy.
    ///
    /// It is still built across bands rather than as one low thud, because the first version
    /// was almost all sub-200Hz and small speakers could not reproduce it, so the swing's
    /// broadband noise buried it and the game sounded like nothing but sword.
    /// </summary>
    static float[] Hit()
    {
        int n = Len(0.17f);
        var buf = new float[n];
        var noise = Noise(n, 21);

        var smack = Bandpass(noise, 1300f, 1.2f);   // the blade meeting the surface
        var press = Bandpass(noise, 430f, 2.2f);    // dense wax giving way, low and damped
        var tear = Crackle(n, 22, 0.010f, 40f, 2400f, 1.3f);   // a touch of 바지직

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            // sags as the wax deforms, then stops dead — no sustain, no bounce
            float freq = Mathf.Lerp(230f, 135f, Mathf.Clamp01(t / 0.06f));
            float body = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 40f);

            // the body is deliberately the quietest layer: it peaks highest, and since the
            // file is peak-normalised, letting it lead squashes everything audible
            buf[i] = smack[i] * Mathf.Exp(-t * 85f) * 4.5f
                   + press[i] * Mathf.Exp(-t * 34f) * 4.0f
                   + tear[i] * 2.5f
                   + body * 0.40f;
        }
        return buf;
    }

    /// <summary>
    /// The wax shell breaking, in the two stages the sound is described by: 바지직, the
    /// split tearing open, then 바사삭, the brittle crust crumbling. Both stages are sparse
    /// impulse trains rather than filtered noise — noise hisses, impulses crackle.
    ///
    /// There is no springy tail here on purpose. An earlier rubber-ball version ended on a
    /// falling "boing", which read as a bouncy toy rather than something breaking apart.
    /// </summary>
    static float[] EnemyDie()
    {
        int n = Len(0.40f);
        var buf = new float[n];
        var noise = Noise(n, 31);

        // 바지직 — the split: mid-high, dense at the start, thinning as it runs out of shell
        var rip = Crackle(n, 32, 0.055f, 22f, 2300f, 1.2f);
        // 바사삭 — the crumble: brighter, sparser, and it outlasts the split
        var crumble = Crackle(n, 33, 0.030f, 7f, 4400f, 0.9f);
        // the dull wax mass underneath, so the break has some weight to it
        var mass = Bandpass(noise, 380f, 1.6f);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            // a short damped drop, not a resonant tail: wax thuds and stops
            float drop = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(300f, 130f, Mathf.Clamp01(t / 0.07f)) * t)
                         * Mathf.Exp(-t * 30f);

            buf[i] = rip[i] * 3.2f
                   + crumble[i] * 2.6f
                   + mass[i] * Mathf.Exp(-t * 26f) * 2.6f
                   + drop * 0.35f;
        }
        return buf;
    }

    /// <summary>Taking a hit: a blunt low knock, deliberately dry so it cuts through combat.</summary>
    static float[] PlayerHurt()
    {
        int n = Len(0.20f);
        var buf = new float[n];
        var rnd = new System.Random(41);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float freq = Mathf.Lerp(150f, 70f, Mathf.Clamp01(t / 0.14f));
            float thud = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 18f);
            float grit = (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 55f) * 0.35f;
            buf[i] = (thud + grit) * 0.8f;
        }
        return buf;
    }

    /// <summary>
    /// Game over: two low drum hits, 두 · 둥~, the second ringing out.
    ///
    /// The previous version was a long descending tone with a 6Hz vibrato on it, and that
    /// modulation — six wobbles a second — read as cartoon laughter rather than defeat.
    /// There is no vibrato here at all.
    /// </summary>
    static float[] PlayerDie()
    {
        int n = Len(1.70f);
        var buf = new float[n];
        var noise = Noise(n, 45);
        var skin = Bandpass(noise, 220f, 1.3f);     // the drum head, not the body
        // A 58Hz fundamental is inaudible on laptop and phone speakers, so the hit also
        // carries its octave and a beater click. Those are what actually get heard; the
        // fundamental only adds weight on speakers that can reproduce it.
        var beater = Bandpass(noise, 1200f, 1.0f);

        var starts = new float[] { 0f, 0.27f };
        var freqs = new float[] { 82f, 58f };
        var decays = new float[] { 6.5f, 1.9f };    // the second hit is the one that hangs

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float sum = 0f;

            for (int k = 0; k < starts.Length; k++)
            {
                if (t < starts[k]) continue;
                float local = t - starts[k];
                // each hit sags a little as it decays, the way a struck drum does
                float f = freqs[k] * Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(local / 0.5f));
                sum += Mathf.Sin(2f * Mathf.PI * f * local) * Mathf.Exp(-local * decays[k]);
                sum += Mathf.Sin(2f * Mathf.PI * f * 2f * local) * Mathf.Exp(-local * decays[k] * 1.5f) * 0.55f;
                sum += skin[i] * Mathf.Exp(-local * 30f) * 0.6f;
                sum += beater[i] * Mathf.Exp(-local * 55f) * 0.9f;
            }
            buf[i] = sum * 0.6f;
        }
        return buf;
    }

    static float[] Dash()
    {
        int n = Len(0.26f);
        var buf = new float[n];
        var rnd = new System.Random(51);
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float p = i / (float)n;
            // cutoff climbs the whole way: an upward whoosh, the opposite of the swing
            float cutoff = Mathf.Lerp(0.03f, 0.7f, p);
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * cutoff;
            float env = Mathf.Sin(p * Mathf.PI);
            buf[i] = lp * env * 0.5f;
        }
        return buf;
    }

    /// <summary>Health orb: a clean rising two-note blip, the only bright sound in combat.</summary>
    static float[] Pickup()
    {
        int n = Len(0.30f);
        var buf = new float[n];
        float half = 0.13f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float freq = t < half ? 660f : 990f;      // E5 then B5
            float local = t < half ? t : t - half;
            float env = Mathf.Exp(-local * 9f) * Mathf.Min(1f, local * 200f);
            buf[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.45f;
        }
        return buf;
    }

    static float[] UpgradeOpen()
    {
        var notes = new float[] { 523f, 659f, 784f };   // C5 E5 G5
        int n = Len(0.52f);
        var buf = new float[n];
        float step = 0.14f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            int idx = Mathf.Min(notes.Length - 1, (int)(t / step));
            float local = t - idx * step;
            float env = Mathf.Exp(-local * 7f) * Mathf.Min(1f, local * 150f);
            buf[i] = Mathf.Sin(2f * Mathf.PI * notes[idx] * t) * env * 0.4f;
        }
        return buf;
    }

    /// <summary>Upgrade taken: the same chord all at once, so it lands as a confirmation.</summary>
    static float[] UpgradePick()
    {
        var notes = new float[] { 523f, 659f, 784f, 1046f };
        int n = Len(0.45f);
        var buf = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Exp(-t * 5.5f) * Mathf.Min(1f, t * 220f);
            float sum = 0f;
            for (int k = 0; k < notes.Length; k++)
                sum += Mathf.Sin(2f * Mathf.PI * notes[k] * t);
            buf[i] = (sum / notes.Length) * env * 0.6f;
        }
        return buf;
    }

    /// <summary>The giant arriving: sub-bass swell into one heavy footfall.</summary>
    static float[] BossSpawn()
    {
        int n = Len(1.9f);
        var buf = new float[n];
        var rnd = new System.Random(61);
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float p = t / 1.9f;

            float swell = Mathf.Pow(Mathf.Clamp01(p / 0.75f), 2f) * (1f - Mathf.Clamp01((p - 0.75f) / 0.25f));
            float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(38f, 52f, p) * t) * swell;

            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.05f;                 // heavily filtered: rumble, not hiss
            float rumble = lp * swell * 1.6f;

            // the impact lands at 0.75 of the way through, where the swell peaks
            float hitT = t - 1.42f;
            float impact = hitT > 0f
                ? Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 40f, Mathf.Clamp01(hitT / 0.25f)) * hitT)
                  * Mathf.Exp(-hitT * 9f)
                : 0f;

            buf[i] = (sub * 0.8f + rumble * 0.7f + impact * 0.9f) * 0.75f;
        }
        return buf;
    }

    static float[] BossDie()
    {
        int n = Len(2.2f);
        var buf = new float[n];
        var rnd = new System.Random(71);
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * 0.18f;

            float blast = lp * Mathf.Exp(-t * 3.2f);
            float sub = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 28f, Mathf.Clamp01(t / 1.2f)) * t)
                        * Mathf.Exp(-t * 2.2f);
            // debris: sparse crackle over the tail so the collapse keeps going
            float crackle = (rnd.NextDouble() < 0.004) ? (float)(rnd.NextDouble() * 2.0 - 1.0) : 0f;
            crackle *= Mathf.Exp(-t * 1.4f) * 0.5f;

            buf[i] = (blast * 0.85f + sub * 0.8f + crackle) * 0.8f;
        }
        return buf;
    }

    static float[] Victory()
    {
        var notes = new float[] { 523f, 659f, 784f, 1046f };
        var starts = new float[] { 0f, 0.16f, 0.32f, 0.50f };
        int n = Len(1.6f);
        var buf = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float sum = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                if (t < starts[k]) continue;
                float local = t - starts[k];
                // the last note rings out; the first three are short steps up to it
                float decay = (k == notes.Length - 1) ? 1.6f : 5f;
                float env = Mathf.Exp(-local * decay) * Mathf.Min(1f, local * 180f);
                sum += Mathf.Sin(2f * Mathf.PI * notes[k] * local) * env;
            }
            buf[i] = (sum / notes.Length) * 0.7f;
        }
        return buf;
    }

    // ---------------------------------------------------------------- helpers

    static int Len(float seconds) { return (int)(Rate * seconds); }

    static float[] Noise(int n, int seed)
    {
        var rnd = new System.Random(seed);
        var buf = new float[n];
        for (int i = 0; i < n; i++) buf[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);
        return buf;
    }

    /// <summary>
    /// Sparse impulses through a high band — the "바지직" crackle of a shell splitting.
    /// Density thins out over the length of the sound, so it tears and then dies away
    /// instead of buzzing evenly.
    /// </summary>
    static float[] Crackle(int n, int seed, float density, float thinning, float freq, float q)
    {
        var rnd = new System.Random(seed);
        var impulses = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float chance = density * Mathf.Exp(-t * thinning);
            if (rnd.NextDouble() < chance)
                impulses[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);
        }
        return Bandpass(impulses, freq, q);
    }

    /// <summary>
    /// Standard RBJ band-pass biquad. Used to place each layer of an impact in a specific
    /// band, which is the only way to keep a hit audible over the swing's broadband noise.
    /// </summary>
    static float[] Bandpass(float[] x, float freq, float q)
    {
        float w0 = 2f * Mathf.PI * freq / Rate;
        float alpha = Mathf.Sin(w0) / (2f * q);
        float cos = Mathf.Cos(w0);

        float a0 = 1f + alpha;
        float b0 = alpha / a0;
        float b2 = -alpha / a0;
        float a1 = (-2f * cos) / a0;
        float a2 = (1f - alpha) / a0;

        var y = new float[x.Length];
        float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;

        for (int i = 0; i < x.Length; i++)
        {
            float v = b0 * x[i] + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x[i];
            y2 = y1; y1 = v;
            y[i] = v;
        }
        return y;
    }

    /// <summary>
    /// Total level and the share of it above roughly 600Hz. Small speakers only reproduce
    /// the second number, so it is what decides whether a sound survives in the mix.
    /// </summary>
    public static string Balance(string name)
    {
        string path = Folder + "/" + name + ".wav";
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) return name + "=missing";

        var data = new float[clip.samples * clip.channels];
        clip.GetData(data, 0);

        // one-pole high-pass at ~600Hz: what is left is the audible-on-laptops part
        float k = Mathf.Exp(-2f * Mathf.PI * 600f / clip.frequency);
        float prev = 0f, prevOut = 0f;
        double total = 0.0, high = 0.0;

        for (int i = 0; i < data.Length; i++)
        {
            float hp = k * (prevOut + data[i] - prev);
            prevOut = hp; prev = data[i];
            total += data[i] * data[i];
            high += hp * hp;
        }

        double rms = System.Math.Sqrt(total / data.Length);
        double hrms = System.Math.Sqrt(high / data.Length);
        return name + ": rms=" + rms.ToString("F3") + " above600Hz=" + hrms.ToString("F3")
             + " (" + (100.0 * hrms / System.Math.Max(0.0001, rms)).ToString("F0") + "%)";
    }

    /// <summary>
    /// Normalises to a consistent peak and applies a short fade at both ends, then writes
    /// a 16-bit mono WAV. Without the fades the buffers click on start and stop.
    /// </summary>
    static void Write(string name, float[] samples)
    {
        int n = samples.Length;
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(samples[i]));

        float gain = 0.89f / peak;
        int fade = Mathf.Min(220, n / 8);

        var pcm = new short[n];
        for (int i = 0; i < n; i++)
        {
            float v = samples[i] * gain;
            if (i < fade) v *= i / (float)fade;
            if (i > n - fade) v *= (n - i) / (float)fade;
            pcm[i] = (short)Mathf.Clamp(v * 32767f, -32768f, 32767f);
        }

        string path = Folder + "/" + name + ".wav";
        using (var stream = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(stream))
        {
            int dataBytes = n * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataBytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataBytes);
            for (int i = 0; i < n; i++) w.Write(pcm[i]);
        }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
}
