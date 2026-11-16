using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Wrecked
{
    /// <summary>A hand. The player's hands use all fields; bot hands only use pos/prev/vel.</summary>
    public class Hand
    {
        public int k;
        public Vector3 L, T, pos, prev, vel, A, n, Lp, lA, ln;
        public Quaternion q = Quaternion.identity;
        public bool anchored, trigger, grip, gripWas, ok;
        public Box dyn;
        public Ape holding;
        public float punchT;
        public InputDevice device;
        public Hand(int k) { this.k = k; }
    }

    public struct HistSample
    {
        public float t; public Vector3 p;
        public HistSample(float t, Vector3 p) { this.t = t; this.p = p; }
    }

    /// <summary>One of the six apes: the player or a bot.</summary>
    public class Ape
    {
        public int i;
        public string name;
        public Vector4 fur, band;
        public Color chip;
        public bool isHuman, isBot, active, outed, grounded;
        public int wins, gripHits;
        public Vector3 pos, vel;
        public float yaw, dizzy, lastHit = -9f, ko, heldT, thrownT;
        public Ape heldBy;
        public Hand heldHand;
        public Vector2 tilt, tiltV;
        public readonly Dictionary<int, float> hitCD = new Dictionary<int, float>();
        public Hand[] hands = { new Hand(0), new Hand(1) };

        // bot brain
        public float think, hopCD, slapCD, slapT = -1f, holdT, blocked, skill = 1f, grudgeT;
        public int slapHand = 1;
        public Ape target, holding, goalApe;
        public Vector3 goalPos;
        public bool hasGoal;
        public readonly float[] grudge = new float[8];
        public bool hang, mayHang = true;
        public float hangT, hangCool;
        public Vector3 hangPos, hangDir, hangLedge, slapDir;

        // the player
        public string kind = "none"; // "vr" | "pc" | "none"
        public Vector3 P, headL = new Vector3(0, 1, 0), head;
        public float pitch, noGrab, struggle;
        public readonly List<HistSample> hist = new List<HistSample>();
        public bool wasAnch, snapLatch;

        public Vector3 Goal => goalApe != null ? goalApe.pos : goalPos;

        public void Skin(int k)
        {
            int s = Mathf.Clamp(k, 0, 5);
            fur = Cfg.V4(ApeData.Fur[s]);
            band = Cfg.V4(ApeData.Band[s]);
            chip = ApeData.Chip[s];
        }
    }
}
