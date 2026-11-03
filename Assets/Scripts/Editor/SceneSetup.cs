using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Travee.TicTacToe.Editor
{
    public static class SceneSetup
    {
        const string ScenePath = "Assets/Scenes/XSi0.unity";

        /// <summary>Creează scena jocului (cameră + bootstrap) și o pune prima în Build Settings.</summary>
        [MenuItem("TRAVEE/Creează scena X și 0")]
        static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.transform.position = new Vector3(0, 0, -10);

            new GameObject("XSi0").AddComponent<TicTacToeBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[TRAVEE] Scena a fost creată: {ScenePath}. Apasă Play.");
        }
    }
}
