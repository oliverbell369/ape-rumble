using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Wrecked
{
    /// <summary>
    /// WRECKED bootstrap: builds the whole game from code and runs it. Put this on one empty GameObject
    /// in the scene (WRECKED > Set Up Project does that for you).
    /// Split across partial files: Game.Fight, Game.Player, Game.Bots, Game.Visuals.
    /// </summary>
    public partial class Game : MonoBehaviour
    {
        enum Phase { Countdown, Fight, End }

        // ---- round state ----
        Phase phase = Phase.Countdown;
        int round;
        float timeLeft = Cfg.RoundTime, countdown, endT;
        Ape winner;
        int lastCount = 4;
        readonly List<string> feedList = new List<string>();
        float feedT;
        float now;

        // ---- apes ----
        Ape human;
        readonly Ape[] allBots = new Ape[6];
        readonly List<Ape> actors = new List<Ape>();
        readonly List<Ape> bots = new List<Ape>();

        // ---- swinging log ----
        static readonly Vector3 LogPivot = new Vector3(0f, 9.9f, 0f);
        const float LogL = 9f, LogA = 1f, LogW = 1.15f;
        static readonly Vector3 LogHalf = new Vector3(2.3f, 0.42f, 0.42f);
        float logTheta;
        Vector3 logC, logV;

        Sfx sfx;
        bool xrWasActive;

        void Awake()
        {
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            World.Build();
            human = new Ape { isHuman = true, name = "You", active = false };
            human.Skin(0);
            for (int k = 0; k < 6; k++)
            {
                allBots[k] = new Ape { isBot = true, i = k, name = ApeData.Names[k], skill = MathX.Rand(0.8f, 1.05f) };
                allBots[k].Skin(k);
            }
            BuildVisuals();
            sfx = new Sfx(gameObject);
            SetupSlots();
            human.kind = XRSettings.isDeviceActive ? "vr" : "pc";
            xrWasActive = XRSettings.isDeviceActive;
            firstVRFrame = human.kind == "vr";
            human.active = true;
            StartRound();
        }

        void SetupSlots()
        {
            actors.Clear(); bots.Clear();
            human.i = 0; human.Skin(0); human.name = "You";
            actors.Add(human);
            for (int k = 1; k < 6; k++) { var b = allBots[k]; b.active = true; actors.Add(b); bots.Add(b); }
        }

        void Feed(string msg)
        {
            feedList.Insert(0, msg);
            while (feedList.Count > 3) feedList.RemoveAt(feedList.Count - 1);
            feedT = 4f;
        }

        int AliveCount()
        {
            int n = 0;
            foreach (var a in actors) if (a.active && !a.outed) n++;
            return n;
        }

        Ape LastAlive()
        {
            foreach (var a in actors) if (a.active && !a.outed) return a;
            return null;
        }

        Vector3 Listener => human.kind == "none" ? Vector3.zero : human.head;

        void Play(string name, Vector3? at = null, float vol = 1f) => sfx.Play(name, at, Listener, vol);

        // ---------------- rounds ----------------
        void PlaceHuman(Vector3 floorPt, float yaw)
        {
            var H = human;
            H.yaw = yaw; H.vel = Vector3.zero; H.hist.Clear(); H.wasAnch = false;
            Vector3 hl = MathX.RotY(H.headL, yaw);
            H.P = new Vector3(floorPt.x, floorPt.y + 0.68f, floorPt.z) - hl;
            H.head = HeadWorld(); H.pos = H.head + new Vector3(0, -0.1f, 0);
            foreach (var h in H.hands)
            {
                DropHeld(h); h.anchored = false;
                h.pos = HandTarget(h);
                // never start a hand on the far side of a floor or wall
                if (World.Sweep(H.head, h.pos, Cfg.HandR, out var hh, true)) h.pos = hh.p;
                h.prev = h.pos;
            }
        }

        void ResetActor(Ape a, int i)
        {
            a.outed = false; a.dizzy = 0; a.ko = 0; a.heldBy = null; a.thrownT = 0; a.tilt = Vector2.zero; a.tiltV = Vector2.zero; a.hitCD.Clear(); a.gripHits = 0;
            Vector3 sp = World.Spawns[i % World.Spawns.Count];
            float yaw = Mathf.Atan2(sp.x, sp.z);
            if (a == human) PlaceHuman(sp, yaw);
            else
            {
                a.pos = new Vector3(sp.x, sp.y + 0.56f, sp.z); a.vel = Vector3.zero; a.yaw = yaw; a.holding = null; a.slapT = -1f;
                a.hasGoal = false; a.goalApe = null; a.hang = false;
                foreach (var h in a.hands) { h.pos = a.pos; h.prev = a.pos; }
            }
        }

        void StartRound()
        {
            SetupSlots();
            round++; phase = Phase.Countdown; countdown = 3.2f; lastCount = 4; timeLeft = Cfg.RoundTime; winner = null; feedList.Clear();
            var order = new List<int>();
            for (int k = 0; k < actors.Count; k++) order.Add(k);
            for (int k = order.Count - 1; k > 0; k--) { int j = Random.Range(0, k + 1); (order[k], order[j]) = (order[j], order[k]); }
            for (int k = 0; k < actors.Count; k++)
            {
                var a = actors[k];
                a.active = a == human ? human.kind != "none" : true;
                if (a.active) ResetActor(a, order[k]);
            }
        }

        void EndRound()
        {
            winner = AliveCount() == 1 ? LastAlive() : null;
            if (winner != null) winner.wins++;
            phase = Phase.End; endT = 5.5f;
            Play(winner == human ? "win" : "go");
        }

        void Eliminate(Ape a)
        {
            if (a.outed) return;
            a.outed = true; a.ko = 0;
            if (a.heldBy != null) Release(a, null);
            Feed($"{a.name} fell!");
            Play("fall", a == human ? (Vector3?)null : a.pos, 0.7f);
            if (a == human)
            {
                foreach (var h in human.hands) DropHeld(h);
                PlaceHuman(World.OutLedge, Mathf.PI);
            }
            else { if (a.holding != null) Release(a.holding, null); a.holding = null; }
        }

        void UpdateGame(float dt)
        {
            feedT -= dt;
            if (phase == Phase.Countdown)
            {
                countdown -= dt;
                int c = Mathf.CeilToInt(countdown);
                if (c != lastCount) { lastCount = c; if (c > 0) Play("beep"); }
                if (countdown <= 0f) { phase = Phase.Fight; Play("go"); }
            }
            else if (phase == Phase.Fight)
            {
                timeLeft -= dt;
                if (AliveCount() <= 1 || timeLeft <= 0f) EndRound();
            }
            else
            {
                endT -= dt;
                if (endT <= 0f) StartRound();
            }
            foreach (var a in actors)
            {
                if (!a.active) continue;
                float y = a == human ? human.head.y : a.pos.y;
                if (!a.outed && y < Cfg.FallY) Eliminate(a);
                else if (a == human && a.outed && human.head.y < Cfg.FallY) PlaceHuman(World.OutLedge, Mathf.PI);
                if (a.ko > 0f) { a.ko -= dt; if (a.ko <= 0f) a.dizzy = 0f; }
                else if (now - a.lastHit > 2.5f) a.dizzy = Mathf.Max(0f, a.dizzy - 6f * dt);
                if (a.thrownT > 0f) a.thrownT -= dt;
                // wobble spring
                a.tiltV.x += (-a.tilt.x * 60f - a.tiltV.x * 7f) * dt; a.tiltV.y += (-a.tilt.y * 60f - a.tiltV.y * 7f) * dt;
                a.tilt.x += a.tiltV.x * dt; a.tilt.y += a.tiltV.y * dt;
            }
        }

        // ---------------- main loop ----------------
        void Update()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
            now += dt;

            // headset connected or removed while running
            bool xr = XRSettings.isDeviceActive;
            if (xr != xrWasActive)
            {
                xrWasActive = xr;
                human.kind = xr ? "vr" : "pc";
                firstVRFrame = xr;
                if (!xr) ResetDesktopCamera();
            }

            foreach (var h in human.hands) h.prev = h.pos;
            if (human.kind == "vr")
            {
                if (ReadXR())
                {
                    if (firstVRFrame)
                    {
                        firstVRFrame = false;
                        human.headL = xrHead;
                        for (int k = 0; k < 2; k++) human.hands[k].L = xrHands[k].pos;
                        round = 0; foreach (var a in actors) a.wins = 0;
                        StartRound();
                    }
                    if (phase == Phase.Countdown && !human.outed)
                    {
                        // free to look around, movement locked
                        foreach (var h in human.hands)
                        {
                            var s = xrHands[h.k];
                            h.L = s.pos; h.q = s.q; h.ok = s.ok; h.device = s.device;
                            h.pos = HandTarget(h);
                        }
                        human.headL = xrHead; human.head = HeadWorld();
                    }
                    else VrStep(dt);
                }
            }
            else if (human.kind == "pc") PcStep(dt);
            float inv = 1f / Mathf.Max(dt, 1e-3f);
            foreach (var h in human.hands) h.vel = (h.pos - h.prev) * inv;

            UpdateGame(dt);
            foreach (var b in bots) UpdateBot(b, dt);
            if (phase == Phase.Fight)
            {
                if (human.kind == "vr") VrGrabs();
                if (human.active && !human.outed && human.ko <= 0f) HandHits(human, human.hands);
                foreach (var b in bots) if (b.active && !b.outed && b.ko <= 0f) HandHits(b, b.hands);
            }
            UpdateHeld(dt);
            BodyBump();
            UpdateLog();
            UpdateVisuals(dt);
            hudT -= dt;
            if (hudT <= 0f) { hudT = 0.15f; DrawBoard(); DrawWrist(); }
        }

        float hudT;
    }
}
