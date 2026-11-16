using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Wrecked.EditorTools
{
    /// <summary>
    /// WRECKED > Set Up Project: makes the scene, a Quest-tuned URP asset, Android/Quest player settings,
    /// and turns on OpenXR with Meta Quest support. Each step logs what it did; read the Console if something fails.
    /// XR packages are touched through reflection so this script still compiles if a package is missing.
    /// </summary>
    public static class WreckedSetup
    {
        const string Root = "Assets/WRECKED";
        const string ScenePath = Root + "/Scenes/Main.unity";
        const string RenderPath = Root + "/Rendering";

        [MenuItem("WRECKED/Set Up Project", priority = 0)]
        public static void SetUp()
        {
            Log("Setting up WRECKED...");
            Step("Scene", MakeScene);
            Step("Render pipeline (URP)", MakePipeline);
            Step("Player settings", PlayerSetup);
            Step("XR (OpenXR + Meta Quest)", XrSetup);
            AssetDatabase.SaveAssets();
            Step("Switch to Android", SwitchToAndroid);
            Log("Done. Press Play to test with keyboard and mouse, or build to your Quest (see README.md).");
        }

        [MenuItem("WRECKED/Open Main Scene", priority = 1)]
        public static void OpenScene()
        {
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Log("The main scene doesn't exist yet. Run WRECKED > Set Up Project first.");
        }

        static void Log(string m) => Debug.Log("[WRECKED setup] " + m);
        static void Warn(string m) => Debug.LogWarning("[WRECKED setup] " + m);

        static void Step(string name, Action a)
        {
            try { a(); Log(name + ": OK"); }
            catch (Exception e) { Debug.LogError("[WRECKED setup] " + name + " failed: " + e.Message + "\n" + e); }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ---------------- scene ----------------
        static void MakeScene()
        {
            EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("WRECKED");
            go.AddComponent<Wrecked.Game>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Log("Saved " + ScenePath + " and added it to the build.");
        }

        // ---------------- URP ----------------
        static void MakePipeline()
        {
            EnsureFolder(RenderPath);
            string rendererPath = RenderPath + "/WRECKED_Renderer.asset";
            string assetPath = RenderPath + "/WRECKED_URP.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, rendererPath);
            }
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            // Quest-friendly: 4x MSAA, no HDR, no shadows, full resolution
            asset.msaaSampleCount = 4;
            asset.supportsHDR = false;
            asset.renderScale = 1f;
            asset.shadowDistance = 0f;
            SetSerialized(asset, "m_MainLightShadowsSupported", false);
            SetSerialized(asset, "m_AdditionalLightShadowsSupported", false);
            SetSerialized(asset, "m_SupportsSoftShadows", false);
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            Log("URP asset assigned in Graphics and every Quality level.");
        }

        static void SetSerialized(UnityEngine.Object o, string prop, bool value)
        {
            var so = new SerializedObject(o);
            var p = so.FindProperty(prop);
            if (p == null) { Warn("URP setting '" + prop + "' not found (skipped)."); return; }
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- player ----------------
        static void PlayerSetup()
        {
            PlayerSettings.companyName = "WRECKED";
            PlayerSettings.productName = "WRECKED";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.wrecked.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            Log("Product WRECKED, package com.wrecked.game, Android API 32+, ARM64, IL2CPP, Linear colour.");
        }

        // ---------------- XR (reflection, so a missing package can't break compilation) ----------------
        const string LoaderSettingsKey = "com.unity.xr.management.loader_settings";

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        static void XrSetup()
        {
            var perTargetType = FindType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget");
            var generalType = FindType("UnityEngine.XR.Management.XRGeneralSettings");
            var managerType = FindType("UnityEngine.XR.Management.XRManagerSettings");
            var metadataType = FindType("UnityEditor.XR.Management.Metadata.XRPackageMetadataStore");
            if (perTargetType == null || generalType == null || managerType == null || metadataType == null)
            {
                Warn("XR Plug-in Management isn't loaded. Open Window > Package Manager, make sure 'XR Plugin Management' and 'OpenXR Plugin' are installed, then run this again.");
                return;
            }

            // the settings container that XR Plug-in Management keeps in EditorBuildSettings
            EditorBuildSettings.TryGetConfigObject(LoaderSettingsKey, out UnityEngine.Object perTarget);
            if (perTarget == null)
            {
                EnsureFolder("Assets/XR");
                perTarget = ScriptableObject.CreateInstance(perTargetType);
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(LoaderSettingsKey, perTarget, true);
                Log("Created XR settings.");
            }

            foreach (var group in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                var general = GetGeneralSettings(perTargetType, perTarget, group, generalType, managerType);
                if (general == null) { Warn("Couldn't create XR settings for " + group + "."); continue; }
                var manager = generalType.GetProperty("Manager")?.GetValue(general);
                generalType.GetProperty("InitManagerOnStart")?.SetValue(general, true);
                var assign = metadataType.GetMethod("AssignLoader", BindingFlags.Public | BindingFlags.Static);
                bool ok = assign != null && manager != null && (bool)assign.Invoke(null, new object[] { manager, "UnityEngine.XR.OpenXR.OpenXRLoader", group });
                if (ok) Log("OpenXR turned on for " + group + ".");
                else Warn("Couldn't turn on OpenXR for " + group + ". Tick it by hand: Edit > Project Settings > XR Plug-in Management > " + group + " tab > OpenXR.");
                EnableOpenXRFeatures(group);
            }
            AssetDatabase.SaveAssets();
        }

        static object GetGeneralSettings(Type perTargetType, UnityEngine.Object perTarget, BuildTargetGroup group, Type generalType, Type managerType)
        {
            var has = perTargetType.GetMethod("HasSettingsForBuildTarget", BindingFlags.Public | BindingFlags.Instance);
            var get = perTargetType.GetMethod("SettingsForBuildTarget", BindingFlags.Public | BindingFlags.Instance);
            var set = perTargetType.GetMethod("SetSettingsForBuildTarget", BindingFlags.Public | BindingFlags.Instance);
            object general = null;
            if (has != null && get != null && (bool)has.Invoke(perTarget, new object[] { group })) general = get.Invoke(perTarget, new object[] { group });
            if (general == null && set != null)
            {
                var g = ScriptableObject.CreateInstance(generalType);
                var m = ScriptableObject.CreateInstance(managerType);
                g.name = group + " Settings"; m.name = group + " Providers";
                generalType.GetProperty("Manager")?.SetValue(g, m);
                AssetDatabase.AddObjectToAsset(g, perTarget);
                AssetDatabase.AddObjectToAsset(m, perTarget);
                set.Invoke(perTarget, new object[] { group, g });
                EditorUtility.SetDirty(perTarget);
                general = g;
            }
            return general;
        }

        static void EnableOpenXRFeatures(BuildTargetGroup group)
        {
            var settingsType = FindType("UnityEngine.XR.OpenXR.OpenXRSettings");
            if (settingsType == null) { Warn("OpenXR package not loaded; skipped features for " + group + "."); return; }
            var helpers = FindType("UnityEditor.XR.OpenXR.Features.FeatureHelpers");
            helpers?.GetMethod("RefreshFeatures", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(BuildTargetGroup) }, null)?.Invoke(null, new object[] { group });
            var getSettings = settingsType.GetMethod("GetSettingsForBuildTargetGroup", BindingFlags.Public | BindingFlags.Static);
            var settings = getSettings?.Invoke(null, new object[] { group }) as UnityEngine.Object;
            if (settings == null) { Warn("No OpenXR settings for " + group + " yet. Open Edit > Project Settings > XR Plug-in Management > OpenXR once, then run this again."); return; }
            var getFeatures = settingsType.GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m => m.Name == "GetFeatures" && !m.IsGenericMethod && m.GetParameters().Length == 0);
            var features = getFeatures?.Invoke(settings, null) as Array;
            if (features == null) { Warn("Couldn't read OpenXR features for " + group + "."); return; }
            string[] wanted = group == BuildTargetGroup.Android
                ? new[] { "MetaQuestFeature", "OculusQuestFeature", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile" }
                : new[] { "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile" };
            int turnedOn = 0;
            foreach (var f in features)
            {
                if (f == null) continue;
                var t = f.GetType();
                if (!wanted.Contains(t.Name)) continue;
                var enabled = t.GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
                if (enabled == null || !enabled.CanWrite) continue;
                enabled.SetValue(f, true);
                if (f is UnityEngine.Object uo) EditorUtility.SetDirty(uo);
                turnedOn++;
                Log("OpenXR feature on (" + group + "): " + t.Name);
            }
            EditorUtility.SetDirty(settings);
            if (turnedOn == 0) Warn("No Meta Quest / Oculus Touch features found for " + group + ". Turn them on by hand under XR Plug-in Management > OpenXR.");
        }

        // ---------------- build target ----------------
        static void SwitchToAndroid()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) { Log("Already on Android."); return; }
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            if (!ok) Warn("Couldn't switch to Android. Install 'Android Build Support' for this Unity version in Unity Hub (Installs > ... > Add modules), then run this again. You can still press Play to test on your computer.");
        }
    }
}
