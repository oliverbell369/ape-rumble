using UnityEngine;

namespace Wrecked
{
    public partial class Game
    {
        // ---------------- slaps ----------------
        void HitApe(Ape attacker, Ape target, Vector3 hv, float speed, int from)
        {
            if (!target.active || target.outed || target == attacker) return;
            int key = attacker.i * 10 + from;
            if (target.hitCD.TryGetValue(key, out float until) && until > now) return;
            target.hitCD[key] = now + 0.35f;
            float s = Mathf.Min(speed, Cfg.SlapCap);
            Vector3 v = MathX.WithLength(hv, s * Cfg.SlapMul);
            v.y = Mathf.Max(v.y, 0f) + Cfg.SlapUp * (s / 6f);
            Push(target, v);
            target.dizzy += s * Cfg.DizzyPerSpeed * (target.ko > 0f ? 0.3f : 1f);
            target.lastHit = now;
            target.tiltV.x += hv.x * 0.9f; target.tiltV.y += hv.z * 0.9f;
            if (target.heldBy != null && target.heldBy != attacker) Release(target, null);
            if (target == human) { PulseAll(Mathf.Min(1f, 0.3f + s * 0.08f), 0.08f); flash = 0.25f; }
            if (attacker == human) PulseHand(from, Mathf.Min(1f, 0.3f + s * 0.1f), 0.05f);
            Play(s > 6f ? "bonk" : "slap", target == human ? (Vector3?)null : target.pos, 0.6f + s * 0.06f);
            // slapping an ape that is holding someone loosens its grip
            if (IsHolding(target))
            {
                target.gripHits += s > 6f ? 2 : 1;
                if (target.gripHits >= Cfg.GripBreak) { DropAll(target); Feed($"{target.name} lost their grip!"); }
            }
            if (target.dizzy >= 100f && target.ko <= 0f)
            {
                target.ko = Cfg.KoTime; DropAll(target);
                Feed(target == human ? "You are dizzy!" : $"{target.name} is dizzy!");
                Play("ko", target == human ? (Vector3?)null : target.pos);
            }
        }

        bool IsHolding(Ape a)
        {
            if (a == human) { foreach (var h in human.hands) if (h.holding != null) return true; return false; }
            return a.holding != null;
        }

        void DropAll(Ape a)
        {
            if (a == human) { foreach (var h in human.hands) DropHeld(h); }
            else if (a.holding != null) Release(a.holding, null);
            a.gripHits = 0;
        }

        void Push(Ape a, Vector3 v)
        {
            if (a == human)
            {
                if (v.magnitude < 5.5f) { foreach (var h in human.hands) if (h.anchored && h.grip) { PulseAll(0.5f, 0.06f); return; } }
                foreach (var h in human.hands) h.anchored = false;
                human.vel += v; human.noGrab = 0.25f; human.wasAnch = false; human.hist.Clear();
            }
            else { a.vel += v; a.thrownT = 0.4f; a.hang = false; a.hangCool = 0.6f; }
        }

        // ---------------- grabbing ----------------
        bool Grab(Ape holder, Hand handRef, Ape target)
        {
            if (target.heldBy != null || target.outed || !target.active) return false;
            target.heldBy = holder; target.heldHand = handRef; target.heldT = 0f; holder.gripHits = 0;
            if (holder == human) handRef.holding = target; else holder.holding = target;
            Play("grab", target == human ? (Vector3?)null : target.pos);
            if (target == human) { foreach (var h in human.hands) h.anchored = false; human.hist.Clear(); }
            return true;
        }

        void ClearHold(Ape target)
        {
            var holder = target.heldBy;
            if (holder == null) return;
            if (holder == human) { foreach (var h in human.hands) if (h.holding == target) h.holding = null; }
            else if (holder.holding == target) holder.holding = null;
            target.heldBy = null; target.heldHand = null;
        }

        void Release(Ape target, Vector3? throwVel)
        {
            if (target.heldBy == null) return;
            ClearHold(target);
            if (throwVel.HasValue)
            {
                if (target == human) human.vel = throwVel.Value;
                else { target.vel = throwVel.Value; target.thrownT = 0.8f; }
                Play("throw", target.pos);
            }
        }

        void DropHeld(Hand h)
        {
            if (h.holding != null) Release(h.holding, null);
            h.holding = null;
        }

        void SetHeldPos(Ape a, Vector3 p)
        {
            if (a == human)
            {
                human.P += p - human.pos; human.vel = Vector3.zero;
                human.head = HeadWorld(); human.pos = human.head + new Vector3(0, -0.1f, 0);
            }
            else { a.pos = p; a.vel = Vector3.zero; }
        }

        void UpdateHeld(float dt)
        {
            foreach (var a in actors)
            {
                if (a.heldBy == null) continue;
                a.heldT += dt;
                Vector3 hp = a.heldHand.pos;
                SetHeldPos(a, new Vector3(hp.x, hp.y - 0.32f, hp.z));
                // safety: nobody gets carried forever
                if (a.heldT > Cfg.HoldMax)
                {
                    Vector3 t = a.heldHand.vel * 0.3f; t.y += 2f;
                    Release(a, t);
                }
            }
        }

        void BodyBump()
        {
            for (int i = 0; i < actors.Count; i++)
                for (int j = i + 1; j < actors.Count; j++)
                {
                    Ape a = actors[i], b = actors[j];
                    if (!a.active || !b.active || a.heldBy != null || b.heldBy != null || a.outed != b.outed) continue;
                    Vector3 d = b.pos - a.pos;
                    float len = d.magnitude;
                    if (len > 0.001f && len < 0.72f)
                    {
                        d *= (0.72f - len) / len * 0.5f;
                        if (a == human) human.P -= d; else a.pos -= d;
                        if (b == human) human.P += d; else b.pos += d;
                    }
                }
        }

        /// <summary>Hands that move fast into another ape slap it.</summary>
        void HandHits(Ape attacker, Hand[] handList)
        {
            for (int k = 0; k < handList.Length; k++)
            {
                var h = handList[k];
                if (h.holding != null) continue;
                // on a computer only a thrown punch counts; bots only hit with the hand that's slapping
                if (attacker == human && human.kind == "pc" && h.punchT <= 0f) continue;
                if (attacker.isBot && (k != attacker.slapHand || attacker.slapT < 0.08f)) continue;
                float sp = h.vel.magnitude * (attacker.isBot ? 0.5f : 1f);
                if (sp < Cfg.SlapMin) continue;
                foreach (var t in actors)
                {
                    if (t == attacker || !t.active || t.outed || t.heldBy == attacker) continue;
                    if ((h.pos - t.pos).sqrMagnitude < 0.25f) HitApe(attacker, t, h.vel, sp, k);
                }
            }
        }

        // ---------------- the swinging log ----------------
        void UpdateLog()
        {
            float th = LogA * Mathf.Sin(now * LogW), om = LogA * LogW * Mathf.Cos(now * LogW);
            logTheta = th;
            logC = new Vector3(0f, LogPivot.y - LogL * Mathf.Cos(th), LogL * Mathf.Sin(th));
            logV = new Vector3(0f, LogL * om * Mathf.Sin(th), LogL * om * Mathf.Cos(th));
            World.SetBoxRotX(World.LogBox, logC.x, logC.y, logC.z, -th);
            for (int k = 0; k < 2; k++) World.SetBoxRotX(World.RopeBoxes[k], k == 1 ? 1.6f : -1.6f, (logC.y + LogPivot.y) / 2f, logC.z / 2f, -th);
            // the log knocks apes flying (log axis is world X; it rotates about X)
            float c = Mathf.Cos(th), s = Mathf.Sin(th);
            foreach (var a in actors)
            {
                if (!a.active || a.outed || a.heldBy != null) continue;
                if (a == human) { bool riding = false; foreach (var h in human.hands) if (h.anchored && h.dyn != null) riding = true; if (riding) continue; }
                Vector3 d = a.pos - logC;
                float ly = d.y * c + d.z * s, lz = -d.y * s + d.z * c, lx = d.x;
                float ex = lx - Mathf.Clamp(lx, -LogHalf.x, LogHalf.x), ey = ly - Mathf.Clamp(ly, -LogHalf.y, LogHalf.y), ez = lz - Mathf.Clamp(lz, -LogHalf.z, LogHalf.z);
                if (ex * ex + ey * ey + ez * ez < 0.42f * 0.42f && logV.magnitude > 1f)
                {
                    if (a.hitCD.TryGetValue(-1, out float until) && until > now) continue;
                    a.hitCD[-1] = now + 0.6f;
                    Vector3 v = logV * 1.25f; v.y = Mathf.Max(v.y, 0f) + 3f;
                    Push(a, v);
                    a.dizzy += 30f; a.lastHit = now; a.tiltV.x += logV.x; a.tiltV.y += logV.z;
                    if (a.dizzy >= 100f && a.ko <= 0f) { a.ko = Cfg.KoTime; Play("ko", a.pos); }
                    Play("bonk", a == human ? (Vector3?)null : a.pos, 1.2f);
                    if (a == human) { PulseAll(1f, 0.15f); flash = 0.3f; }
                }
            }
        }
    }
}
