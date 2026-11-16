using UnityEngine;

namespace Wrecked
{
    public partial class Game
    {
        readonly Contact botContact = new Contact();

        Ape NearestTarget(Ape b)
        {
            Ape best = null; float bd = 1e9f;
            foreach (var t in actors)
            {
                if (t == b || !t.active || t.outed || t.heldBy != null) continue;
                float d = Vector3.Distance(t.pos, b.pos) - (t.ko > 0f ? 3f : 0f) + b.grudge[Mathf.Clamp(t.i, 0, 7)];
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }

        bool GroundAt(Vector3 p, float up = 1.5f, float down = 4f)
        {
            var a = new Vector3(p.x, p.y + up, p.z); var b = new Vector3(p.x, p.y - down, p.z);
            return World.Sweep(a, b, 0.1f, out var h, true) && h.n.y > 0.6f;
        }

        bool SafeHop(Ape b, float vx, float vy, float vz)
        {
            float tf = (vy + Mathf.Sqrt(vy * vy + 2f * Cfg.Grav * 0.6f)) / Cfg.Grav;
            return GroundAt(new Vector3(b.pos.x + vx * tf, b.pos.y, b.pos.z + vz * tf), 2.5f, 4f);
        }

        void BotThink(Ape b)
        {
            b.think = MathX.Rand(0.2f, 0.35f);
            if (b.holding != null)
            {
                // carry to the nearest edge
                Vector3 best = Vector3.zero; float bd = 1e9f;
                foreach (var e in World.EdgePoints) { float d = Vector3.Distance(e, b.pos); if (d < bd) { bd = d; best = e; } }
                b.goalApe = null; b.goalPos = best; b.hasGoal = true; b.target = null;
                return;
            }
            if (now > b.grudgeT) { b.grudgeT = now + MathX.Rand(3f, 6f); for (int k = 0; k < b.grudge.Length; k++) b.grudge[k] = MathX.Rand(0f, 3f); }
            b.target = NearestTarget(b);
            b.goalApe = b.target; b.hasGoal = b.target != null;
        }

        void UpdateBot(Ape b, float dt)
        {
            if (!b.active || b.outed) return;
            b.think -= dt; b.hopCD -= dt; b.slapCD -= dt; b.blocked -= dt;
            bool live = phase == Phase.Fight;
            foreach (var h in b.hands) h.prev = h.pos;
            if (b.heldBy != null)
            {
                // a grabbed bot that's awake slaps whoever is holding it
                b.grounded = false; b.hang = false;
                if (b.ko <= 0f && live && b.slapT < 0f && b.slapCD <= 0f)
                {
                    b.slapT = 0f; b.slapHand = 0; b.slapDir = (b.heldBy.pos - b.pos).normalized; b.slapCD = MathX.Rand(0.45f, 0.8f) / b.skill;
                }
                AnimBot(b, dt); return;
            }
            b.hangCool -= dt;
            if (b.hang)
            {
                b.hangT -= dt;
                b.pos = b.hangPos; b.vel = Vector3.zero; b.grounded = false;
                if (b.ko > 0f) { b.hang = false; b.hangCool = 2f; }
                else if (b.hangT <= 0f && live)
                {
                    // pull up over the edge
                    b.hang = false; b.hangCool = 1.2f;
                    b.pos = new Vector3(b.hangLedge.x + b.hangDir.x * 0.45f, b.hangLedge.y + 0.62f, b.hangLedge.z + b.hangDir.z * 0.45f);
                    b.vel = new Vector3(b.hangDir.x * 1.5f, 2.2f, b.hangDir.z * 1.5f);
                }
                AnimBot(b, dt);
                return;
            }
            if (b.think <= 0f && b.ko <= 0f) BotThink(b);
            bool canAct = live && b.ko <= 0f;
            if (canAct && b.hasGoal)
            {
                Vector3 g = b.Goal;
                float dx = g.x - b.pos.x, dz = g.z - b.pos.z, dh = Mathf.Sqrt(dx * dx + dz * dz), dy = g.y - b.pos.y;
                if (b.holding != null && dh < 1.6f)
                {
                    // throw outward off the edge
                    Vector3 v = new Vector3(dx, 0f, dz).normalized * 6f; v.y = 3f;
                    Release(b.holding, v); b.slapCD = 0.8f;
                }
                else if (b.grounded && b.hopCD <= 0f && dh > (b.holding != null ? 0.5f : 1.0f))
                {
                    float sp = Mathf.Min(5.6f, 1.8f + dh * 0.45f) * b.skill * (b.holding != null ? 0.7f : 1f);
                    float vy = Mathf.Clamp(3.2f + Mathf.Max(0f, dy) * 1.5f + (b.blocked > 0f ? 3.5f : 0f), 3f, 9f);
                    float vx = dx / dh * sp, vz = dz / dh * sp;
                    if (!SafeHop(b, vx, vy, vz))
                    {
                        vx *= 0.4f; vz *= 0.4f; vy = Mathf.Min(vy, 4f);
                        if (!SafeHop(b, vx, vy, vz)) { vx = vz = 0f; vy = 0f; }
                    }
                    if (vy > 0f) { b.vel = new Vector3(vx, vy, vz); b.hopCD = MathX.Rand(0.12f, 0.4f); }
                    else b.hopCD = 0.3f;
                }
                var t = b.target;
                if (t != null && b.holding == null)
                {
                    float d = Vector3.Distance(t.pos, b.pos);
                    if (t.ko > 0f && d < 1.1f) { Grab(b, b.hands[1], t); b.think = 0f; }
                    else if (d < 1.25f && b.slapCD <= 0f && b.slapT < 0f)
                    {
                        if (Random.value < 0.12f && d < 0.95f) { Grab(b, b.hands[1], t); b.holdT = 0f; b.think = 0f; }
                        else { b.slapT = 0f; b.slapHand = 1 - b.slapHand; b.slapDir = (t.pos - b.pos).normalized; b.slapCD = MathX.Rand(0.45f, 0.95f) / b.skill; }
                    }
                }
            }
            b.vel.y -= Cfg.Grav * dt;
            b.pos += b.vel * dt;
            botContact.Reset();
            for (int it = 0; it < 2; it++)
                foreach (var s in Cfg.BotSph)
                {
                    Vector3 p = b.pos; p.y += s.dy; Vector3 before = p;
                    World.PushSphere(ref p, s.r, botContact, ref b.vel, true);
                    b.pos += p - before;
                }
            b.grounded = botContact.ground;
            if (b.grounded) b.mayHang = Random.value < 0.75f * b.skill;
            else if (b.mayHang && b.vel.y < -1f && b.hangCool <= 0f && b.ko <= 0f && b.holding == null) TryLedge(b);
            if (botContact.wall && !botContact.ground) b.blocked = 0.7f;
            if (b.grounded) { float f = Mathf.Exp(-(b.thrownT > 0f || b.ko > 0f ? 3f : 12f) * dt); b.vel.x *= f; b.vel.z *= f; }
            // face where we're going or who we're fighting
            float fx = b.vel.x, fz = b.vel.z;
            if (b.target != null && b.ko <= 0f) { fx = b.target.pos.x - b.pos.x; fz = b.target.pos.z - b.pos.z; }
            if (b.ko <= 0f && fx * fx + fz * fz > 0.04f)
            {
                float d = Mathf.Atan2(-fx, -fz) - b.yaw; d = Mathf.Atan2(Mathf.Sin(d), Mathf.Cos(d));
                b.yaw += d * Mathf.Min(1f, 8f * dt);
            }
            AnimBot(b, dt);
        }

        void TryLedge(Ape b)
        {
            // look for a ledge top just above and in front of us (back the way we came)
            Vector3 dir = new Vector3(-b.vel.x, 0f, -b.vel.z);
            if (dir.sqrMagnitude < 0.01f) dir = new Vector3(-b.pos.x, 0f, -b.pos.z);
            dir.Normalize();
            float px = b.pos.x + dir.x * 0.6f, pz = b.pos.z + dir.z * 0.6f;
            if (!World.Sweep(new Vector3(px, b.pos.y + 1f, pz), new Vector3(px, b.pos.y - 0.1f, pz), 0.05f, out var h, true)) return;
            if (h.n.y < 0.6f || h.p.y < b.pos.y + 0.15f) return;
            b.hang = true; b.hangT = MathX.Rand(0.9f, 2.2f); b.mayHang = false;
            b.hangDir = dir; b.hangLedge = h.p;
            b.hangPos = new Vector3(h.p.x - dir.x * 0.5f, h.p.y - 0.62f, h.p.z - dir.z * 0.5f);
            b.yaw = Mathf.Atan2(-dir.x, -dir.z);
            b.pos = b.hangPos; b.vel = Vector3.zero;
            Play("tap", b.pos, 2f);
        }

        void AnimBot(Ape b, float dt)
        {
            float sn = Mathf.Sin(b.yaw), cs = Mathf.Cos(b.yaw);
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f; var h = b.hands[k];
                if (b.ko > 0f || (b.heldBy != null && !(b.slapT >= 0f && k == b.slapHand)))
                {
                    h.pos = b.pos + MathX.RotY(new Vector3(side * 0.6f, -0.35f + Mathf.Sin(now * 5f + k) * 0.05f, 0.1f), b.yaw);
                }
                else if (b.slapT >= 0f && k == b.slapHand)
                {
                    float t = b.slapT / 0.24f;
                    Vector3 back = b.pos + MathX.RotY(new Vector3(side * 0.55f, 0.25f, 0.25f), b.yaw);
                    Vector3 fwd = b.pos + b.slapDir * 0.95f;
                    float e = t < 0.35f ? 0f : Mathf.Min(1f, (t - 0.35f) / 0.45f);
                    h.pos = Vector3.LerpUnclamped(back, fwd, e * e);
                    if (t >= 1f) b.slapT = -1f;
                }
                else if (b.hang)
                {
                    h.pos = new Vector3(b.hangLedge.x + b.hangDir.z * side * 0.24f, b.hangLedge.y + 0.05f, b.hangLedge.z - b.hangDir.x * side * 0.24f);
                }
                else if (b.holding != null && k == 1)
                {
                    h.pos = new Vector3(b.pos.x - sn * 0.45f, b.pos.y + 0.65f, b.pos.z - cs * 0.45f);
                }
                else if (b.grounded)
                {
                    float sw = Mathf.Sin(now * 9f + k * Mathf.PI) * 0.08f * (new Vector2(b.vel.x, b.vel.z).magnitude > 0.5f ? 1f : 0.2f);
                    h.pos = b.pos + MathX.RotY(new Vector3(side * 0.42f, -0.5f, -0.22f + sw), b.yaw);
                }
                else
                {
                    h.pos = b.pos + MathX.RotY(new Vector3(side * 0.62f, 0.15f + Mathf.Sin(now * 14f + k) * 0.1f, 0.05f), b.yaw);
                }
            }
            if (b.slapT >= 0f) b.slapT += dt;
            float inv = 1f / Mathf.Max(dt, 1e-3f);
            foreach (var h in b.hands) h.vel = (h.pos - h.prev) * inv;
        }
    }
}
