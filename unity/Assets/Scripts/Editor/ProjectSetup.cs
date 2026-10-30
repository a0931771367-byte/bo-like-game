using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BoLike.Editor
{
    /// <summary>
    /// One-time project setup kept in code so the repo needs no hand-made settings:
    /// URP with the 2D renderer, landscape iOS player settings. Runs automatically the first time the
    /// project opens (no render pipeline assigned yet) and can be re-run from the BoLike menu.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string SettingsDir = "Assets/Settings";
        const string RendererPath = SettingsDir + "/Renderer2D.asset";
        const string PipelinePath = SettingsDir + "/URP-2D.asset";

        const string ScenePath = "Assets/Scenes/Main.unity";

        static ProjectSetup()
        {
            if (GraphicsSettings.defaultRenderPipeline == null) EditorApplication.delayCall += Run;
            else if (!File.Exists(ScenePath)) EditorApplication.delayCall += SetupScene;
        }

        [MenuItem("BoLike/Setup Project")]
        public static void Run()
        {
            SetupRenderPipeline();
            SetupPlayer();
            SetupScene();
            AssetDatabase.SaveAssets();
            Debug.Log("BoLike: project setup done (URP 2D renderer, iOS landscape player settings).");
        }

        static void SetupRenderPipeline()
        {
            Directory.CreateDirectory(SettingsDir);
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            pipeline.renderScale = 1f;
            pipeline.msaaSampleCount = 1;
            pipeline.supportsHDR = false; // 2D, mobile: no HDR needed
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        /// <summary>The one scene: an empty GameObject with Bootstrap, which builds everything else at runtime.</summary>
        [MenuItem("BoLike/Create Main Scene")]
        public static void SetupScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("Bootstrap");
                go.AddComponent<BoLike.Game.Bootstrap>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("BoLike: Main scene ready at " + ScenePath);
        }

        /// <summary>
        /// App icon from the art department's logo-final (1024×1024, opaque). App Store Connect rejects an upload
        /// without the 1024 "Any Appearance" icon, so it is set as the default icon and on every iOS icon slot.
        /// </summary>
        static void SetupIcons()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Icons/app-icon-1024.png");
            if (tex == null) { Debug.LogWarning("[ProjectSetup] Assets/Icons/app-icon-1024.png missing: no app icon set"); return; }
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { tex });
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                foreach (var icon in icons) icon.SetTexture(tex);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
            }
        }

        static void SetupPlayer()
        {
            PlayerSettings.companyName = "Pocket Adventurers";
            PlayerSettings.productName = "口袋冒險團"; // iOS home-screen name; App Store name: 口袋冒險團 Pocket Adventurers
            // Must match the bundle id registered in App Store Connect (sprint D10).
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.pocketadventurers.game");
            SetupIcons();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.hideHomeButton = false;
            PlayerSettings.iOS.deferSystemGesturesMode = UnityEngine.iOS.SystemGestureDeferMode.All;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.iOS, 1); // arm64
            // Only seed these once; BuildIos bumps the build number on every export.
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1") PlayerSettings.bundleVersion = "0.1.0";
            if (string.IsNullOrEmpty(PlayerSettings.iOS.buildNumber) || PlayerSettings.iOS.buildNumber == "0") PlayerSettings.iOS.buildNumber = "1";
            // "Data Not Collected" privacy label: no analytics, no ads, no network calls.
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
        }
    }
}
