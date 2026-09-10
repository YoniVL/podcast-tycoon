using System.Collections;
using System.IO;
using PodcastTycoon.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PodcastTycoon.EditorTools
{
    /// <summary>
    /// Enters play mode headless, walks the first few screens by calling the Bootstrap
    /// debug hooks, and saves a screenshot of each. Run via the Editor binary directly
    /// (NOT `unity run`, which injects -quit): -executeMethod PodcastTycoon.EditorTools.PlayCapture.Begin
    /// </summary>
    [InitializeOnLoad]
    public static class PlayCapture
    {
        const string ActiveKey = "PT_CAPTURE_ACTIVE";
        public static readonly string OutDir =
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "CaptureOut");

        static PlayCapture()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
                {
                    var go = new GameObject("~PlayCapture") { hideFlags = HideFlags.HideAndDontSave };
                    Object.DontDestroyOnLoad(go);
                    go.AddComponent<CaptureRunner>();
                }
            };
        }

        public static void Begin()
        {
            Directory.CreateDirectory(OutDir);
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            EditorApplication.EnterPlaymode();
        }

        public static void Done()
        {
            SessionState.SetBool(ActiveKey, false);
        }
    }

    public sealed class CaptureRunner : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            var bootstrap = FindObjectOfType<Bootstrap>();

            yield return Shot("01_setup");

            bootstrap.DebugStartRun();
            yield return Shot("02_week");

            bootstrap.DebugPickFirstTopic();
            yield return Shot("03_prep");

            bootstrap.DebugPublish();
            yield return Shot("04_results");

            bootstrap.ContinueFromResults();
            yield return Shot("05_week2");

            PlayCapture.Done();
            Debug.Log("[PlayCapture] done — screenshots in " + PlayCapture.OutDir);
            yield return new WaitForSecondsRealtime(0.5f);
            EditorApplication.isPlaying = false;
        }

        void OnApplicationQuit()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.Exit(0);
            };
        }

        static IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(0.9f);
            var path = Path.Combine(PlayCapture.OutDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path, 1);
            yield return new WaitForSecondsRealtime(0.6f);
            Debug.Log("[PlayCapture] " + path);
        }
    }
}
