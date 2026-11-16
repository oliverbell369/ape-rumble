using System;
using UnityEngine;

namespace Wrecked
{
    // All gameplay math runs in the same right-handed coordinates as the web version
    // (x right, y up, -z forward). Positions and rotations are converted to Unity's
    // left-handed space only when they are drawn or read from the headset (see Conv).
    public static class Cfg
    {
        public const float Grav = 9.8f;
        public const float RoundTime = 120f;
        public const float FallY = -8f;
        public const float FogNear = 26f, FogFar = 150f;
        public static readonly Color SkyTop = Hex(0x5d93c4);
        public static readonly Color Fog = Hex(0xa9c3c4);
        public static readonly Vector3 LightDir = new Vector3(0.45f, 0.8f, 0.35f).normalized;

        // arm movement
        public const float HandR = 0.06f, ArmMax = 1.35f, Unstick = 0.7f, ReleaseEps = 0.003f, HistWindow = 0.1f;
        public const float FlingMin = 0.4f, FlingMul = 1.2f, FlingCap = 7.5f;

        // fighting
        public const float SlapMin = 2.2f, SlapMul = 0.75f, SlapCap = 9f, SlapUp = 1.6f, DizzyPerSpeed = 3.2f, KoTime = 3.5f;
        public const float ThrowMul = 0.9f, ThrowCap = 7f, AwakeThrow = 0.6f, HoldMax = 8f, Reach = 0.5f;
        public const int GripBreak = 3;

        public static readonly Sph[] HumanSph = { new Sph(0f, 0.2f), new Sph(-0.38f, 0.28f) };
        public static readonly Sph[] BotSph = { new Sph(0f, 0.3f), new Sph(-0.27f, 0.28f) };

        // raw (display) colour from a hex value; never gamma-converted
        public static Color Hex(int h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f, 1f);
        public static Vector4 V4(Color c) => new Vector4(c.r, c.g, c.b, c.a);
    }

    public struct Sph
    {
        public float dy, r;
        public Sph(float dy, float r) { this.dy = dy; this.r = r; }
    }

    public static class ApeData
    {
        public static readonly string[] Names = { "Rex", "Bonk", "Mojo", "Pip", "Tango", "Biscuit" };
        public static readonly Color[] Fur =
        {
            new Color(0.5f, 0.34f, 0.22f), new Color(0.24f, 0.21f, 0.2f), new Color(0.74f, 0.42f, 0.2f),
            new Color(0.58f, 0.57f, 0.56f), new Color(0.82f, 0.64f, 0.34f), new Color(0.88f, 0.84f, 0.76f),
        };
        public static readonly Color[] Band =
        {
            new Color(0.9f, 0.18f, 0.15f), new Color(0.16f, 0.45f, 0.95f), new Color(0.2f, 0.8f, 0.3f),
            new Color(1f, 0.8f, 0.15f), new Color(0.95f, 0.35f, 0.75f), new Color(0.2f, 0.85f, 0.9f),
        };
        public static readonly Color[] Chip =
        {
            Cfg.Hex(0xe62e26), Cfg.Hex(0x2973f2), Cfg.Hex(0x33cc4d), Cfg.Hex(0xffcc26), Cfg.Hex(0xf259bf), Cfg.Hex(0x33d9e6),
        };
    }

    /// <summary>Same deterministic generator as the web version (mulberry32).</summary>
    public class Rng
    {
        uint a;
        public Rng(uint seed) { a = seed; }
        public float Next()
        {
            unchecked
            {
                a += 0x6D2B79F5u;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (float)((t ^ (t >> 14)) / 4294967296.0);
            }
        }
    }

    public static class MathX
    {
        public static float Rand(float a, float b) => UnityEngine.Random.Range(a, b);
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);

        public static Vector3 RotY(Vector3 v, float yaw)
        {
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            return new Vector3(v.x * c + v.z * s, v.y, -v.x * s + v.z * c);
        }

        public static float Hash2(float a, float b)
        {
            double s = Math.Sin(a * 127.1 + b * 311.7) * 43758.5453;
            return (float)(s - Math.Floor(s));
        }

        public static Vector3 WithLength(Vector3 v, float len)
        {
            float m = v.magnitude;
            return m > 1e-6f ? v * (len / m) : Vector3.zero;
        }

        // quaternion for a rotation of `rad` radians about a unit axis (right-handed, like three.js)
        public static Quaternion AxisAngle(Vector3 axis, float rad)
        {
            float s = Mathf.Sin(rad * 0.5f);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(rad * 0.5f));
        }

        // three.js Quaternion.setFromUnitVectors
        public static Quaternion FromUnitVectors(Vector3 from, Vector3 to)
        {
            float r = Vector3.Dot(from, to) + 1f;
            Quaternion q;
            if (r < 1e-6f)
            {
                r = 0f;
                if (Mathf.Abs(from.x) > Mathf.Abs(from.z)) q = new Quaternion(-from.y, from.x, 0f, r);
                else q = new Quaternion(0f, -from.z, from.y, r);
            }
            else
            {
                Vector3 c = Vector3.Cross(from, to);
                q = new Quaternion(c.x, c.y, c.z, r);
            }
            return Normalize(q);
        }

        public static Quaternion Normalize(Quaternion q)
        {
            float l = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (l < 1e-9f) return Quaternion.identity;
            return new Quaternion(q.x / l, q.y / l, q.z / l, q.w / l);
        }

        static readonly Vector3 X = new Vector3(1, 0, 0), Y = new Vector3(0, 1, 0), Z = new Vector3(0, 0, 1);

        /// <summary>three.js Euler with order "XYZ" as a quaternion.</summary>
        public static Quaternion EulerXYZ(float rx, float ry, float rz) => AxisAngle(X, rx) * AxisAngle(Y, ry) * AxisAngle(Z, rz);
        /// <summary>three.js Euler with order "YXZ" as a quaternion.</summary>
        public static Quaternion EulerYXZ(float rx, float ry, float rz) => AxisAngle(Y, ry) * AxisAngle(X, rx) * AxisAngle(Z, rz);
        public static Quaternion YawQ(float yaw) => AxisAngle(Y, yaw);

        /// <summary>Translation * rotation * scale (three.js Matrix4.compose).</summary>
        public static Matrix4x4 Compose(Vector3 p, Quaternion q, Vector3 s) => Matrix4x4.TRS(p, q, s);

        /// <summary>three.js-style helper M(x,y,z, rx,ry,rz, sx,sy,sz) with Euler XYZ.</summary>
        public static Matrix4x4 M(float x = 0, float y = 0, float z = 0, float rx = 0, float ry = 0, float rz = 0, float sx = 1, float sy = 1, float sz = 1)
            => Matrix4x4.TRS(new Vector3(x, y, z), EulerXYZ(rx, ry, rz), new Vector3(sx, sy, sz));
    }

    /// <summary>Conversions between the game's right-handed space and Unity's left-handed space.</summary>
    public static class Conv
    {
        public static Vector3 U(Vector3 v) => new Vector3(v.x, v.y, -v.z);
        public static Quaternion UQ(Quaternion q) => new Quaternion(-q.x, -q.y, q.z, q.w);
    }
}
