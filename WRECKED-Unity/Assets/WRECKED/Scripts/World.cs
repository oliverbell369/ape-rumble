using System.Collections.Generic;
using UnityEngine;

namespace Wrecked
{
    /// <summary>An oriented box: the only collision shape in the game (game space, see Conv).</summary>
    public class Box
    {
        public Vector3 c;
        public readonly float[] h = new float[3];
        public readonly float[] ax = new float[3], ay = new float[3], az = new float[3];
        public float rad;
        public Color col = Color.white;
        public bool hidden, dyn;
        public string mask; // which faces to draw, e.g. "+Y-Z" (null = all)
        public string mat = "rock", top, side;
    }

    public struct Hit
    {
        public float t;
        public Vector3 p, n;
        public Box box;
        public int fi;
    }

    public class Contact
    {
        public bool ground, wall;
        public int count;
        public void Reset() { ground = false; wall = false; count = 0; }
    }

    /// <summary>The Ravine Sawmill: level boxes, spawn points and collision queries.</summary>
    public static class World
    {
        public static readonly List<Box> Boxes = new List<Box>();
        public static readonly List<Vector3> Spawns = new List<Vector3>();
        public static readonly List<Vector3> EdgePoints = new List<Vector3>();
        public static readonly float[][] Crystals = { new[] { -16.3f, 1.5f, -2f, 1.5f }, new[] { -15.6f, 1.5f, 2.2f, 1.1f } };
        public static readonly Vector3 OutLedge = new Vector3(0f, 12f, 26f);
        public static Box LogBox;
        public static Box[] RopeBoxes;

        public static Box AddBox(float x, float cy, float z, float w, float h, float d,
            float rx = 0f, float ry = 0f, string mat = "rock", string top = null, string side = null, bool hidden = false, bool dyn = false)
        {
            // rotation = Euler(rx, ry, 0, 'YXZ') = Ry * Rx ; the box axes are the matrix columns
            float cx = Mathf.Cos(rx), sx = Mathf.Sin(rx), cyy = Mathf.Cos(ry), sy = Mathf.Sin(ry);
            var b = new Box { c = new Vector3(x, cy, z), mat = mat, top = top, side = side, hidden = hidden, dyn = dyn };
            b.h[0] = w / 2f; b.h[1] = h / 2f; b.h[2] = d / 2f;
            b.ax[0] = cyy; b.ax[1] = 0f; b.ax[2] = -sy;
            b.ay[0] = sy * sx; b.ay[1] = cx; b.ay[2] = cyy * sx;
            b.az[0] = sy * cx; b.az[1] = -sx; b.az[2] = cyy * cx;
            b.rad = Mathf.Sqrt(w * w + h * h + d * d) / 2f;
            Boxes.Add(b);
            return b;
        }

        static Box Blk(float x, float y0, float z, float w, float h, float d, string mat = "rock", string top = null, string side = null, float ry = 0f, bool hidden = false)
            => AddBox(x, y0 + h / 2f, z, w, h, d, 0f, ry, mat, top, side, hidden);

        static Box Deck(float x, float y0, float z, float w, float h, float d) => Blk(x, y0, z, w, h, d, "rock", "planks", "darkwood");

        public static void Build()
        {
            Boxes.Clear(); Spawns.Clear(); EdgePoints.Clear();
            // main deck (top at y = 0) on posts that drop into the ravine
            Deck(0f, -0.6f, 0f, 12f, 0.6f, 12f);
            foreach (var p in new[] { new Vector2(-5.6f, -5.6f), new Vector2(5.6f, -5.6f), new Vector2(-5.6f, 5.6f), new Vector2(5.6f, 5.6f) })
                Blk(p.x, -30f, p.y, 0.6f, 29.4f, 0.6f, "bark");
            // swing frame holding the log
            Blk(-5.6f, 0f, 0f, 0.5f, 10f, 0.5f, "bark");
            Blk(5.6f, 0f, 0f, 0.5f, 10f, 0.5f, "bark");
            Blk(0f, 10f, 0f, 11.7f, 0.5f, 0.5f, "bark");
            // crates
            Blk(-3.6f, 0f, -2.6f, 1.1f, 1.1f, 1.1f, "darkwood");
            Blk(3.8f, 0f, 3.4f, 1.0f, 1.0f, 1.0f, "darkwood");
            Blk(3.8f, 1.0f, 3.4f, 0.8f, 0.8f, 0.8f, "darkwood", ry: 0.4f);
            // east: plank ramp up to the shed deck
            float ae = Mathf.Atan2(1.2f, 5f);
            AddBox(8.5f, 0.45f, 0f, 2.2f, 0.3f, 5.3f, ae, Mathf.PI / 2f, "planks");
            Deck(14.5f, 0f, 0f, 7f, 1.2f, 9f);
            foreach (var p in new[] { new Vector2(11.3f, -4.2f), new Vector2(17.7f, -4.2f), new Vector2(11.3f, 4.2f), new Vector2(17.7f, 4.2f) })
                Blk(p.x, -30f, p.y, 0.6f, 30f, 0.6f, "bark");
            foreach (var p in new[] { new Vector2(12.3f, -3f), new Vector2(16.7f, -3f), new Vector2(12.3f, 3f), new Vector2(16.7f, 3f) })
                Blk(p.x, 1.2f, p.y, 0.35f, 2.6f, 0.35f, "bark");
            AddBox(14.5f, 4.65f, 1.755f, 5.6f, 0.25f, 3.9f, -0.45f, 0f, "planks");
            AddBox(14.5f, 4.65f, -1.755f, 5.6f, 0.25f, 3.9f, 0.45f, 0f, "planks");
            // west: narrow log bridge to a rock island with glowing crystals
            float aw = Mathf.Atan2(1.5f, 5f);
            AddBox(-8.6f, 0.55f, 0f, 0.9f, 0.5f, 5.6f, -aw, Mathf.PI / 2f, "bark");
            Blk(-14.5f, -30f, 0f, 7f, 31.5f, 7f, "rock", "grass");
            foreach (var c in Crystals) Blk(c[0], c[1], c[2], c[3], c[3] * 1.1f, c[3], hidden: true);
            // north: steep log up to a lookout
            Blk(0f, -30f, -11f, 0.7f, 34.2f, 0.7f, "bark");
            Deck(0f, 4.2f, -11f, 4f, 0.4f, 4f);
            AddBox(0f, 2.35f, -7.6f, 0.8f, 0.4f, 5.6f, -0.98f, 0f, "bark");
            // south: the "out" ledge where knocked-out players watch
            Deck(0f, 11.5f, 26f, 6f, 0.5f, 4f);
            Blk(0f, -30f, 26f, 0.6f, 41.5f, 0.6f, "bark");

            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + 0.5f;
                Spawns.Add(new Vector3(Mathf.Cos(a) * 4f, 0f, Mathf.Sin(a) * 4f));
            }
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                float s = 7.2f / Mathf.Max(Mathf.Abs(Mathf.Cos(a)), Mathf.Abs(Mathf.Sin(a)));
                EdgePoints.Add(new Vector3(Mathf.Cos(a) * s, 0f, Mathf.Sin(a) * s));
            }
            // moving parts you can grab and ride (updated every frame)
            LogBox = AddBox(0f, 0.9f, 0f, 4.6f, 0.84f, 0.84f, hidden: true, dyn: true);
            RopeBoxes = new[]
            {
                AddBox(-1.6f, 5.4f, 0f, 0.12f, 9f, 0.12f, hidden: true, dyn: true),
                AddBox(1.6f, 5.4f, 0f, 0.12f, 9f, 0.12f, hidden: true, dyn: true),
            };
        }

        public static void SetBoxRotX(Box b, float x, float y, float z, float th)
        {
            float c = Mathf.Cos(th), s = Mathf.Sin(th);
            b.c = new Vector3(x, y, z);
            b.ax[0] = 1f; b.ax[1] = 0f; b.ax[2] = 0f;
            b.ay[0] = 0f; b.ay[1] = c; b.ay[2] = s;
            b.az[0] = 0f; b.az[1] = -s; b.az[2] = c;
        }

        public static Vector3 ToLocal(Box b, Vector3 p)
        {
            float dx = p.x - b.c.x, dy = p.y - b.c.y, dz = p.z - b.c.z;
            return new Vector3(dx * b.ax[0] + dy * b.ax[1] + dz * b.ax[2], dx * b.ay[0] + dy * b.ay[1] + dz * b.ay[2], dx * b.az[0] + dy * b.az[1] + dz * b.az[2]);
        }
        public static Vector3 DirToLocal(Box b, Vector3 d)
            => new Vector3(d.x * b.ax[0] + d.y * b.ax[1] + d.z * b.ax[2], d.x * b.ay[0] + d.y * b.ay[1] + d.z * b.ay[2], d.x * b.az[0] + d.y * b.az[1] + d.z * b.az[2]);
        public static Vector3 DirToWorld(Box b, Vector3 l)
            => new Vector3(b.ax[0] * l.x + b.ay[0] * l.y + b.az[0] * l.z, b.ax[1] * l.x + b.ay[1] * l.y + b.az[1] * l.z, b.ax[2] * l.x + b.ay[2] * l.y + b.az[2] * l.z);
        public static Vector3 ToWorld(Box b, Vector3 l) => DirToWorld(b, l) + b.c;

        static float Cl(float v, float a, float b) => v < a ? a : (v > b ? b : v);

        /// <summary>Push a sphere out of every box it overlaps. Optionally removes velocity into the surfaces.</summary>
        public static void PushSphere(ref Vector3 p, float r, Contact res, ref Vector3 vel, bool useVel)
        {
            for (int bi = 0; bi < Boxes.Count; bi++)
            {
                var b = Boxes[bi];
                float dx = p.x - b.c.x, dy = p.y - b.c.y, dz = p.z - b.c.z;
                float rr = b.rad + r;
                if (dx * dx + dy * dy + dz * dz > rr * rr) continue;
                float lx = dx * b.ax[0] + dy * b.ax[1] + dz * b.ax[2];
                float ly = dx * b.ay[0] + dy * b.ay[1] + dz * b.ay[2];
                float lz = dx * b.az[0] + dy * b.az[1] + dz * b.az[2];
                float hx = b.h[0], hy = b.h[1], hz = b.h[2];
                float ex = lx - Cl(lx, -hx, hx), ey = ly - Cl(ly, -hy, hy), ez = lz - Cl(lz, -hz, hz);
                float d2 = ex * ex + ey * ey + ez * ez;
                float nx, ny, nz, pen;
                if (d2 > 1e-12f)
                {
                    if (d2 >= r * r) continue;
                    float d = Mathf.Sqrt(d2);
                    nx = ex / d; ny = ey / d; nz = ez / d; pen = r - d;
                }
                else
                {
                    float px = hx - Mathf.Abs(lx), py = hy - Mathf.Abs(ly), pz = hz - Mathf.Abs(lz);
                    nx = ny = nz = 0f;
                    if (px < py && px < pz) { nx = lx < 0 ? -1f : 1f; pen = px + r; }
                    else if (py < pz) { ny = ly < 0 ? -1f : 1f; pen = py + r; }
                    else { nz = lz < 0 ? -1f : 1f; pen = pz + r; }
                }
                float wx = nx * b.ax[0] + ny * b.ay[0] + nz * b.az[0];
                float wy = nx * b.ax[1] + ny * b.ay[1] + nz * b.az[1];
                float wz = nx * b.ax[2] + ny * b.ay[2] + nz * b.az[2];
                p.x += wx * pen; p.y += wy * pen; p.z += wz * pen;
                if (res != null) { res.count++; if (wy > 0.55f) res.ground = true; else if (Mathf.Abs(wy) < 0.4f) res.wall = true; }
                if (useVel)
                {
                    float vn = vel.x * wx + vel.y * wy + vel.z * wz;
                    if (vn < 0f) { vel.x -= wx * vn; vel.y -= wy * vn; vel.z -= wz * vn; }
                }
            }
        }

        public static void PushSphere(ref Vector3 p, float r, Contact res)
        {
            Vector3 dummy = Vector3.zero;
            PushSphere(ref p, r, res, ref dummy, false);
        }

        static readonly float[] O = new float[3], D = new float[3];

        /// <summary>Sweep a sphere from a to b against all boxes. Returns the first hit.</summary>
        public static bool Sweep(Vector3 a, Vector3 bpt, float r, out Hit hit, bool skipInside = false)
        {
            hit = default;
            float Dx = bpt.x - a.x, Dy = bpt.y - a.y, Dz = bpt.z - a.z;
            float segLen = Mathf.Sqrt(Dx * Dx + Dy * Dy + Dz * Dz);
            float best = float.PositiveInfinity;
            Box bb = null;
            int bk = 0; float bs = 0f, bnx = 0f, bny = 0f, bnz = 0f;
            bool insideHit = false;
            float mx = a.x + Dx * 0.5f, my = a.y + Dy * 0.5f, mz = a.z + Dz * 0.5f;
            for (int bi = 0; bi < Boxes.Count; bi++)
            {
                var b = Boxes[bi];
                float cx = mx - b.c.x, cy = my - b.c.y, cz = mz - b.c.z;
                float rr = b.rad + r + segLen * 0.5f;
                if (cx * cx + cy * cy + cz * cz > rr * rr) continue;
                float ox = a.x - b.c.x, oy = a.y - b.c.y, oz = a.z - b.c.z;
                O[0] = ox * b.ax[0] + oy * b.ax[1] + oz * b.ax[2];
                O[1] = ox * b.ay[0] + oy * b.ay[1] + oz * b.ay[2];
                O[2] = ox * b.az[0] + oy * b.az[1] + oz * b.az[2];
                D[0] = Dx * b.ax[0] + Dy * b.ax[1] + Dz * b.ax[2];
                D[1] = Dx * b.ay[0] + Dy * b.ay[1] + Dz * b.ay[2];
                D[2] = Dx * b.az[0] + Dy * b.az[1] + Dz * b.az[2];
                float tmin = float.NegativeInfinity, tmax = float.PositiveInfinity, sgn = 0f;
                int axis = -1;
                bool miss = false;
                for (int k = 0; k < 3; k++)
                {
                    float H = b.h[k] + r;
                    if (Mathf.Abs(D[k]) < 1e-9f) { if (O[k] < -H || O[k] > H) { miss = true; break; } }
                    else
                    {
                        float t1 = (-H - O[k]) / D[k], t2 = (H - O[k]) / D[k], s = -1f;
                        if (t1 > t2) { float tt = t1; t1 = t2; t2 = tt; s = 1f; }
                        if (t1 > tmin) { tmin = t1; axis = k; sgn = s; }
                        if (t2 < tmax) tmax = t2;
                        if (tmin > tmax) { miss = true; break; }
                    }
                }
                if (miss || tmax < 0f) continue;
                if (tmin >= 0f)
                {
                    if (tmin <= 1f && tmin < best) { best = tmin; bb = b; bk = axis; bs = sgn; insideHit = false; }
                }
                else if (!skipInside && 0f < best)
                {
                    // starting inside the inflated box: only a hit if moving inward
                    float hx = b.h[0], hy = b.h[1], hz = b.h[2];
                    float ex = O[0] - Cl(O[0], -hx, hx), ey = O[1] - Cl(O[1], -hy, hy), ez = O[2] - Cl(O[2], -hz, hz);
                    float nx = 0f, ny = 0f, nz = 0f;
                    float d2 = ex * ex + ey * ey + ez * ez;
                    if (d2 > 1e-12f)
                    {
                        float aex = Mathf.Abs(ex), aey = Mathf.Abs(ey), aez = Mathf.Abs(ez);
                        if (aex >= aey && aex >= aez) nx = Mathf.Sign(ex); else if (aey >= aez) ny = Mathf.Sign(ey); else nz = Mathf.Sign(ez);
                    }
                    else
                    {
                        float px = hx - Mathf.Abs(O[0]), py = hy - Mathf.Abs(O[1]), pz = hz - Mathf.Abs(O[2]);
                        if (px < py && px < pz) nx = O[0] < 0 ? -1f : 1f; else if (py < pz) ny = O[1] < 0 ? -1f : 1f; else nz = O[2] < 0 ? -1f : 1f;
                    }
                    if (D[0] * nx + D[1] * ny + D[2] * nz < 0f)
                    {
                        best = 0f; bb = b; insideHit = true; bnx = nx; bny = ny; bnz = nz;
                        bk = nx != 0f ? 0 : (ny != 0f ? 1 : 2);
                        bs = nx != 0f ? nx : (ny != 0f ? ny : nz);
                    }
                }
            }
            if (bb == null) return false;
            hit.t = best; hit.box = bb;
            hit.p = new Vector3(a.x + Dx * best, a.y + Dy * best, a.z + Dz * best);
            float fx = 0f, fy = 0f, fz = 0f;
            if (insideHit) { fx = bnx; fy = bny; fz = bnz; }
            else { if (bk == 0) fx = bs; else if (bk == 1) fy = bs; else fz = bs; }
            hit.n = new Vector3(
                fx * bb.ax[0] + fy * bb.ay[0] + fz * bb.az[0],
                fx * bb.ax[1] + fy * bb.ay[1] + fz * bb.az[1],
                fx * bb.ax[2] + fy * bb.ay[2] + fz * bb.az[2]);
            hit.fi = bk * 2 + (bs > 0f ? 0 : 1);
            return true;
        }
    }
}
