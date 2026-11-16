using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Wrecked
{
    public partial class Game
    {
        // ---------------- headset input ----------------
        struct XrHand { public bool ok; public Vector3 pos; public Quaternion q; public bool trigger, grip; public InputDevice device; }
        readonly XrHand[] xrHands = new XrHand[2];
        Vector3 xrHead = new Vector3(0f, 1.6f, 0f);
        Quaternion xrHeadQ = Quaternion.identity;
        float xrStickX;
        bool firstVRFrame;
        float originRetryT;

        static readonly List<XRInputSubsystem> inputSubsystems = new List<XRInputSubsystem>();

        void EnsureFloorOrigin()
        {
            originRetryT -= Time.unscaledDeltaTime;
            if (originRetryT > 0f) return;
            originRetryT = 2f;
            SubsystemManager.GetSubsystems(inputSubsystems);
            foreach (var s in inputSubsystems)
                if (s.running && s.GetTrackingOriginMode() != TrackingOriginModeFlags.Floor)
                    s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
        }

        /// <summary>Read head and controllers. Everything is converted into game space (see Conv).</summary>
        bool ReadXR()
        {
            EnsureFloorOrigin();
            var head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!head.isValid) head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid) return false;
            if (!head.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 hp) && !head.TryGetFeatureValue(CommonUsages.devicePosition, out hp)) return false;
            if (!head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion hq)) head.TryGetFeatureValue(CommonUsages.deviceRotation, out hq);
            xrHead = Conv.U(hp); xrHeadQ = Conv.UQ(hq);
            for (int k = 0; k < 2; k++)
            {
                var d = InputDevices.GetDeviceAtXRNode(k == 0 ? XRNode.LeftHand : XRNode.RightHand);
                var h = new XrHand { device = d };
                if (d.isValid && d.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 p))
                {
                    h.ok = true; h.pos = Conv.U(p);
                    h.q = d.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion q) ? Conv.UQ(q) : Quaternion.identity;
                    d.TryGetFeatureValue(CommonUsages.triggerButton, out h.trigger);
                    d.TryGetFeatureValue(CommonUsages.gripButton, out h.grip);
                    if (k == 1 && d.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)) xrStickX = stick.x;
                }
                xrHands[k] = h;
            }
            return true;
        }

        void PulseHand(int k, float amp, float seconds)
        {
            if (human.kind != "vr" || k < 0 || k > 1) return;
            var d = human.hands[k].device;
            if (d.isValid) d.SendHapticImpulse(0u, Mathf.Clamp01(amp), seconds);
        }
        void PulseAll(float amp, float seconds) { PulseHand(0, amp, seconds); PulseHand(1, amp, seconds); }

        // ---------------- arm movement ----------------
        Vector3 HeadWorld() => MathX.RotY(human.headL, human.yaw) + human.P;

        Vector3 HandTarget(Hand h)
        {
            Vector3 o = MathX.RotY(h.L, human.yaw) + human.P;
            Vector3 hw = HeadWorld();
            Vector3 d = o - hw;
            float len = d.magnitude;
            if (len > Cfg.ArmMax) o = hw + d * (Cfg.ArmMax / len);
            return o;
        }

        readonly Contact humanContact = new Contact();

        void CollideHuman(bool withVel, float dt, bool friction = true)
        {
            var H = human;
            humanContact.Reset();
            for (int it = 0; it < 2; it++)
                foreach (var s in Cfg.HumanSph)
                {
                    Vector3 p = HeadWorld(); p.y += s.dy;
                    Vector3 before = p;
                    World.PushSphere(ref p, s.r, humanContact, ref H.vel, withVel);
                    H.P += p - before;
                }
            H.grounded = humanContact.ground;
            if (withVel && friction && humanContact.ground) { float f = Mathf.Exp(-9f * dt); H.vel.x *= f; H.vel.z *= f; }
        }

        void Fling()
        {
            var H = human; var hs = H.hist;
            if (hs.Count < 2) return;
            var a = hs[0]; var b = hs[hs.Count - 1];
            float dt = b.t - a.t;
            if (dt < 0.015f) return;
            Vector3 v = (b.p - a.p) / dt;
            if (v.magnitude < Cfg.FlingMin) { H.vel = v; return; }
            v *= Cfg.FlingMul;
            if (v.magnitude > Cfg.FlingCap) v = MathX.WithLength(v, Cfg.FlingCap);
            H.vel = v;
        }

        void VrStep(float dt)
        {
            var H = human;
            H.headL = xrHead;
            float flail = 0f;
            for (int k = 0; k < 2; k++)
            {
                var h = H.hands[k]; var s = xrHands[k];
                h.ok = s.ok; if (!s.ok) continue;
                flail += Mathf.Min(6f, Vector3.Distance(h.Lp, s.pos) / Mathf.Max(dt, 1e-3f)); h.Lp = s.pos;
                h.L = s.pos; h.q = s.q; h.trigger = s.trigger; h.gripWas = h.grip; h.grip = s.grip; h.device = s.device;
            }
            // shaking your arms hard shakes off dizziness
            if (H.ko > 0f) AddStruggle(Mathf.Max(0f, flail - 2f) * 0.15f * dt); else H.struggle = 0f;
            // snap turn on the right stick
            if (Mathf.Abs(xrStickX) > 0.7f && !H.snapLatch)
            {
                H.snapLatch = true;
                Vector3 hw = HeadWorld();
                H.yaw -= Mathf.Sign(xrStickX) * Mathf.PI / 6f;
                H.P = hw - MathX.RotY(H.headL, H.yaw);
                foreach (var h in H.hands) { h.anchored = false; h.pos = HandTarget(h); h.prev = h.pos; }
            }
            else if (Mathf.Abs(xrStickX) < 0.3f) H.snapLatch = false;
            H.noGrab -= dt;
            bool stunned = H.ko > 0f || H.heldBy != null;
            if (H.heldBy != null)
            {
                foreach (var h in H.hands) { h.anchored = false; h.pos = HandTarget(h); }
                H.head = HeadWorld(); H.pos = H.head + new Vector3(0, -0.1f, 0);
                return;
            }

            // phase 1: hands already holding a surface move the body
            int n1 = 0; Vector3 D = Vector3.zero;
            foreach (var h in H.hands)
            {
                if (!h.ok || !h.anchored) continue;
                if (h.dyn != null) { h.A = World.ToWorld(h.dyn, h.lA); h.n = World.DirToWorld(h.dyn, h.ln); }
                h.T = HandTarget(h);
                Vector3 rel = h.T - h.A;
                float outward = Vector3.Dot(rel, h.n), dist = rel.magnitude;
                if (stunned || dist > Cfg.Unstick || (outward > Cfg.ReleaseEps && !h.grip)) { h.anchored = false; continue; }
                rel -= h.n * outward;
                h.A += rel * (h.grip ? 0f : 0.02f);
                if (h.dyn != null) h.lA = World.ToLocal(h.dyn, h.A);
                D += h.A - h.T; n1++;
            }
            if (n1 > 0)
            {
                H.P += D / n1; CollideHuman(false, dt); H.vel = Vector3.zero;
            }
            else
            {
                if (H.wasAnch) Fling();
                H.vel.y -= Cfg.Grav * dt;
                H.P += H.vel * dt;
                CollideHuman(true, dt);
            }
            // phase 2: free hands sweep toward the controller and grab what they hit
            int n2 = 0; D = Vector3.zero;
            foreach (var h in H.hands)
            {
                if (!h.ok || h.anchored) continue;
                h.T = HandTarget(h);
                bool can = h.holding == null && !stunned && H.noGrab <= 0f;
                if (can && World.Sweep(h.pos, h.T, Cfg.HandR, out var hit))
                {
                    h.anchored = true; h.A = hit.p; h.n = hit.n;
                    h.dyn = hit.box.dyn ? hit.box : null;
                    if (h.dyn != null) { h.lA = World.ToLocal(h.dyn, h.A); h.ln = World.DirToLocal(h.dyn, h.n); }
                    D += h.A - h.T; n2++;
                    float sp = H.vel.magnitude;
                    PulseHand(h.k, Mathf.Min(1f, 0.15f + sp * 0.08f), 0.025f);
                    if (sp > 2f) Play("tap", h.A);
                }
                else h.pos = h.T;
            }
            if (n2 > 0)
            {
                H.P += D / n2; CollideHuman(false, dt); H.vel = Vector3.zero;
                foreach (var h in H.hands)
                {
                    if (!h.ok || h.anchored) continue;
                    h.T = HandTarget(h);
                    if (h.holding == null && World.Sweep(h.pos, h.T, Cfg.HandR, out var hit3)) h.pos = hit3.p; else h.pos = h.T;
                }
            }
            bool any = false;
            foreach (var h in H.hands) if (h.anchored) { h.pos = h.A; any = true; }
            if (any)
            {
                H.hist.Add(new HistSample(now, H.P));
                while (H.hist.Count > 2 && now - H.hist[0].t > Cfg.HistWindow) H.hist.RemoveAt(0);
            }
            else H.hist.Clear();
            H.wasAnch = any;
            H.head = HeadWorld(); H.pos = H.head + new Vector3(0, -0.1f, 0);
        }

        void VrGrabs()
        {
            var H = human;
            if (H.ko > 0f || H.heldBy != null || H.outed) { foreach (var h in H.hands) DropHeld(h); return; }
            foreach (var h in H.hands)
            {
                if (!h.ok) continue;
                if (h.grip && !h.gripWas && !h.anchored && h.holding == null)
                {
                    Ape best = null; float bd = Cfg.Reach;
                    foreach (var t in actors) { if (t == H || !t.active || t.outed) continue; float d = Vector3.Distance(h.pos, t.pos); if (d < bd) { bd = d; best = t; } }
                    if (best != null && Grab(H, h, best)) PulseHand(h.k, 0.6f, 0.06f);
                }
                else if (!h.grip && h.holding != null)
                {
                    Vector3 v = h.vel * Cfg.ThrowMul; v.y += 1f;
                    if (v.magnitude > Cfg.ThrowCap) v = MathX.WithLength(v, Cfg.ThrowCap);
                    if (h.holding.ko <= 0f) v *= Cfg.AwakeThrow;
                    Release(h.holding, v);
                }
            }
        }

        void AddStruggle(float v)
        {
            var H = human;
            if (!(H.ko > 0f) || H.outed || phase != Phase.Fight) return;
            H.struggle += v;
            if (H.struggle < 1f) return;
            H.struggle = 0f;
            H.ko = 0f; H.dizzy = 30f;
            Feed("You shook it off!"); Play("grab"); PulseAll(0.8f, 0.12f);
        }

        // ---------------- keyboard + mouse (no headset) ----------------
        int pcPunchHand = 1;

        Vector3 PcHandPos(int k)
        {
            var h = human.hands[k]; float side = k == 0 ? -1f : 1f;
            float x = side * 0.24f, y = -0.22f, z = -0.38f;
            if (h.punchT > 0f)
            {
                float t = 1f - h.punchT / 0.32f;
                float f = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                x = side * (0.24f - 0.16f * f); y = -0.22f + 0.12f * f; z = -0.38f - 0.62f * f;
            }
            else if (DeskInput.RightHeld || h.holding != null) { x = side * 0.14f; y = -0.12f; z = -0.72f; }
            return MathX.EulerYXZ(human.pitch, human.yaw, 0f) * new Vector3(x, y, z) + human.head;
        }

        void PcStep(float dt)
        {
            var H = human;
            H.headL = new Vector3(0f, 1f, 0f);
            DeskInput.UpdateCursor();
            Vector2 md = DeskInput.MouseDelta();
            H.yaw -= md.x * 0.0025f; H.pitch = Mathf.Clamp(H.pitch - md.y * 0.0025f, -1.3f, 1.3f);
            if (DeskInput.Held(DeskKey.Q)) H.yaw += 2.2f * dt;
            if (DeskInput.Held(DeskKey.E)) H.yaw -= 2.2f * dt;
            if (DeskInput.AnyPressed()) AddStruggle(0.12f);
            if (DeskInput.LeftPressed())
            {
                var ph = H.hands[pcPunchHand];
                if (ph.punchT <= 0f) { ph.punchT = 0.32f; pcPunchHand = 1 - pcPunchHand; }
            }
            if (H.heldBy == null)
            {
                bool can = H.ko <= 0f && !H.outed;
                float f = can ? (DeskInput.Held(DeskKey.W) || DeskInput.Held(DeskKey.Up) ? 1f : 0f) - (DeskInput.Held(DeskKey.S) || DeskInput.Held(DeskKey.Down) ? 1f : 0f) : 0f;
                float s = can ? (DeskInput.Held(DeskKey.D) || DeskInput.Held(DeskKey.Right) ? 1f : 0f) - (DeskInput.Held(DeskKey.A) || DeskInput.Held(DeskKey.Left) ? 1f : 0f) : 0f;
                float sn = Mathf.Sin(H.yaw), cs = Mathf.Cos(H.yaw);
                float wx = -sn * f + cs * s, wz = -cs * f - sn * s;
                float wl = Mathf.Sqrt(wx * wx + wz * wz); if (wl > 0f) { wx /= wl; wz /= wl; }
                float k = H.grounded ? (H.noGrab > 0f ? 1.5f : 14f) : 2.5f;
                H.vel.x += (wx * 4.6f - H.vel.x) * Mathf.Min(1f, k * dt);
                H.vel.z += (wz * 4.6f - H.vel.z) * Mathf.Min(1f, k * dt);
                if (can && DeskInput.Held(DeskKey.Space) && H.grounded) { H.vel.y = 5.8f; H.grounded = false; }
                H.noGrab -= dt;
                H.vel.y -= Cfg.Grav * dt;
                H.P += H.vel * dt;
                CollideHuman(true, dt, false);
            }
            H.head = HeadWorld(); H.pos = H.head + new Vector3(0, -0.1f, 0);
            for (int k = 0; k < 2; k++) { var h = H.hands[k]; h.ok = true; if (h.punchT > 0f) h.punchT -= dt; h.pos = PcHandPos(k); }
            // grab with right mouse, throw where you look on release
            var R = H.hands[1];
            if (DeskInput.RightHeld && R.holding == null && H.ko <= 0f && !H.outed)
            {
                Ape best = null; float bd = 1f;
                foreach (var t in actors) { if (t == H || !t.active || t.outed) continue; float d = Vector3.Distance(R.pos, t.pos); if (d < bd) { bd = d; best = t; } }
                if (best != null) Grab(H, R, best);
            }
            else if (!DeskInput.RightHeld && R.holding != null)
            {
                Vector3 v = MathX.EulerYXZ(human.pitch + 0.25f, human.yaw, 0f) * new Vector3(0f, 0f, -1f);
                v *= R.holding.ko > 0f ? 6.5f : 6.5f * Cfg.AwakeThrow;
                Release(R.holding, v);
            }
        }
    }
}
