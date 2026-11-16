using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wrecked
{
    public partial class Game
    {
        // ---------------- scene objects ----------------
        Transform worldRoot, rig, skyT, boardT, wristT, vignetteT, logT;
        Camera cam;
        Material levelMat, sceneryMat, apeMat, skyMat, shadowMat, panelMat, chipMat, textMat, wristPanelMat, wristTextMat, vignetteMat;
        Mesh apeMesh, bandMesh, armMesh, handMesh, starMesh, myHandMesh, discMesh, logMesh, ropeMesh;
        Font font;
        MaterialPropertyBlock mpb;
        float flash;

        class ApeView
        {
            public Transform body, band, shadow;
            public readonly Transform[] arms = new Transform[2], hands = new Transform[2], stars = new Transform[3];
            public Renderer bodyR, bandR;
            public readonly Renderer[] armR = new Renderer[2];
        }
        readonly ApeView[] views = new ApeView[8];
        readonly Transform[] myHands = new Transform[2];
        readonly Renderer[] myHandR = new Renderer[2];
        readonly Transform[] ropes = new Transform[2];

        // scoreboard + wrist
        TextMesh boardTitle, boardTime, boardSub, boardBanner, wristLeft, wristRight;
        Transform boardBannerBg, wristFill;
        readonly TextMesh[] rowName = new TextMesh[7], rowStatus = new TextMesh[7];
        readonly Transform[] rowChip = new Transform[7];
        readonly Renderer[] rowChipR = new Renderer[7];
        Renderer wristFillR;

        static Shader FindShader(string n)
        {
            var s = Shader.Find(n);
            if (s == null) Debug.LogError($"WRECKED: shader '{n}' not found. Is the Universal Render Pipeline set up? Run WRECKED > Set Up Project.");
            return s;
        }

        void SetEnv(Material m, Texture texArr)
        {
            if (texArr != null) m.SetTexture("_TexArr", texArr);
            m.SetVector("_FogColor", Cfg.V4(Cfg.Fog));
            m.SetFloat("_FogNear", Cfg.FogNear);
            m.SetFloat("_FogFar", Cfg.FogFar);
            Vector3 l = Conv.U(Cfg.LightDir);
            m.SetVector("_LightDir", new Vector4(l.x, l.y, l.z, 0f));
        }

        Material Unlit(Vector4 color, int queue, bool always = false, float offset = 0f, Texture tex = null)
        {
            var m = new Material(FindShader("Wrecked/Unlit"));
            m.SetVector("_Color", color);
            m.SetFloat("_ZTest", always ? (float)CompareFunction.Always : (float)CompareFunction.LessEqual);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetFloat("_OffsetUnits", offset);
            if (tex != null) m.mainTexture = tex;
            m.renderQueue = queue;
            return m;
        }

        Transform MakeObj(string name, Mesh mesh, Material mat, Transform parent, out MeshRenderer r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go.transform;
        }
        Transform MakeObj(string name, Mesh mesh, Material mat, Transform parent) => MakeObj(name, mesh, mat, parent, out _);

        TextMesh MakeText(string name, Transform parent, Vector3 local, float heightM, TextAnchor anchor, Color color, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = heightM / 6.4f;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.LowerRight || anchor == TextAnchor.MiddleRight ? TextAlignment.Right : anchor == TextAnchor.LowerCenter || anchor == TextAnchor.MiddleCenter ? TextAlignment.Center : TextAlignment.Left;
            tm.color = color;
            var r = go.GetComponent<MeshRenderer>();
            if (r == null) r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return tm;
        }

        void SetTint(Renderer r, Vector4 tint)
        {
            mpb.SetVector("_Tint", tint);
            r.SetPropertyBlock(mpb);
        }

        void SetColor(Renderer r, Vector4 c)
        {
            mpb.SetVector("_Color", c);
            r.SetPropertyBlock(mpb);
        }

        static void Place(Transform t, Vector3 pos, Quaternion q, Vector3 scale)
        {
            t.localPosition = Conv.U(pos);
            t.localRotation = Conv.UQ(q);
            t.localScale = scale;
        }

        void BuildVisuals()
        {
            mpb = new MaterialPropertyBlock();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            worldRoot = new GameObject("World").transform;

            // camera rig (any camera already in the scene is switched off)
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
            rig = new GameObject("Rig").transform;
            var camGO = new GameObject("Head Camera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(rig, false);
            cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f; cam.fieldOfView = 75f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Cfg.Fog;
            camGO.AddComponent<AudioListener>();
            Application.onBeforeRender += OnBeforeRender;

            // textures + materials
            var texArr = TextureGen.Build();
            levelMat = new Material(FindShader("Wrecked/Level")); SetEnv(levelMat, texArr);
            sceneryMat = new Material(FindShader("Wrecked/Scenery")); SetEnv(sceneryMat, texArr);
            apeMat = new Material(FindShader("Wrecked/Ape")); SetEnv(apeMat, texArr); apeMat.enableInstancing = true;
            skyMat = new Material(FindShader("Wrecked/Sky"));
            skyMat.SetVector("_Top", Cfg.V4(Cfg.SkyTop)); skyMat.SetVector("_Horizon", Cfg.V4(Cfg.Fog));
            shadowMat = Unlit(new Vector4(0, 0, 0, 0.25f), 2900, false, -2f);
            panelMat = Unlit(new Vector4(22 / 255f, 18 / 255f, 13 / 255f, 0.88f), 3000);
            chipMat = Unlit(Vector4.one, 3001);
            textMat = Unlit(Vector4.one, 3002, false, 0f, font.material.mainTexture);
            wristPanelMat = Unlit(new Vector4(22 / 255f, 18 / 255f, 13 / 255f, 0.9f), 4000, true);
            wristTextMat = Unlit(Vector4.one, 4002, true, 0f, font.material.mainTexture);
            vignetteMat = Unlit(new Vector4(26 / 255f, 13 / 255f, 0f, 0f), 4500, true);
            Font.textureRebuilt += OnFontRebuilt;

            // static world
            MakeObj("Level", MeshBuilder.BuildLevel(), levelMat, worldRoot);
            MakeObj("Scenery", MeshBuilder.BuildScenery(), sceneryMat, worldRoot);
            skyT = MakeObj("Sky", MeshBuilder.SkyDome(300f), skyMat, null);

            // ape meshes (our design: hunched furry torso, pale face, big round chimp ears, heavy brow; front is -Z)
            int fur = TextureGen.Mats["fur"].l;
            Color F = Color.white, SKIN = new Color(0.86f, 0.7f, 0.56f), DARK = new Color(0.12f, 0.09f, 0.08f), WHITE = new Color(0.96f, 0.95f, 0.92f);
            apeMesh = MeshBuilder.MergeParts("Ape",
                new Part(Geo.Ico(0.3f, 1), F, MathX.M(0, -0.08f, 0.02f, 0.25f, 0, 0, 1.05f, 1.0f, 0.9f), fur, true),
                new Part(Geo.Ico(0.19f, 1), F, MathX.M(0, 0.2f, -0.1f, 0, 0, 0, 1, 0.95f, 1), fur, true),
                new Part(Geo.Ico(0.13f, 1), SKIN, MathX.M(0, 0.17f, -0.22f, 0, 0, 0, 1.15f, 1.05f, 0.65f)),
                new Part(Geo.Ico(0.085f, 1), SKIN, MathX.M(0, 0.1f, -0.27f, 0, 0, 0, 1.35f, 0.85f, 0.85f)),
                new Part(Geo.BoxG(0.26f, 0.05f, 0.07f), F, MathX.M(0, 0.255f, -0.24f), fur, true),
                new Part(Geo.Cyl(0.075f, 0.075f, 0.03f, 10), SKIN, MathX.M(0.205f, 0.22f, -0.08f, 0, 0, Mathf.PI / 2)),
                new Part(Geo.Cyl(0.075f, 0.075f, 0.03f, 10), SKIN, MathX.M(-0.205f, 0.22f, -0.08f, 0, 0, Mathf.PI / 2)),
                new Part(Geo.Ico(0.035f, 1), WHITE, MathX.M(0.06f, 0.205f, -0.29f)),
                new Part(Geo.Ico(0.035f, 1), WHITE, MathX.M(-0.06f, 0.205f, -0.29f)),
                new Part(Geo.Ico(0.02f, 1), DARK, MathX.M(0.062f, 0.205f, -0.318f)),
                new Part(Geo.Ico(0.02f, 1), DARK, MathX.M(-0.062f, 0.205f, -0.318f)),
                new Part(Geo.BoxG(0.1f, 0.012f, 0.02f), DARK, MathX.M(0, 0.07f, -0.34f)),
                new Part(Geo.Ico(0.014f, 0), DARK, MathX.M(0.025f, 0.125f, -0.345f)),
                new Part(Geo.Ico(0.014f, 0), DARK, MathX.M(-0.025f, 0.125f, -0.345f)));
            bandMesh = MeshBuilder.MergeParts("Band",
                new Part(Geo.Cyl(0.195f, 0.195f, 0.055f, 12), F, MathX.M(0, 0.245f, -0.1f, -0.12f, 0, 0), -1, true),
                new Part(Geo.BoxG(0.05f, 0.1f, 0.04f), F, MathX.M(0.05f, 0.2f, 0.09f, 0.3f, 0, 0.3f), -1, true));
            armMesh = MeshBuilder.MergeParts("Arm", new Part(Geo.Cyl(0.055f, 0.045f, 1f, 6).Translate(0, 0.5f, 0), F, Matrix4x4.identity, fur, true));
            handMesh = MeshBuilder.MergeParts("Hand",
                new Part(Geo.Ico(0.075f, 1), SKIN, MathX.M(0, 0, 0, 0, 0, 0, 1, 0.8f, 1.15f)),
                new Part(Geo.Ico(0.035f, 0), SKIN, MathX.M(0.06f, 0.01f, -0.03f)));
            starMesh = MeshBuilder.MergeParts("Star", new Part(Geo.Octa(0.05f), new Color(1f, 0.86f, 0.25f), Matrix4x4.identity));
            myHandMesh = MeshBuilder.MergeParts("MyHand",
                new Part(Geo.Ico(0.06f, 1), SKIN, MathX.M(0, 0, 0, 0, 0, 0, 1, 0.8f, 1.2f)),
                new Part(Geo.Ico(0.028f, 0), SKIN, MathX.M(0.05f, 0.01f, -0.03f)),
                new Part(Geo.Cyl(0.04f, 0.045f, 0.1f, 7), F, MathX.M(0, 0, 0.085f, Mathf.PI / 2, 0, 0), fur, true));
            logMesh = MeshBuilder.MergeParts("Log",
                new Part(Geo.Cyl(0.42f, 0.42f, 4.6f, 9), F, MathX.M(0, 0, 0, 0, 0, Mathf.PI / 2), TextureGen.Mats["bark"].l),
                new Part(Geo.Cyl(0.36f, 0.36f, 0.02f, 9), new Color(0.9f, 0.75f, 0.5f), MathX.M(2.31f, 0, 0, 0, 0, Mathf.PI / 2)),
                new Part(Geo.Cyl(0.36f, 0.36f, 0.02f, 9), new Color(0.9f, 0.75f, 0.5f), MathX.M(-2.31f, 0, 0, 0, 0, Mathf.PI / 2)));
            ropeMesh = MeshBuilder.MergeParts("Rope", new Part(Geo.Cyl(0.035f, 0.035f, 1f, 5).Translate(0, 0.5f, 0), new Color(0.75f, 0.66f, 0.45f), Matrix4x4.identity));
            discMesh = MeshBuilder.Disc(0.34f, 12);

            for (int i = 0; i < views.Length; i++)
            {
                var v = new ApeView();
                var root = new GameObject($"Ape {i}").transform; root.SetParent(worldRoot, false);
                v.body = MakeObj("Body", apeMesh, apeMat, root, out var br); v.bodyR = br;
                v.band = MakeObj("Band", bandMesh, apeMat, root, out var bandR); v.bandR = bandR;
                for (int k = 0; k < 2; k++)
                {
                    v.arms[k] = MakeObj("Arm", armMesh, apeMat, root, out var ar); v.armR[k] = ar;
                    v.hands[k] = MakeObj("Hand", handMesh, apeMat, root);
                }
                for (int k = 0; k < 3; k++) v.stars[k] = MakeObj("Star", starMesh, apeMat, root);
                v.shadow = MakeObj("Shadow", discMesh, shadowMat, root);
                views[i] = v;
                Hide(v);
            }
            for (int k = 0; k < 2; k++) { myHands[k] = MakeObj("My Hand", myHandMesh, apeMat, worldRoot, out var mr); myHandR[k] = mr; }
            logT = MakeObj("Swinging Log", logMesh, apeMat, worldRoot);
            for (int k = 0; k < 2; k++) ropes[k] = MakeObj("Rope", ropeMesh, apeMat, worldRoot);

            // hit / dizzy vignette around the head
            vignetteT = MakeObj("Vignette", MeshBuilder.Sphere(0.4f), vignetteMat, cam.transform);
            vignetteT.gameObject.SetActive(false);

            BuildBoard();
            BuildWrist();
        }

        void OnFontRebuilt(Font f)
        {
            if (f != font) return;
            textMat.mainTexture = font.material.mainTexture;
            wristTextMat.mainTexture = font.material.mainTexture;
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= OnBeforeRender;
            Font.textureRebuilt -= OnFontRebuilt;
        }

        static void Hide(ApeView v)
        {
            v.body.gameObject.SetActive(false); v.band.gameObject.SetActive(false); v.shadow.gameObject.SetActive(false);
            for (int k = 0; k < 2; k++) { v.arms[k].gameObject.SetActive(false); v.hands[k].gameObject.SetActive(false); }
            for (int k = 0; k < 3; k++) v.stars[k].gameObject.SetActive(false);
        }

        // board layout uses the web version's 1024x512 canvas pixels
        const float BoardW = 5.6f, BoardH = 2.8f, BoardPx = BoardW / 1024f;
        static Vector3 BoardPos(float x, float y, float z = -0.01f) => new Vector3(x * BoardPx - BoardW / 2f, BoardH / 2f - y * BoardPx, z);
        static readonly Color Ink = new Color(0.953f, 0.918f, 0.847f), Muted = new Color(0.725f, 0.678f, 0.58f), Banana = new Color(1f, 0.761f, 0.227f);

        void BuildBoard()
        {
            boardT = new GameObject("Scoreboard").transform;
            boardT.position = Conv.U(new Vector3(0f, 7.2f, -14f));
            MakeObj("Panel", MeshBuilder.Quad(BoardW, BoardH), panelMat, boardT);
            boardTitle = MakeText("Round", boardT, BoardPos(48, 100), 76 * BoardPx * 0.75f, TextAnchor.LowerLeft, Banana, textMat);
            boardTime = MakeText("Time", boardT, BoardPos(976, 100), 76 * BoardPx * 0.75f, TextAnchor.LowerRight, Ink, textMat);
            boardSub = MakeText("Mode", boardT, BoardPos(48, 142), 30 * BoardPx * 0.75f, TextAnchor.LowerLeft, Muted, textMat);
            var chipMesh = MeshBuilder.Quad(56 * BoardPx, 56 * BoardPx);
            for (int i = 0; i < rowName.Length; i++)
            {
                float x = 48 + (i % 2) * 470, y = 170 + (i / 2) * 92;
                rowChip[i] = MakeObj("Chip", chipMesh, chipMat, boardT, out var cr); rowChipR[i] = cr;
                rowChip[i].localPosition = BoardPos(x + 28, y + 28, -0.005f);
                rowName[i] = MakeText("Name", boardT, BoardPos(x + 76, y + 46), 52 * BoardPx * 0.75f, TextAnchor.LowerLeft, Ink, textMat);
                rowStatus[i] = MakeText("Status", boardT, BoardPos(x + 430, y + 44), 34 * BoardPx * 0.75f, TextAnchor.LowerRight, Muted, textMat);
            }
            boardBannerBg = MakeObj("Banner", MeshBuilder.Quad(784 * BoardPx, 160 * BoardPx), panelMat, boardT);
            boardBannerBg.localPosition = BoardPos(512, 270, -0.015f);
            boardBanner = MakeText("Banner Text", boardT, BoardPos(512, 300, -0.02f), 86 * BoardPx * 0.75f, TextAnchor.LowerCenter, Banana, textMat);
        }

        const float WristW = 0.16f, WristH = 0.08f, WristPx = WristW / 256f;
        static Vector3 WristPos(float x, float y, float z = -0.001f) => new Vector3(x * WristPx - WristW / 2f, WristH / 2f - y * WristPx, z);

        void BuildWrist()
        {
            wristT = new GameObject("Wrist Display").transform;
            MakeObj("Panel", MeshBuilder.Quad(WristW, WristH), wristPanelMat, wristT);
            wristLeft = MakeText("Left", wristT, WristPos(16, 50), 40 * WristPx * 0.75f, TextAnchor.LowerLeft, Ink, wristTextMat);
            wristRight = MakeText("Right", wristT, WristPos(240, 50), 40 * WristPx * 0.75f, TextAnchor.LowerRight, Banana, wristTextMat);
            var barMat = Unlit(new Vector4(1, 1, 1, 0.15f), 4001, true);
            var bg = MakeObj("Bar", MeshBuilder.BarQuad(224 * WristPx, 22 * WristPx), barMat, wristT);
            bg.localPosition = WristPos(16, 91, -0.0005f);
            wristFill = MakeObj("Fill", MeshBuilder.BarQuad(224 * WristPx, 22 * WristPx), Unlit(Vector4.one, 4001, true), wristT, out var fr);
            wristFillR = fr;
            wristFill.localPosition = WristPos(16, 91, -0.0008f);
            wristT.gameObject.SetActive(false);
        }

        // ---------------- per frame ----------------
        static readonly Vector3 Up = new Vector3(0, 1, 0);

        void Seg(Transform t, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float len = Mathf.Max(0.05f, d.magnitude);
            Place(t, from, MathX.FromUnitVectors(Up, d / len), new Vector3(1f, len, 1f));
        }

        void UpdateVisuals(float dt)
        {
            int vi = 0;
            foreach (var a in actors)
            {
                if (!a.active || vi >= views.Length) continue;
                bool isMe = a == human;
                var v = views[vi++];
                bool show = !isMe;
                v.body.gameObject.SetActive(show); v.band.gameObject.SetActive(show);
                for (int k = 0; k < 2; k++) { v.arms[k].gameObject.SetActive(show); v.hands[k].gameObject.SetActive(show); }
                bool ko = show && a.ko > 0f;
                for (int k = 0; k < 3; k++) v.stars[k].gameObject.SetActive(ko);
                if (show)
                {
                    // body: yaw, then wobble tilt; knocked-out apes flop onto their backs
                    float tx = Mathf.Clamp(a.tilt.y * 0.12f, -0.7f, 0.7f) + (a.ko > 0f ? -1.25f : 0f), tz = Mathf.Clamp(-a.tilt.x * 0.12f, -0.7f, 0.7f);
                    Quaternion q = MathX.EulerXYZ(tx, 0f, tz) * MathX.YawQ(a.yaw);
                    Place(v.body, a.pos, q, Vector3.one); SetTint(v.bodyR, a.fur);
                    Place(v.band, a.pos, q, Vector3.one); SetTint(v.bandR, a.band);
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 shoulder = a.pos + MathX.RotY(new Vector3(k == 0 ? -0.27f : 0.27f, -0.06f, -0.02f), a.yaw);
                        Seg(v.arms[k], shoulder, a.hands[k].pos); SetTint(v.armR[k], a.fur);
                        Place(v.hands[k], a.hands[k].pos, MathX.YawQ(a.yaw), Vector3.one);
                    }
                    if (ko)
                        for (int k = 0; k < 3; k++)
                        {
                            float an = now * 4f + k * 2.09f;
                            Place(v.stars[k], new Vector3(a.pos.x + Mathf.Cos(an) * 0.28f, a.pos.y + 0.5f, a.pos.z + Mathf.Sin(an) * 0.28f), MathX.YawQ(now * 6f), Vector3.one);
                        }
                }
                // blob shadow under every ape (yours too in VR)
                Hit h = default;
                bool shadow = !a.outed && !(isMe && human.kind == "pc")
                    && World.Sweep(a.pos, new Vector3(a.pos.x, a.pos.y - 25f, a.pos.z), 0.02f, out h, true) && h.n.y > 0.5f;
                v.shadow.gameObject.SetActive(shadow);
                if (shadow)
                {
                    float s = Mathf.Clamp(1f - (a.pos.y - h.p.y) * 0.08f, 0.35f, 1f);
                    Place(v.shadow, new Vector3(h.p.x, h.p.y + 0.015f, h.p.z), Quaternion.identity, new Vector3(s, 1f, s));
                }
            }
            for (; vi < views.Length; vi++) Hide(views[vi]);

            // your hands
            bool showMine = human.kind != "none";
            for (int k = 0; k < 2; k++)
            {
                myHands[k].gameObject.SetActive(showMine);
                if (!showMine) continue;
                var hd = human.hands[k];
                Quaternion q = human.kind == "vr" ? MathX.YawQ(human.yaw) * hd.q : MathX.EulerYXZ(human.pitch, human.yaw, 0f);
                Place(myHands[k], hd.pos, q, Vector3.one); SetTint(myHandR[k], human.fur);
            }

            // swinging log + ropes
            Place(logT, logC, MathX.AxisAngle(new Vector3(1, 0, 0), logTheta), Vector3.one);
            for (int k = 0; k < 2; k++)
            {
                float x = k == 1 ? 1.6f : -1.6f;
                Seg(ropes[k], new Vector3(x, logC.y, logC.z), new Vector3(x, LogPivot.y, 0f));
            }

            // hit flash / dizzy vignette
            flash = Mathf.Max(0f, flash - dt);
            float vo = human.ko > 0f ? 0.55f : flash * 1.6f;
            bool vig = human.kind != "none" && vo > 0.01f;
            vignetteT.gameObject.SetActive(vig);
            if (vig) vignetteMat.SetVector("_Color", new Vector4(26 / 255f, 13 / 255f, 0f, Mathf.Clamp01(vo)));
        }

        void LateUpdate()
        {
            rig.localPosition = Conv.U(human.P);
            rig.localRotation = Quaternion.Euler(0f, -human.yaw * Mathf.Rad2Deg, 0f);
            if (human.kind != "vr")
            {
                cam.transform.localPosition = Conv.U(human.headL);
                cam.transform.localRotation = Quaternion.Euler(-human.pitch * Mathf.Rad2Deg, 0f, 0f);
            }
            Vector3 cp = cam.transform.position;
            skyT.position = cp;
            Vector3 fwd = boardT.position - new Vector3(cp.x, boardT.position.y, cp.z);
            if (fwd.sqrMagnitude > 1e-4f) boardT.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            bool wrist = human.kind == "vr" && human.hands[0].ok;
            wristT.gameObject.SetActive(wrist);
            if (wrist)
            {
                wristT.position = Conv.U(human.hands[0].pos + new Vector3(0f, 0.1f, 0f));
                Vector3 wf = wristT.position - cp;
                if (wf.sqrMagnitude > 1e-6f) wristT.rotation = Quaternion.LookRotation(wf, Vector3.up);
            }
        }

        void ResetDesktopCamera()
        {
            cam.transform.localPosition = Conv.U(human.headL);
            cam.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Head pose is applied right before rendering for the lowest latency in the headset.</summary>
        void OnBeforeRender()
        {
            if (human == null || human.kind != "vr" || cam == null) return;
            var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.CenterEye);
            if (!head.isValid) head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            if (!head.isValid) return;
            if (head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyePosition, out Vector3 p) || head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out p))
                cam.transform.localPosition = p;
            if (head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyeRotation, out Quaternion q) || head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out q))
                cam.transform.localRotation = q;
        }

        // ---------------- HUD text ----------------
        static string FmtTime(float t)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(t));
            return $"{s / 60}:{s % 60:00}";
        }

        string Banner()
        {
            if (phase == Phase.Countdown) return countdown > 0.2f ? Mathf.CeilToInt(countdown).ToString() : "RUMBLE!";
            if (phase == Phase.Fight && timeLeft > Cfg.RoundTime - 1f) return "RUMBLE!";
            if (phase == Phase.End) return winner != null ? (winner == human ? "You win!" : $"{winner.name} wins!") : "Nobody wins!";
            if (human.outed) return "You fell! Next round soon";
            if (human.heldBy != null) return human.kind == "pc" ? "Grabbed! Click to slap them till they let go" : "Grabbed! Slap them till they let go!";
            if (human.ko > 0f) return human.kind == "pc" ? "Dizzy! Mash keys to shake it off" : "Dizzy! Shake your arms!";
            return "";
        }

        void DrawBoard()
        {
            boardTitle.text = $"ROUND {round}";
            boardTime.text = phase == Phase.Countdown ? Banner() : phase == Phase.End ? "NEXT..." : FmtTime(timeLeft);
            boardSub.text = "Offline · bots";
            int i = 0;
            foreach (var a in actors)
            {
                if (!a.active || i >= rowName.Length) continue;
                float alpha = a.outed ? 0.35f : 1f;
                rowChip[i].gameObject.SetActive(true);
                SetColor(rowChipR[i], new Vector4(a.chip.r, a.chip.g, a.chip.b, alpha));
                rowName[i].text = a.name; rowName[i].color = new Color(Ink.r, Ink.g, Ink.b, alpha);
                rowStatus[i].text = a.outed ? "out" : a.ko > 0f ? "dizzy" : $"{a.wins} {(a.wins == 1 ? "win" : "wins")}";
                rowStatus[i].color = new Color(Muted.r, Muted.g, Muted.b, alpha);
                i++;
            }
            for (; i < rowName.Length; i++) { rowChip[i].gameObject.SetActive(false); rowName[i].text = ""; rowStatus[i].text = ""; }
            bool end = phase == Phase.End;
            boardBannerBg.gameObject.SetActive(end);
            boardBanner.text = end ? Banner() : "";
        }

        void DrawWrist()
        {
            if (wristT == null || !wristT.gameObject.activeSelf) return;
            var H = human;
            bool stuck = H.ko > 0f;
            wristLeft.text = H.outed ? "OUT" : stuck ? "SHAKE!" : H.heldBy != null ? "SLAP!" : FmtTime(timeLeft);
            wristRight.text = $"{AliveCount()} left";
            float fill = Mathf.Clamp01(stuck ? H.struggle : H.dizzy / 100f);
            wristFill.localScale = new Vector3(Mathf.Max(0.005f, fill), 1f, 1f);
            Color c = stuck ? Banana : new Color(1f, 0.416f, 0.239f);
            SetColor(wristFillR, new Vector4(c.r, c.g, c.b, 1f));
        }

        GUIStyle guiBig, guiSmall;

        void OnGUI()
        {
            if (human == null || human.kind != "pc") return;
            if (guiBig == null)
            {
                guiBig = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                guiBig.normal.textColor = Banana;
                guiSmall = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                guiSmall.normal.textColor = Ink;
            }
            float w = Screen.width, h = Screen.height;
            GUI.Label(new Rect(0, 8, w, 28), $"Round {round}    Apes left {AliveCount()}    {FmtTime(timeLeft)}", guiSmall);
            string b = Banner();
            if (string.IsNullOrEmpty(b) && feedT > 0f && feedList.Count > 0) b = feedList[0];
            GUI.Label(new Rect(0, h * 0.22f, w, 80), b, guiBig);
            GUI.Label(new Rect(0, h - 84, w, 24), Cursor.lockState == CursorLockMode.Locked
                ? "WASD move · Space hop · Click slap · Hold right-click to grab, let go to throw · Esc frees the mouse"
                : "Click the game view to capture the mouse", guiSmall);
            bool stuck = human.ko > 0f;
            float fill = Mathf.Clamp01(stuck ? human.struggle : human.dizzy / 100f);
            float bw = Mathf.Min(320f, w - 32f), bx = (w - bw) / 2f, by = h - 52f;
            GUI.Label(new Rect(0, by - 22, w, 20), stuck ? "Break free" : "Dizzy", guiSmall);
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(bx, by, bw, 12), Texture2D.whiteTexture);
            GUI.color = stuck ? Banana : new Color(1f, 0.416f, 0.239f); GUI.DrawTexture(new Rect(bx, by, bw * fill, 12), Texture2D.whiteTexture);
            GUI.color = old;
            // crosshair
            GUI.Label(new Rect(w / 2 - 10, h / 2 - 12, 20, 24), "+", guiSmall);
        }
    }
}
