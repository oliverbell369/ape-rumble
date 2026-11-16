using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wrecked
{
    /// <summary>All sound effects are synthesised in code at startup (same recipes as the web version).</summary>
    public class Sfx
    {
        const int Rate = 44100;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly AudioSource src;
        readonly System.Random rnd = new System.Random(7);

        public Sfx(GameObject host)
        {
            src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            Make("slap", 0.45f, 0.12f, (b) => { Noise(b, 0.1f, 1800, 0.7f, false); Tone(b, 320, 140, 0.06f, Wave.Tri); });
            Make("bonk", 0.5f, 0.3f, (b) => { Tone(b, 260, 90, 0.25f, Wave.Tri); Noise(b, 0.12f, 700, 0.8f, true); });
            Make("grab", 0.15f, 0.08f, (b) => Noise(b, 0.07f, 900, 1.5f, false));
            Make("throw", 0.18f, 0.3f, (b) => Noise(b, 0.3f, 600, 0.6f, false));
            Make("ko", 0.25f, 0.8f, (b) => { float[] f = { 880, 1100, 880, 1100 }; for (int i = 0; i < 4; i++) Tone(b, f[i], f[i] * 0.98f, 0.12f, Wave.Sine, i * 0.12f); });
            Make("fall", 0.25f, 1.1f, (b) => Tone(b, 700, 120, 1.0f, Wave.Tri));
            Make("tap", 0.05f, 0.04f, (b) => Noise(b, 0.04f, 1400, 2f, false));
            Make("beep", 0.2f, 0.2f, (b) => Tone(b, 660, 660, 0.16f, Wave.Sine));
            Make("go", 0.25f, 0.4f, (b) => Tone(b, 990, 990, 0.36f, Wave.Sine));
            Make("win", 0.25f, 0.9f, (b) => { float[] f = { 523, 659, 784, 1047 }; for (int i = 0; i < 4; i++) Tone(b, f[i], f[i], 0.18f, Wave.Tri, i * 0.13f); });
        }

        enum Wave { Sine, Tri, Square }

        void Make(string name, float vol, float envDur, Action<float[]> fill)
        {
            int n = Mathf.CeilToInt(Mathf.Max(envDur, 0.6f) * Rate) + 64;
            var buf = new float[n];
            fill(buf);
            // gain envelope: exponential fall from vol to 0.0008 over envDur, then held
            float k = Mathf.Log(0.0008f / vol);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float g = vol * Mathf.Exp(k * Mathf.Min(1f, t / envDur));
                buf[i] = Mathf.Clamp(buf[i] * g, -1f, 1f);
            }
            int last = n - 1;
            while (last > 0 && Mathf.Abs(buf[last]) < 1e-4f) last--;
            int len = Mathf.Max(64, last + 1);
            var clip = AudioClip.Create(name, len, 1, Rate, false);
            var trimmed = new float[len];
            Array.Copy(buf, trimmed, len);
            clip.SetData(trimmed, 0);
            clips[name] = clip;
        }

        void Noise(float[] b, float dur, float freq, float q, bool lowpass)
        {
            // RBJ biquad on white noise
            float w0 = 2f * Mathf.PI * freq / Rate, cw = Mathf.Cos(w0), alpha = Mathf.Sin(w0) / (2f * (lowpass ? 0.707f : q));
            float b0, b1, b2, a0 = 1 + alpha, a1 = -2 * cw, a2 = 1 - alpha;
            if (lowpass) { b0 = (1 - cw) / 2; b1 = 1 - cw; b2 = (1 - cw) / 2; }
            else { b0 = alpha; b1 = 0; b2 = -alpha; }
            float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            int n = Mathf.Min(b.Length, Mathf.CeilToInt(dur * Rate));
            for (int i = 0; i < n; i++)
            {
                float x = (float)(rnd.NextDouble() * 2 - 1);
                float y = (b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                b[i] += y;
            }
        }

        void Tone(float[] b, float f0, float f1, float dur, Wave w, float off = 0f)
        {
            int start = Mathf.FloorToInt(off * Rate), n = Mathf.CeilToInt((dur + 0.02f) * Rate);
            double phase = 0;
            for (int i = 0; i < n && start + i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float f = f0 * Mathf.Pow(f1 / f0, Mathf.Min(1f, t / dur));
                phase += f / Rate;
                float p = (float)(phase - Math.Floor(phase));
                float s = w == Wave.Sine ? Mathf.Sin(p * Mathf.PI * 2f) : w == Wave.Tri ? 1f - 4f * Mathf.Abs(p - 0.5f) : (p < 0.5f ? 1f : -1f);
                b[start + i] += s;
            }
        }

        /// <summary>Play a sound. `at` (game space) quietens it with distance from the listener.</summary>
        public void Play(string name, Vector3? at, Vector3 listener, float vol = 1f)
        {
            if (!clips.TryGetValue(name, out var clip)) return;
            if (at.HasValue)
            {
                vol *= 1f / (1f + Vector3.Distance(at.Value, listener) * 0.15f);
                if (vol < 0.03f) return;
            }
            src.PlayOneShot(clip, Mathf.Clamp(vol, 0f, 2f));
        }
    }
}
