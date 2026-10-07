using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes the boss theme to Assets/Resources/Audio/MusicBoss.wav, where GameAudio picks it
/// up by name and crossfades to it when the boss spawns.
///
/// Synthesised rather than generated, because VARCO Sound returns a video with a mixed
/// soundtrack over it — usable as ambience, but there was no way to get a separate boss
/// track out of it (Blender's headless mixdown produced 0.04s and there is no ffmpeg here).
///
/// Everything is written with wrap-around indexing, so a note's tail at the end of the loop
/// lands at the beginning instead of being cut off. That is what makes the loop seamless;
/// truncating the tails leaves an audible click every 16 seconds.
/// </summary>
public static class BossMusicSynth
{
    const int Rate = 44100;
    const string Path = "Assets/Resources/Audio/MusicBoss.wav";

    const float Bpm = 124f;
    const int Bars = 8;

    [MenuItem("Tools/Wakkuball/Generate Boss Music")]
    public static void Generate()
    {
        float beat = 60f / Bpm;
        float barLength = beat * 4f;
        int total = Mathf.RoundToInt(barLength * Bars * Rate);
        var buf = new float[total];

        // A minor: the bass walks A - A - F - G, which keeps circling back without resolving
        float[] roots = { 55.00f, 55.00f, 43.65f, 49.00f };
        // pentatonic ostinato, deliberately repetitive — it is a chase, not a melody
        float[] lead = { 440f, 523.25f, 587.33f, 659.25f, 587.33f, 523.25f, 440f, 392f };

        var rnd = new System.Random(9001);

        for (int bar = 0; bar < Bars; bar++)
        {
            float barStart = bar * barLength;
            float root = roots[bar % roots.Length];

            // low drone under the whole bar, with its octave so small speakers hear it at all
            AddTone(buf, barStart, barLength, root, 0.30f, 1.1f, rnd);
            AddTone(buf, barStart, barLength, root * 2f, 0.16f, 1.3f, rnd);

            for (int b = 0; b < 4; b++)
            {
                float beatStart = barStart + b * beat;

                // kick on 1 and 3, plus a pickup before the bar turns over
                if (b == 0 || b == 2) AddKick(buf, beatStart, 0.35f);
                if (b == 3) AddKick(buf, beatStart + beat * 0.5f, 0.22f);

                // snare on 2 and 4
                if (b == 1 || b == 3) AddNoise(buf, beatStart, 0.18f, 1900f, 1.1f, 28f, 0.30f, rnd);

                // hats on every eighth, quiet, just to keep the pulse moving
                AddNoise(buf, beatStart, 0.06f, 7000f, 0.9f, 90f, 0.07f, rnd);
                AddNoise(buf, beatStart + beat * 0.5f, 0.06f, 7000f, 0.9f, 90f, 0.05f, rnd);

                // ostinato in eighths
                int step = (bar * 8 + b * 2) % lead.Length;
                AddPluck(buf, beatStart, beat * 0.48f, lead[step], 0.15f);
                AddPluck(buf, beatStart + beat * 0.5f, beat * 0.48f, lead[(step + 1) % lead.Length], 0.11f);
            }

            // a low swell every four bars, so the loop has somewhere to build to
            if (bar % 4 == 3) AddSwell(buf, barStart, barLength, root * 3f, 0.13f);
        }

        Write(buf);
        AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);
        ApplyImportSettings();
        Debug.Log("BossMusicSynth: wrote " + (total / (float)Rate).ToString("F1") + "s loop to " + Path);
    }

    // ------------------------------------------------------------------ voices

    /// <summary>Sustained low tone with a slow tremolo, the bed the rest sits on.</summary>
    static void AddTone(float[] buf, float start, float length, float freq, float gain,
                        float detuneCents, System.Random rnd)
    {
        int n = Mathf.RoundToInt(length * Rate);
        int s0 = Mathf.RoundToInt(start * Rate);
        float detune = freq * Mathf.Pow(2f, detuneCents / 1200f);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            // two voices a hair apart beat against each other, which is what makes a drone
            // feel uneasy instead of flat
            float v = Mathf.Sin(2f * Mathf.PI * freq * t) + Mathf.Sin(2f * Mathf.PI * detune * t);
            float env = Mathf.Min(1f, t / 0.08f) * Mathf.Min(1f, (length - t) / 0.15f);
            Accumulate(buf, s0 + i, v * 0.5f * gain * env);
        }
    }

    static void AddKick(float[] buf, float start, float gain)
    {
        int n = Mathf.RoundToInt(0.28f * Rate);
        int s0 = Mathf.RoundToInt(start * Rate);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float freq = Mathf.Lerp(120f, 42f, Mathf.Clamp01(t / 0.06f));
            float body = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-t * 11f);
            float click = Mathf.Sin(2f * Mathf.PI * 900f * t) * Mathf.Exp(-t * 120f) * 0.4f;
            Accumulate(buf, s0 + i, (body + click) * gain);
        }
    }

    static void AddNoise(float[] buf, float start, float length, float freq, float q,
                         float decay, float gain, System.Random rnd)
    {
        int n = Mathf.RoundToInt(length * Rate);
        int s0 = Mathf.RoundToInt(start * Rate);

        // a fresh band-passed burst each time, filtered inline so the state stays local
        float w0 = 2f * Mathf.PI * freq / Rate;
        float alpha = Mathf.Sin(w0) / (2f * q);
        float a0 = 1f + alpha;
        float b0 = alpha / a0, b2 = -alpha / a0;
        float a1 = (-2f * Mathf.Cos(w0)) / a0, a2 = (1f - alpha) / a0;
        float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float x = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x;
            y2 = y1; y1 = y;
            Accumulate(buf, s0 + i, y * Mathf.Exp(-t * decay) * gain);
        }
    }

    /// <summary>Short plucked note: a couple of harmonics with a fast decay.</summary>
    static void AddPluck(float[] buf, float start, float length, float freq, float gain)
    {
        int n = Mathf.RoundToInt(length * Rate);
        int s0 = Mathf.RoundToInt(start * Rate);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float v = Mathf.Sin(2f * Mathf.PI * freq * t)
                    + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.35f
                    + Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * 0.15f;
            float env = Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * 9f);
            Accumulate(buf, s0 + i, v * 0.45f * gain * env);
        }
    }

    /// <summary>Rising dissonant swell, used once per four bars.</summary>
    static void AddSwell(float[] buf, float start, float length, float freq, float gain)
    {
        int n = Mathf.RoundToInt(length * Rate);
        int s0 = Mathf.RoundToInt(start * Rate);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float p = t / length;
            // a minor second above the root: the interval that reads as "something is wrong"
            float v = Mathf.Sin(2f * Mathf.PI * freq * t)
                    + Mathf.Sin(2f * Mathf.PI * freq * 1.06f * t);
            Accumulate(buf, s0 + i, v * 0.5f * gain * p * p);
        }
    }

    // ------------------------------------------------------------------ output

    /// <summary>Adds a sample, wrapping past the end so note tails close the loop.</summary>
    static void Accumulate(float[] buf, int index, float value)
    {
        buf[((index % buf.Length) + buf.Length) % buf.Length] += value;
    }

    static void Write(float[] samples)
    {
        int n = samples.Length;
        float peak = 0.0001f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
        float gain = 0.85f / peak;

        var pcm = new short[n];
        for (int i = 0; i < n; i++)
            pcm[i] = (short)Mathf.Clamp(samples[i] * gain * 32767f, -32768f, 32767f);

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        using (var stream = new FileStream(Path, FileMode.Create))
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
    }

    /// <summary>Same treatment as the combat bed: streamed and Vorbis, not decompressed in RAM.</summary>
    static void ApplyImportSettings()
    {
        var importer = AssetImporter.GetAtPath(Path) as AudioImporter;
        if (importer == null) return;

        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.Streaming;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.6f;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;
        importer.SaveAndReimport();
    }
}
