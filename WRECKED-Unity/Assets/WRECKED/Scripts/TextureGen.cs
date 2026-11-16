using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wrecked
{
    /// <summary>The ten 64x64 pixel-art textures, generated in code into one Texture2DArray.</summary>
    public static class TextureGen
    {
        public const int Size = 64;
        public const int Layers = 10;

        public struct Mat
        {
            public int l; public float s;
            public Mat(int l, float s) { this.l = l; this.s = s; }
        }

        public static readonly Dictionary<string, Mat> Mats = new Dictionary<string, Mat>
        {
            { "grass", new Mat(0, 0.25f) }, { "dirt", new Mat(1, 0.25f) }, { "rock", new Mat(2, 0.22f) }, { "planks", new Mat(3, 0.9f) },
            { "bark", new Mat(4, 0.6f) }, { "darkwood", new Mat(5, 0.7f) }, { "stone", new Mat(6, 0.5f) }, { "leaves", new Mat(7, 0.45f) },
            { "crystal", new Mat(8, 0.7f) }, { "fur", new Mat(9, 2.2f) },
        };

        public static float[] LayerScales()
        {
            var s = new float[Layers];
            foreach (var kv in Mats) s[kv.Value.l] = kv.Value.s;
            return s;
        }

        /// <summary>Tileable value noise (same as the web version's tileNoise).</summary>
        static Func<float, float, float> TileNoise(uint seed, int px, int py = -1)
        {
            if (py < 0) py = px;
            var r = new Rng(seed);
            var g = new float[px * py];
            for (int i = 0; i < g.Length; i++) g[i] = r.Next();
            int ppx = px, ppy = py;
            float At(int x, int y) => g[(((y % ppy) + ppy) % ppy) * ppx + (((x % ppx) + ppx) % ppx)];
            return (u, v) =>
            {
                float x = u * ppx, y = v * ppy;
                int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
                float fx = x - xi, fy = y - yi;
                float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
                float a = At(xi, yi), b = At(xi + 1, yi), c = At(xi, yi + 1), d = At(xi + 1, yi + 1);
                return a + (b - a) * sx + (c - a) * sy + (a - b - c + d) * sx * sy;
            };
        }

        static Vector3 Pal(Vector3[] p, float t) => p[Mathf.Clamp(Mathf.FloorToInt(t * p.Length), 0, p.Length - 1)];
        static Vector3 V(float r, float g, float b) => new Vector3(r, g, b);
        static float H2(float a, float b) => MathX.Hash2(a, b);

        public static Texture2DArray Build()
        {
            var gens = new List<Func<float, float, int, int, Vector3>>();
            { // 0 grass: mossy mosaic of greens and tans
                var a = TileNoise(11, 4); var b = TileNoise(12, 8); var c = TileNoise(13, 16); var r = new Rng(14);
                var P = new[] { V(46, 64, 26), V(66, 88, 32), V(88, 114, 42), V(116, 138, 56), V(146, 150, 84), V(176, 168, 118) };
                gens.Add((u, v, i, j) => Pal(P, (0.45f * a(u, v) + 0.3f * b(u, v) + 0.25f * c(u, v)) * 0.8f + r.Next() * 0.32f - 0.06f));
            }
            { // 1 dirt
                var a = TileNoise(21, 4); var b = TileNoise(22, 16); var r = new Rng(23);
                var P = new[] { V(64, 48, 33), V(86, 65, 45), V(108, 84, 58), V(132, 106, 76), V(158, 136, 102) };
                gens.Add((u, v, i, j) => Pal(P, 0.55f * a(u, v) + 0.25f * b(u, v) + r.Next() * 0.3f - 0.05f));
            }
            { // 2 rock: warm grey with cracks and strata
                var a = TileNoise(31, 4); var b = TileNoise(32, 8); var c = TileNoise(33, 16); var k = TileNoise(34, 6); var r = new Rng(35);
                gens.Add((u, v, i, j) =>
                {
                    float bas = 0.5f * a(u, v) + 0.3f * b(u, v) + 0.2f * c(u, v);
                    float g = 88 + bas * 92 + r.Next() * 18 + Mathf.Sin((v * 5 + bas * 1.5f) * Mathf.PI * 2) * 7;
                    float ridge = 1 - Mathf.Abs(k(u, v) * 2 - 1);
                    if (ridge > 0.96f) g *= 0.74f;
                    return V(g * 1.03f, g * 0.98f, g * 0.9f);
                });
            }
            { // 3 planks: 4 boards across, grain along u
                var gr = TileNoise(41, 4, 32); var r = new Rng(42);
                gens.Add((u, v, i, j) =>
                {
                    int p = j / 16, jj = j % 16;
                    float t = (0.8f + 0.3f * H2(p, 3)) * (0.78f + 0.32f * gr(u + p * 0.37f, v)) + r.Next() * 0.06f;
                    if (jj == 0) t *= 0.4f; else if (jj == 1) t *= 0.78f;
                    if ((i + p * 23) % 64 < 1) t *= 0.5f;
                    if (jj == 8 && (i + p * 11) % 32 == 4) t *= 0.45f;
                    return V(150 * t, 104 * t, 64 * t);
                });
            }
            { // 4 bark: vertical furrows
                var a = TileNoise(51, 8, 2); var b = TileNoise(52, 16, 4); var r = new Rng(53);
                gens.Add((u, v, i, j) =>
                {
                    float n = 0.65f * a(u, v) + 0.35f * b(u, v);
                    float t = 0.65f + n * 0.55f + r.Next() * 0.08f;
                    if (n < 0.38f) t *= 0.55f;
                    return V(104 * t, 76 * t, 54 * t);
                });
            }
            { // 5 dark wood: vertical boards
                var gr = TileNoise(61, 32, 4); var r = new Rng(62);
                gens.Add((u, v, i, j) =>
                {
                    int p = i / 16, ii = i % 16;
                    float t = (0.78f + 0.32f * H2(p, 9)) * (0.75f + 0.35f * gr(u, v + p * 0.29f)) + r.Next() * 0.06f;
                    if (ii == 0) t *= 0.4f; else if (ii == 1) t *= 0.75f;
                    return V(98 * t, 70 * t, 48 * t);
                });
            }
            { // 6 stone tiles
                var a = TileNoise(71, 8); var r = new Rng(72);
                gens.Add((u, v, i, j) =>
                {
                    if (i % 32 < 2 || j % 32 < 2) return V(66, 64, 60);
                    float g = 112 + 46 * H2(i / 32, j / 32) + a(u, v) * 26 + r.Next() * 12;
                    return V(g, g * 0.99f, g * 0.96f);
                });
            }
            { // 7 leaves
                var a = TileNoise(81, 8); var b = TileNoise(82, 16); var r = new Rng(83);
                var P = new[] { V(20, 40, 20), V(32, 62, 26), V(52, 92, 34), V(80, 124, 46), V(112, 150, 62) };
                gens.Add((u, v, i, j) => Pal(P, 0.55f * a(u, v) + 0.45f * b(u, v) + r.Next() * 0.28f - 0.1f));
            }
            { // 8 crystal: bright facets with streaks (tinted per shard)
                var a = TileNoise(91, 4); var r = new Rng(92);
                gens.Add((u, v, i, j) =>
                {
                    float s = (u + v) * 3 + a(u, v) * 0.8f;
                    float f = s - Mathf.Floor(s);
                    float g = 175 + (f < 0.15f ? 80 : f * 40) + r.Next() * 14;
                    return V(g, g, g);
                });
            }
            { // 9 fur: soft streaks, tinted per ape
                var a = TileNoise(101, 16, 4); var b = TileNoise(102, 32, 8); var r = new Rng(103);
                gens.Add((u, v, i, j) =>
                {
                    float n = 0.6f * a(u, v) + 0.4f * b(u, v);
                    float t = 0.66f + n * 0.45f + r.Next() * 0.08f;
                    if (n < 0.33f) t *= 0.72f;
                    float g = 235 * t;
                    return V(g, g, g);
                });
            }

            // linear = true: values are used exactly as written (the shaders treat them as display colours)
            var tex = new Texture2DArray(Size, Size, Layers, TextureFormat.RGBA32, true, true);
            var px = new Color32[Size * Size];
            for (int l = 0; l < Layers; l++)
            {
                for (int j = 0; j < Size; j++)
                    for (int i = 0; i < Size; i++)
                    {
                        Vector3 c = gens[l]((i + 0.5f) / Size, (j + 0.5f) / Size, i, j);
                        px[j * Size + i] = new Color32((byte)Mathf.Clamp(c.x, 0, 255), (byte)Mathf.Clamp(c.y, 0, 255), (byte)Mathf.Clamp(c.z, 0, 255), 255);
                    }
                tex.SetPixels32(px, l, 0);
            }
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.anisoLevel = 0;
            tex.Apply(true, true);
            return tex;
        }
    }
}
