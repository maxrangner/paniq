using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Paniq.Editor
{
    /// <summary>
    /// Pressing Play with no scene open plays the game instead of nothing.
    ///
    /// Unity opens a blank, never-saved scene when it has nothing to reopen
    /// (as it did on 2026-09-24, after a session that closed straight after a
    /// play-mode test run). Play on that scene enters play mode faithfully
    /// and shows an empty Game tab, which reads as "the game does not
    /// launch". When the only open scene is that blank one, this cancels the
    /// Play, opens the game's starting scene, presses Play again, and says so
    /// in the Console. Any scene with anything in it, saved or not, and the
    /// saved scene the test runner builds for play-mode tests, are left alone.
    ///
    /// Not <c>EditorSceneManager.playModeStartScene</c>: that starts every
    /// Play from one chosen scene, including the test runner's, and the test
    /// framework does not guard against it, so the play-mode tests would
    /// break.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayFromABlankScene
    {
        private const string StartingScene = "Assets/Paniq/Scenes/Bootstrap.unity";

        static PlayFromABlankScene()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode || !IsTheBlankScene())
            {
                return;
            }

            EditorApplication.isPlaying = false;
            Debug.Log("Paniq: no scene was open, so Play would have shown nothing. " +
                      "Opening the game's scene and playing it.");

            // On the next editor tick, once the cancelled Play is settled.
            // Not EditorApplication.delayCall: that waits for the editor's
            // panels to refresh, which they do not while the window is
            // minimised, and the scene then never opened.
            EditorApplication.update += OpenTheGameAndPlay;
        }

        private static void OpenTheGameAndPlay()
        {
            EditorApplication.update -= OpenTheGameAndPlay;
            EditorSceneManager.OpenScene(StartingScene, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        /// <summary>
        /// Exactly one scene open, never saved and never touched. The scene
        /// Unity makes when it has nothing to reopen is not empty: it holds a
        /// camera and a light, so "no objects" would never match it. What
        /// marks it is that nobody has done anything to it. An unsaved scene
        /// somebody has changed is theirs, and is left alone.
        /// </summary>
        private static bool IsTheBlankScene()
        {
            if (SceneManager.sceneCount != 1)
            {
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            return string.IsNullOrEmpty(scene.path) && !scene.isDirty;
        }
    }
}
