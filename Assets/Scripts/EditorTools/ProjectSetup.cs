using System.IO;
using System.Linq;
using PodcastTycoon.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PodcastTycoon.EditorTools
{
    /// <summary>
    /// Creates the runtime assets slice 1 needs: a PanelSettings and a Game scene wired to a
    /// UIDocument + Bootstrap. Idempotent — safe to run repeatedly.
    /// Invoke from the menu, or headless: -executeMethod PodcastTycoon.EditorTools.ProjectSetup.Build
    /// </summary>
    public static class ProjectSetup
    {
        const string PanelSettingsPath = "Assets/Settings/PodcastTycoonPanelSettings.asset";
        const string ThemePath = "Assets/UI/PodcastTycoonTheme.tss";
        const string RootUxmlPath = "Assets/UI/Root.uxml";
        const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Podcast Tycoon/Set up slice 1 scene")]
        public static void Build()
        {
            var panelSettings = EnsurePanelSettings();
            var scene = EnsureScene(panelSettings);
            EnsureInBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ProjectSetup] Ready. Scene: {ScenePath}  PanelSettings: {PanelSettingsPath}");

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static PanelSettings EnsurePanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (existing != null) return existing;

            Directory.CreateDirectory("Assets/Settings");

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.name = "PodcastTycoonPanelSettings";
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1280, 800);
            ps.match = 0.5f;

            var tss = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (tss == null)
            {
                var guid = AssetDatabase.FindAssets("t:ThemeStyleSheet").FirstOrDefault();
                if (!string.IsNullOrEmpty(guid))
                    tss = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(AssetDatabase.GUIDToAssetPath(guid));
            }
            if (tss != null) ps.themeStyleSheet = tss;
            else Debug.LogWarning("[ProjectSetup] No ThemeStyleSheet found; default controls may look unstyled.");

            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            return ps;
        }

        static Scene EnsureScene(PanelSettings panelSettings)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(RootUxmlPath);

            Scene scene;
            if (File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                Directory.CreateDirectory("Assets/Scenes");
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            var go = GameObject.Find("Game") ?? new GameObject("Game");

            var doc = go.GetComponent<UIDocument>() ?? go.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            doc.visualTreeAsset = uxml;

            var bootstrap = go.GetComponent<Bootstrap>() ?? go.AddComponent<Bootstrap>();
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/PodcastTycoon.uss");
            if (uss != null)
            {
                var so = new SerializedObject(bootstrap);
                var prop = so.FindProperty("_styleSheet");
                if (prop != null) { prop.objectReferenceValue = uss; so.ApplyModifiedPropertiesWithoutUndo(); }
            }

            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera");
                cam.tag = "MainCamera";
                var c = cam.AddComponent<Camera>();
                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            return scene;
        }

        static void EnsureInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
