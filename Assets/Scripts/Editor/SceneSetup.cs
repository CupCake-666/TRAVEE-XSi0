using System.Linq;
using Travee.TicTacToe.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Travee.TicTacToe.Editor
{
    public static class SceneSetup
    {
        const string ScenePath2D = "Assets/Scenes/XSi0.unity";
        const string ScenePath3D = "Assets/Scenes/XSi0_3D.unity";
        const string ControllerPath = CharacterImportSettings.CharacterFolder + "OpponentAnimator.controller";
        const string RendererPath = "Assets/Settings/UniversalRenderer3D.asset";
        const string SeeThroughMaterialPath = "Assets/Art/Materials/BoardSeeThrough.mat";

        /// <summary>Creează scena jocului 2D (cameră + bootstrap) și o pune prima în Build Settings.</summary>
        [MenuItem("TRAVEE/Creează scena X și 0 (2D)")]
        static void CreateScene2D()
        {
            CreateScene(ScenePath2D, ViewMode.TwoD, null, null, null);
        }

        /// <summary>
        /// Creează scena 3D: trece proiectul pe rendererul 3D, generează Animator Controller-ul personajului
        /// și pune modelul + controller-ul în bootstrap.
        /// </summary>
        [MenuItem("TRAVEE/Creează scena X și 0 (3D)")]
        static void CreateScene3D()
        {
            EnsureUniversalRenderer();
            CharacterImportSettings.EnsureModelTextures();
            CharacterImportSettings.FixExtraTakes();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterImportSettings.ModelPath);
            if (model == null)
                Debug.LogWarning($"[TRAVEE] Nu am găsit modelul {CharacterImportSettings.ModelPath}. Scena se creează fără personaj.");

            var controller = BuildAnimatorController();
            var seeThrough = EnsureSeeThroughMaterial();
            CreateScene(ScenePath3D, ViewMode.ThreeD, model, controller, seeThrough);
        }

        /// <summary>Pentru linia de comandă: Unity.exe -batchmode -executeMethod Travee.TicTacToe.Editor.SceneSetup.BatchCreateScene3D</summary>
        public static void BatchCreateScene3D() => CreateScene3D();

        [MenuItem("TRAVEE/Regenerează animațiile personajului")]
        static void RebuildAnimator()
        {
            CharacterImportSettings.FixExtraTakes();
            BuildAnimatorController();
        }

        static void CreateScene(string path, ViewMode mode, GameObject model, RuntimeAnimatorController controller,
            Material seeThrough)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = mode == ViewMode.TwoD;
            cam.transform.position = new Vector3(0, 0, -10);

            var bootstrap = new GameObject("XSi0").AddComponent<TicTacToeBootstrap>();
            // Câmpurile sunt private ([SerializeField]), deci le setăm prin SerializedObject, ca Inspector-ul.
            var so = new SerializedObject(bootstrap);
            so.FindProperty("viewMode").enumValueIndex = (int)mode;
            so.FindProperty("opponentModel").objectReferenceValue = model;
            so.FindProperty("opponentAnimator").objectReferenceValue = controller;
            so.FindProperty("boardSeeThroughMaterial").objectReferenceValue = seeThrough;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, path);

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[TRAVEE] Scena a fost creată: {path}. Apasă Play.");
        }

        /// <summary>
        /// Animator Controller cu câte o stare pentru fiecare rol din <see cref="OpponentAnimations"/>.
        /// Fără tranziții: codul (OpponentCharacter) trece între stări cu CrossFade.
        /// </summary>
        static RuntimeAnimatorController BuildAnimatorController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            // IK Pass: necesar ca OpponentCharacter să-și ducă mâna la căsuță și să se uite la pacient.
            var layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            var machine = controller.layers[0].stateMachine;

            int position = 0;
            foreach (var pair in OpponentAnimations.Files)
            {
                var clip = CharacterImportSettings.LoadClip(pair.Value);
                if (clip == null)
                {
                    Debug.LogWarning($"[TRAVEE] Lipsește animația „{pair.Value}.fbx” (rolul {pair.Key}).");
                    continue;
                }

                // Pozițiile sunt doar pentru aranjarea stărilor în fereastra Animator.
                var state = machine.AddState(pair.Key, new Vector3(300 + (position % 3) * 220, (position / 3) * 70, 0));
                state.motion = clip;
                if (pair.Key == OpponentAnimations.Idle) machine.defaultState = state;
                position++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[TRAVEE] Animator Controller generat: {ControllerPath}");
            return controller;
        }

        /// <summary>
        /// Material URP Lit transparent, folosit ca șablon pentru tabla 3D care devine transparentă.
        /// E un asset (nu creat din cod la rulare) ca varianta transparentă a shader-ului să intre în build.
        /// </summary>
        static Material EnsureSeeThroughMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(SeeThroughMaterialPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            if (!AssetDatabase.IsValidFolder("Assets/Art/Materials")) AssetDatabase.CreateFolder("Assets/Art", "Materials");

            var material = new Material(shader) { name = "BoardSeeThrough" };
            material.SetFloat("_Surface", 1f);                                  // Transparent
            material.SetFloat("_Blend", 0f);                                    // Alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Smoothness", 0.25f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;

            AssetDatabase.CreateAsset(material, SeeThroughMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// Proiectul a pornit din șablonul 2D, al cărui renderer nu luminează obiecte 3D.
        /// Înlocuim rendererul din pipeline-ul URP cu un Universal Renderer (UI-ul 2D funcționează la fel).
        /// </summary>
        static void EnsureUniversalRenderer()
        {
            // currentRenderPipeline ține cont și de asset-ul setat pe nivelul de calitate (cazul acestui proiect).
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline))
            {
                Debug.LogWarning("[TRAVEE] Proiectul nu folosește URP; rendererul nu a fost schimbat.");
                return;
            }

            var so = new SerializedObject(pipeline);
            var list = so.FindProperty("m_RendererDataList");
            if (list.arraySize > 0 && list.GetArrayElementAtIndex(0).objectReferenceValue is UniversalRendererData)
                return; // deja 3D

            var existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (existing == null)
            {
                pipeline.LoadBuiltinRendererData(RendererType.UniversalRenderer); // creează Assets/UniversalRenderer.asset
                AssetDatabase.SaveAssets();
                AssetDatabase.MoveAsset("Assets/UniversalRenderer.asset", RendererPath);
                existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                so.Update();
            }

            list.GetArrayElementAtIndex(0).objectReferenceValue = existing;
            so.FindProperty("m_DefaultRendererIndex").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TRAVEE] Renderer-ul URP a fost trecut pe 3D ({RendererPath}).");
        }
    }
}
