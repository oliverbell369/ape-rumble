using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wrecked
{
    /// <summary>A triangle soup in game space: every 3 vertices form one counter-clockwise (outward) triangle.</summary>
    public class Geo
    {
        public readonly List<Vector3> v = new List<Vector3>();

        public void Tri(Vector3 a, Vector3 b, Vector3 c) { v.Add(a); v.Add(b); v.Add(c); }

        public Geo Translate(float x, float y, float z)
        {
            var o = new Vector3(x, y, z);
            for (int i = 0; i < v.Count; i++) v[i] += o;
            return this;
        }

        // ---- primitives matching three.js shapes ----
        static readonly float T = (1f + Mathf.Sqrt(5f)) / 2f;
        static readonly Vector3[] IcoV =
        {
            new Vector3(-1, T, 0), new Vector3(1, T, 0), new Vector3(-1, -T, 0), new Vector3(1, -T, 0),
            new Vector3(0, -1, T), new Vector3(0, 1, T), new Vector3(0, -1, -T), new Vector3(0, 1, -T),
            new Vector3(T, 0, -1), new Vector3(T, 0, 1), new Vector3(-T, 0, -1), new Vector3(-T, 0, 1),
        };
        static readonly int[] IcoI =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        static readonly Vector3[] OctV = { new Vector3(1, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, -1, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1) };
        static readonly int[] OctI = { 0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2, 1, 2, 5, 1, 5, 3, 1, 3, 4, 1, 4, 2 };

        static Geo Polyhedron(Vector3[] verts, int[] idx, float radius, int detail)
        {
            var g = new Geo();
            int cols = detail + 1;
            for (int f = 0; f < idx.Length; f += 3)
            {
                Vector3 a = verts[idx[f]], b = verts[idx[f + 1]], c = verts[idx[f + 2]];
                var rows = new List<Vector3>[cols + 1];
                for (int i = 0; i <= cols; i++)
                {
                    rows[i] = new List<Vector3>();
                    Vector3 aj = Vector3.Lerp(a, c, (float)i / cols);
                    Vector3 bj = Vector3.Lerp(b, c, (float)i / cols);
                    int r = cols - i;
                    for (int j = 0; j <= r; j++)
                        rows[i].Add(j == 0 && i == cols ? aj : Vector3.Lerp(aj, bj, r == 0 ? 0f : (float)j / r));
                }
                for (int i = 0; i < cols; i++)
                    for (int j = 0; j < 2 * (cols - i) - 1; j++)
                    {
                        int k = j / 2;
                        if (j % 2 == 0) g.Tri(rows[i][k + 1], rows[i + 1][k], rows[i][k]);
                        else g.Tri(rows[i][k + 1], rows[i + 1][k + 1], rows[i + 1][k]);
                    }
            }
            for (int i = 0; i < g.v.Count; i++) g.v[i] = g.v[i].normalized * radius;
            return g;
        }

        public static Geo Ico(float r, int detail = 1) => Polyhedron(IcoV, IcoI, r, detail);
        public static Geo Octa(float r = 1f) => Polyhedron(OctV, OctI, r, 0);

        /// <summary>Cylinder centred on the origin along Y (three.js CylinderGeometry with caps).</summary>
        public static Geo Cyl(float rTop, float rBot, float h, int seg)
        {
            var g = new Geo();
            float y0 = h / 2f, y1 = -h / 2f;
            var top = new Vector3(0, y0, 0); var bot = new Vector3(0, y1, 0);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1));
                Vector3 t0 = d0 * rTop + top, t1 = d1 * rTop + top, b0 = d0 * rBot + bot, b1 = d1 * rBot + bot;
                g.Tri(t0, b0, b1);
                if (rTop > 0f) g.Tri(t0, b1, t1);
                if (rTop > 0f) g.Tri(top, t0, t1);
                if (rBot > 0f) g.Tri(bot, b1, b0);
            }
            return g;
        }

        public static Geo Cone(float r, float h, int seg) => Cyl(0f, r, h, seg);

        public static Geo BoxG(float w, float h, float d)
        {
            var g = new Geo();
            float x = w / 2f, y = h / 2f, z = d / 2f;
            Vector3 P(float a, float b, float c) => new Vector3(a, b, c);
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 e) { g.Tri(a, b, c); g.Tri(a, c, e); }
            Quad(P(x, -y, z), P(x, -y, -z), P(x, y, -z), P(x, y, z));     // +X
            Quad(P(-x, -y, -z), P(-x, -y, z), P(-x, y, z), P(-x, y, -z)); // -X
            Quad(P(-x, y, z), P(x, y, z), P(x, y, -z), P(-x, y, -z));     // +Y
            Quad(P(-x, -y, -z), P(x, -y, -z), P(x, -y, z), P(-x, -y, z)); // -Y
            Quad(P(-x, -y, z), P(x, -y, z), P(x, y, z), P(-x, y, z));     // +Z
            Quad(P(x, -y, -z), P(-x, -y, -z), P(-x, y, -z), P(x, y, -z)); // -Z
            return g;
        }

        /// <summary>Flat disc in the XZ plane facing +Y.</summary>
        public static Geo Disc(float r, int seg)
        {
            var g = new Geo();
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                g.Tri(Vector3.zero, new Vector3(Mathf.Sin(a0) * r, 0, Mathf.Cos(a0) * r), new Vector3(Mathf.Sin(a1) * r, 0, Mathf.Cos(a1) * r));
            }
            return g;
        }
    }

    public struct Part
    {
        public Geo geo; public Matrix4x4 m; public Color c; public int layer; public bool tint;
        public Part(Geo geo, Color c, Matrix4x4 m, int layer = -1, bool tint = false) { this.geo = geo; this.c = c; this.m = m; this.layer = layer; this.tint = tint; }
    }

    /// <summary>Builds Unity meshes from game-space triangles (flat shaded, mirrored into Unity space).</summary>
    public class MeshBuilder
    {
        readonly List<Vector3> pos = new List<Vector3>(), nor = new List<Vector3>();
        readonly List<Color> col = new List<Color>();
        readonly List<Vector4> uv = new List<Vector4>();
        readonly List<int> idx = new List<int>();

        public int Count => pos.Count;

        /// <summary>Add one game-space triangle (counter-clockwise from outside) with a flat normal.</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color cl, Vector4 data)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            n = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            Tri(a, b, c, n, n, n, cl, cl, cl, data, data, data);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Color ca, Color cb, Color cc, Vector4 da, Vector4 db, Vector4 dc)
        {
            int i0 = pos.Count;
            pos.Add(Conv.U(a)); pos.Add(Conv.U(b)); pos.Add(Conv.U(c));
            nor.Add(Conv.U(na)); nor.Add(Conv.U(nb)); nor.Add(Conv.U(nc));
            col.Add(ca); col.Add(cb); col.Add(cc);
            uv.Add(da); uv.Add(db); uv.Add(dc);
            // mirroring z flips the winding, so swap two corners
            idx.Add(i0); idx.Add(i0 + 2); idx.Add(i0 + 1);
        }

        public void AddGeo(Geo g, Matrix4x4 m, Color c, Vector4 data)
        {
            for (int i = 0; i < g.v.Count; i += 3)
                Tri(m.MultiplyPoint3x4(g.v[i]), m.MultiplyPoint3x4(g.v[i + 1]), m.MultiplyPoint3x4(g.v[i + 2]), c, data);
        }

        public Mesh Build(string name, bool bigBounds = false)
        {
            var mesh = new Mesh { name = name };
            if (pos.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(pos);
            mesh.SetNormals(nor);
            mesh.SetColors(col);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(idx, 0, true);
            if (bigBounds) mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000f);
            mesh.UploadMeshData(false);
            return mesh;
        }

        /// <summary>Merge parts into one ape-style mesh. uv0 = (texture layer or -1, 1 if tinted by the ape colour).</summary>
        public static Mesh MergeParts(string name, params Part[] parts)
        {
            var mb = new MeshBuilder();
            foreach (var p in parts) mb.AddGeo(p.geo, p.m, p.c, new Vector4(p.layer, p.tint ? 1f : 0f, 0f, 0f));
            return mb.Build(name);
        }

        // ---------------- the level: every visible box face in one mesh ----------------
        struct FaceDef
        {
            public int k; public Vector3 n, u, v; public string name;
            public FaceDef(int k, Vector3 n, Vector3 u, Vector3 v, string name) { this.k = k; this.n = n; this.u = u; this.v = v; this.name = name; }
        }
        static readonly FaceDef[] Faces =
        {
            new FaceDef(0, new Vector3(1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0), "+X"),
            new FaceDef(0, new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0), "-X"),
            new FaceDef(1, new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1), "+Y"),
            new FaceDef(1, new Vector3(0, -1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), "-Y"),
            new FaceDef(2, new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 1, 0), "+Z"),
            new FaceDef(2, new Vector3(0, 0, -1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0), "-Z"),
        };
        static int AxisOf(Vector3 v) => v.x != 0 ? 0 : (v.y != 0 ? 1 : 2);

        public static Mesh BuildLevel()
        {
            var mb = new MeshBuilder();
            foreach (var b in World.Boxes)
            {
                if (b.hidden) continue;
                for (int i = 0; i < 6; i++)
                {
                    var fd = Faces[i];
                    if (b.mask != null && !b.mask.Contains(fd.name)) continue;
                    float hu = b.h[AxisOf(fd.u)], hv = b.h[AxisOf(fd.v)], hn = b.h[fd.k];
                    Vector3 lo = fd.n * hn - fd.u * hu - fd.v * hv;
                    Vector3 o = World.DirToWorld(b, lo), U = World.DirToWorld(b, fd.u), V = World.DirToWorld(b, fd.v), N = World.DirToWorld(b, fd.n);
                    var mat = TextureGen.Mats[i == 2 ? (b.top ?? b.mat) : (b.side ?? b.mat)];
                    float w = hu * 2f, h = hv * 2f;
                    var corners = new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h) };
                    var p = new Vector3[4]; var c = new Color[4]; var d = new Vector4[4];
                    for (int q = 0; q < 4; q++)
                    {
                        float uu = corners[q].x, vv = corners[q].y;
                        p[q] = b.c + o + U * uu + V * vv;
                        float ao = Mathf.Abs(N.y) < 0.5f && Mathf.Abs(p[q].y) < 0.05f ? 0.7f : 1f;
                        c[q] = new Color(b.col.r * ao, b.col.g * ao, b.col.b * ao, 1f);
                        d[q] = new Vector4(uu * mat.s, vv * mat.s, mat.l, 0f);
                    }
                    mb.Tri(p[0], p[1], p[2], N, N, N, c[0], c[1], c[2], d[0], d[1], d[2]);
                    mb.Tri(p[0], p[2], p[3], N, N, N, c[0], c[2], c[3], d[0], d[2], d[3]);
                }
            }
            return mb.Build("Level", true);
        }

        // ---------------- scenery: canyon rocks, pines, crystals (one mesh) ----------------
        public static Mesh BuildScenery()
        {
            var mb = new MeshBuilder();
            var r = new Rng(2024);
            void Add(Geo geo, Matrix4x4 m, string matName, Color c, float glow = 0f, int jitter = 0)
            {
                var src = geo.v;
                var pts = new Vector3[src.Count];
                for (int i = 0; i < src.Count; i++)
                {
                    Vector3 v = src[i];
                    if (jitter != 0)
                    {
                        float h = MathX.Hash2(Mathf.Round(v.x * 97) + Mathf.Round(v.z * 13) * 7, Mathf.Round(v.y * 53) + jitter * 3.1f);
                        float k = 1 + (h - 0.5f) * 0.45f;
                        v = new Vector3(v.x * k, v.y * (1 + (MathX.Hash2(h * 91, jitter) - 0.5f) * 0.3f), v.z * k);
                    }
                    pts[i] = m.MultiplyPoint3x4(v);
                }
                var data = new Vector4(TextureGen.Mats[matName].l, glow, TextureGen.Mats[matName].s, 0f);
                for (int i = 0; i < pts.Length; i += 3) mb.Tri(pts[i], pts[i + 1], pts[i + 2], c, data);
            }
            void Rock(float x, float y, float z, float sx, float sy, float sz, float tint)
            {
                var m = MathX.M(x, y, z, r.Next() * 0.4f, r.Next() * 6.28f, r.Next() * 0.4f, sx, sy, sz);
                Add(Geo.Ico(1f, 1), m, "rock", new Color(tint, tint * 0.98f, tint * 0.95f), 0f, Mathf.FloorToInt(r.Next() * 1000) + 1);
            }
            void Pine(float x, float y, float z, float s)
            {
                Add(Geo.Cyl(0.22f * s, 0.32f * s, 4.5f * s, 6), MathX.M(x, y + 2.25f * s, z), "bark", Color.white);
                float t = 0.85f + r.Next() * 0.3f;
                var layers = new[] { new Vector2(1.8f, 2.2f), new Vector2(1.45f, 3.6f), new Vector2(1.0f, 4.9f), new Vector2(0.55f, 6.0f) };
                foreach (var L in layers)
                    Add(Geo.Cone(L.x * s, 1.9f * s, 7), MathX.M(x, y + L.y * s, z, 0, r.Next() * 6.28f, 0), "leaves", new Color(t, t, t));
            }
            var CC = new[] { new Color(0.22f, 0.9f, 1f), new Color(1f, 0.24f, 0.95f), new Color(0.95f, 1f, 0.3f), new Color(1f, 0.32f, 0.55f) };
            void Cluster(float x, float y, float z, float s)
            {
                int n = 7 + Mathf.FloorToInt(r.Next() * 5);
                for (int i = 0; i < n; i++)
                {
                    Color c = CC[i == 0 ? 0 : Mathf.FloorToInt(r.Next() * (r.Next() < 0.6f ? 1 : CC.Length))];
                    float h = s * (0.5f + r.Next() * 0.9f), w = s * (0.1f + r.Next() * 0.12f);
                    float ox = (r.Next() - 0.5f) * s * 0.8f, oz = (r.Next() - 0.5f) * s * 0.8f;
                    float rx = (r.Next() - 0.5f) * 1.1f, ry = r.Next() * 6.28f, rz = (r.Next() - 0.5f) * 1.1f;
                    Add(Geo.Octa(), MathX.M(x + ox, y + h * 0.45f, z + oz, rx, ry, rz, w, h, w), "crystal", c, 1f);
                }
            }
            // canyon walls on both sides of the ravine, stacked boulders from deep below to the rim
            foreach (int side in new[] { -1, 1 })
            {
                for (int z = -110; z <= 110; z += 9)
                {
                    foreach (var ys in new[] { new Vector2(-34, 16), new Vector2(-14, 15), new Vector2(6, 13), new Vector2(20, 10) })
                    {
                        float x = side * (42 + r.Next() * 8 + (ys.x > 10 ? 6 : 0));
                        float yy = ys.x + r.Next() * 4, zz = z + r.Next() * 5;
                        float sx = ys.y * (0.8f + r.Next() * 0.5f), sy = ys.y * (0.7f + r.Next() * 0.5f), sz = ys.y * (0.8f + r.Next() * 0.5f);
                        Rock(x, yy, zz, sx, sy, sz, 0.78f + r.Next() * 0.25f);
                    }
                    if (r.Next() < 0.35f)
                    {
                        float cx = side * (27 + r.Next() * 3), cy = -20 + r.Next() * 30, cz = z + r.Next() * 6, cs = 2 + r.Next() * 2;
                        Cluster(cx, cy, cz, cs);
                    }
                }
                // rim forest
                for (int i = 0; i < 45; i++)
                {
                    float px = side * (46 + r.Next() * 30), pz = -110 + r.Next() * 220, ps = 1.4f + r.Next() * 1.2f;
                    Pine(px, 26, pz, ps);
                }
            }
            // far ends of the canyon
            foreach (int end in new[] { -1, 1 })
                for (int x = -50; x <= 50; x += 12)
                {
                    float rx = x + r.Next() * 6, ry = -10 + r.Next() * 20, rz = end * (130 + r.Next() * 15);
                    float sx = 16 + r.Next() * 10, sy = 30 + r.Next() * 20;
                    Rock(rx, ry, rz, sx, sy, 14, 0.75f);
                }
            // rock island crystals
            foreach (var c in World.Crystals) Cluster(c[0], c[1], c[2], c[3]);
            return mb.Build("Scenery", true);
        }

        // ---------------- small helpers ----------------
        public static Mesh SkyDome(float radius)
        {
            var mb = new MeshBuilder();
            int seg = 24, rings = 12;
            for (int y = 0; y < rings; y++)
                for (int x = 0; x < seg; x++)
                {
                    Vector3 P(int xi, int yi)
                    {
                        float th = yi / (float)rings * Mathf.PI, ph = xi / (float)seg * Mathf.PI * 2f;
                        return new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * radius;
                    }
                    // inward facing (drawn from inside)
                    mb.Tri(P(x, y), P(x + 1, y), P(x + 1, y + 1), Color.white, Vector4.zero);
                    mb.Tri(P(x, y), P(x + 1, y + 1), P(x, y + 1), Color.white, Vector4.zero);
                }
            return mb.Build("Sky", true);
        }

        public static Mesh Disc(float r, int seg)
        {
            var mb = new MeshBuilder();
            mb.AddGeo(Geo.Disc(r, seg), Matrix4x4.identity, Color.white, Vector4.zero);
            return mb.Build("Disc");
        }

        public static Mesh Sphere(float r)
        {
            var mb = new MeshBuilder();
            mb.AddGeo(Geo.Ico(r, 2), Matrix4x4.identity, Color.white, Vector4.zero);
            return mb.Build("Sphere");
        }

        /// <summary>A Unity-space quad in the XY plane centred on the origin (for UI panels; drawn double-sided).</summary>
        public static Mesh Quad(float w, float h)
        {
            var mesh = new Mesh { name = "Quad" };
            mesh.SetVertices(new List<Vector3> { new Vector3(-w / 2, -h / 2, 0), new Vector3(-w / 2, h / 2, 0), new Vector3(w / 2, h / 2, 0), new Vector3(w / 2, -h / 2, 0) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            mesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>A quad anchored at its left edge (so scaling X grows it to the right).</summary>
        public static Mesh BarQuad(float w, float h)
        {
            var mesh = new Mesh { name = "Bar" };
            mesh.SetVertices(new List<Vector3> { new Vector3(0, -h / 2, 0), new Vector3(0, h / 2, 0), new Vector3(w, h / 2, 0), new Vector3(w, -h / 2, 0) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            mesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
