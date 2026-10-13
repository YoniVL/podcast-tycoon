using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PodcastTycoon.EditorTools
{
    /// <summary>
    /// Builds the Android APK for sideloading onto a phone.
    /// Headless: unity build --target Android --execute-method PodcastTycoon.EditorTools.AndroidBuild.Build -o Builds/Android/PodcastTycoon.apk
    /// Uses the scene list in Build Settings, the project's Android player settings and Unity's
    /// default debug keystore (fine for installing on your own device, not for the Play Store).
    /// </summary>
    public static class AndroidBuild
    {
        const string DefaultOutput = "Builds/Android/PodcastTycoon.apk";

        const string IconDir = "Assets/AppIcon/";

        /// <summary>Sets the launcher icon from Assets/AppIcon: an adaptive icon (navy background +
        /// artwork inside the safe zone) Unity derives the legacy and round launcher icons from it.</summary>
        [MenuItem("Podcast Tycoon/Apply Android app icon")]
        public static void ApplyIcons()
        {
            Texture2D Load(string name)
            {
                string path = IconDir + name;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) throw new FileNotFoundException("Missing app icon art: " + path);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            var background = Load("icon_background.png");
            var foreground = Load("icon_foreground.png");
            var target = NamedBuildTarget.Android;

            var adaptive = PlayerSettings.GetPlatformIcons(target, AndroidPlatformIconKind.Adaptive);
            foreach (var icon in adaptive) icon.SetTextures(background, foreground);
            PlayerSettings.SetPlatformIcons(target, AndroidPlatformIconKind.Adaptive, adaptive);

            AssetDatabase.SaveAssets();
            Debug.Log($"[AndroidBuild] App icon applied ({adaptive.Length} adaptive slots).");
        }

        [MenuItem("Podcast Tycoon/Build Android APK")]
        public static void Build()
        {
            string output = DefaultOutput;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-buildOutput") output = args[i + 1];

            ApplyIcons();

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No enabled scenes in Build Settings.");

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            EditorUserBuildSettings.buildAppBundle = false;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            var summary = report.summary;
            Debug.Log($"[AndroidBuild] {summary.result} — {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} error(s), {summary.totalWarnings} warning(s) → {summary.outputPath}");
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
