using System.IO;
using UnityEditor;
using UnityEditor.Build;
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

        static ProjectSetup()
        {
            if (GraphicsSettings.defaultRenderPipeline == null) EditorApplication.delayCall += Run;
        }

        [MenuItem("BoLike/Setup Project")]
        public static void Run()
        {
            SetupRenderPipeline();
            SetupPlayer();
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

        static void SetupPlayer()
        {
            PlayerSettings.companyName = "Blade & Spell Squad";
            PlayerSettings.productName = "劍與魔法小隊"; // iOS home-screen name; App Store name: 劍與魔法小隊 Blade & Spell Squad
            // Must match the bundle id registered in App Store Connect (sprint D10).
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.bladespellsquad.game");

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
        }
    }
}
