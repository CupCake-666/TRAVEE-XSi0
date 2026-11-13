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
        const string WorldMaterialPath = "Assets/Art/Materials/WorldLit.mat";
        const string SkyMaterialPath = "Assets/Art/Materials/SkyPanoramic.mat";
        const string GlowMaterialPath = "Assets/Art/Materials/Glow.mat";
        const string GameAssetsPath = "Assets/Art/GameAssets.asset";
        const string AudioFolder = "Assets/Audio";

        /// <summary>Creează scena jocului 2D (cameră + bootstrap) și o pune prima în Build Settings.</summary>
        [MenuItem("TRAVEE/Creează scena X și 0 (2D)")]
        static void CreateScene2D()
        {
            CreateScene(ScenePath2D, ViewMode.TwoD, null, null, null, null, null);
        }

        /// <summary>
        /// Creează scena 3D: trece proiectul pe rendererul 3D, generează Animator Controller-ul personajului,
        /// adună modelele și sunetele în biblioteca GameAssets și le pune pe toate în bootstrap.
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
            var world = EnsureWorldMaterial();
            var assets = BuildGameAssets();
            CreateScene(ScenePath3D, ViewMode.ThreeD, model, controller, seeThrough, world, assets);
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
            Material seeThrough, Material world, GameAssets assets)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Dacă scena există deja, o deschidem și doar îi actualizăm legăturile: tot ce ai pus de mână în ea
            // (decor, valori schimbate în Inspector) rămâne. Doar prima dată se creează una nouă, goală.
            bool exists = System.IO.File.Exists(path);
            var scene = exists
                ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrap = Object.FindAnyObjectByType<TicTacToeBootstrap>();
            if (bootstrap == null)
            {
                var cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.orthographic = mode == ViewMode.TwoD;
                cam.transform.position = new Vector3(0, 0, -10);
                bootstrap = new GameObject("XSi0").AddComponent<TicTacToeBootstrap>();
            }
            if (mode == ViewMode.ThreeD) EnsureDecorRoots();

            // Câmpurile sunt private ([SerializeField]), deci le setăm prin SerializedObject, ca Inspector-ul.
            var so = new SerializedObject(bootstrap);
            so.FindProperty("viewMode").enumValueIndex = (int)mode;
            so.FindProperty("opponentModel").objectReferenceValue = model;
            so.FindProperty("opponentAnimator").objectReferenceValue = controller;
            so.FindProperty("boardSeeThroughMaterial").objectReferenceValue = seeThrough;
            so.FindProperty("worldMaterial").objectReferenceValue = world;
            // Reîncărcăm biblioteca de pe disc: crearea scenei noi (NewScene) descarcă din memorie obiectul primit
            // ca parametru, iar o referință spre un obiect descărcat s-ar salva ca „None”.
            so.FindProperty("assets").objectReferenceValue =
                mode == ViewMode.ThreeD ? AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath) : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (mode == ViewMode.ThreeD)
            {
                // Ceața e pornită din cod la rulare. O activăm și în scena salvată, altfel Unity ar considera că
                // jocul nu folosește ceață și ar scoate din build variantele de shader care o desenează.
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
            }

            EditorSceneManager.SaveScene(scene, path);

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[TRAVEE] Scena a fost {(exists ? "actualizată" : "creată")}: {path}. Apasă Play.");
        }

        /// <summary>
        /// Obiectele goale sub care pui decor de mână: Decor_All (apare în toate peisajele) și câte unul pentru
        /// fiecare peisaj (Decor_Forest, Decor_Mountain, Decor_Beach). La rulare, Room3D îl arată doar pe cel potrivit.
        /// </summary>
        static void EnsureDecorRoots()
        {
            var names = new System.Collections.Generic.List<string> { World.Room3D.DecorPrefix + "All" };
            foreach (var theme in System.Enum.GetNames(typeof(World.WorldTheme))) names.Add(World.Room3D.DecorPrefix + theme);

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (string name in names)
                if (!roots.Any(root => root.name == name)) new GameObject(name);
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
        /// Biblioteca GameAssets: toate modelele din Assets/Art/Nature, toate sunetele din Assets/Audio și
        /// materialul de cer. Se reface la fiecare rulare a meniului, deci un fișier nou pus în aceste foldere
        /// ajunge în joc fără alt pas manual.
        /// </summary>
        static GameAssets BuildGameAssets()
        {
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(assets, GameAssetsPath);
            }

            NatureImport.Setup();       // materialele modelelor și setările panoramelor de cer
            assets.models = LoadAll<GameObject>("t:Model", NatureImport.NatureFolder);
            assets.skies = LoadAll<Texture2D>("t:Texture2D", NatureImport.SkyFolder);
            assets.clips = LoadAll<AudioClip>("t:AudioClip", AudioFolder);
            assets.skyMaterial = EnsureSkyMaterial();
            assets.glowMaterial = EnsureGlowMaterial();
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();

            if (assets.models.Length == 0) Debug.LogWarning($"[TRAVEE] Nu am găsit modele în {NatureImport.NatureFolder}: peisajele vor fi goale.");
            if (assets.clips.Length == 0) Debug.LogWarning($"[TRAVEE] Nu am găsit sunete în {AudioFolder}: jocul va fi mut.");
            Debug.Log($"[TRAVEE] GameAssets: {assets.models.Length} modele, {assets.clips.Length} sunete, {assets.skies.Length} panorame de cer.");
            return assets;
        }

        static T[] LoadAll<T>(string filter, string folder) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new T[0];
            return AssetDatabase.FindAssets(filter, new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(path => path)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();
        }

        /// <summary>
        /// Material URP Unlit („neluminat”): își arată culoarea așa cum e, fără lumini și umbre, deci pare că
        /// luminează singur. Folosit pentru flăcări și licurici. Asset, ca shader-ul să ajungă în build.
        /// </summary>
        static Material EnsureGlowMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            if (!AssetDatabase.IsValidFolder("Assets/Art/Materials")) AssetDatabase.CreateFolder("Assets/Art", "Materials");

            var material = new Material(shader) { name = "Glow" };
            AssetDatabase.CreateAsset(material, GlowMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// Material de cer cu shader-ul Skybox/Panoramic, în modul „Latitude Longitude” (o imagine 360°).
        /// Imaginea o desenează codul la rulare (Sky.cs); aici e doar „rama” ei, salvată ca asset pentru build.
        /// </summary>
        static Material EnsureSkyMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null) return null;
            if (!AssetDatabase.IsValidFolder("Assets/Art/Materials")) AssetDatabase.CreateFolder("Assets/Art", "Materials");

            var material = new Material(shader) { name = "SkyPanoramic" };
            material.SetFloat("_Mapping", 1f);              // 1 = Latitude Longitude Layout
            material.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            material.SetFloat("_ImageType", 0f);            // 360°
            material.SetFloat("_Exposure", 1f);
            AssetDatabase.CreateAsset(material, SkyMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>
        /// Material URP Lit opac din care se fac toate obiectele 3D (podea, perete, piese). E un asset referit de
        /// scenă, ca shader-ul să ajungă în build: materialul implicit al pipeline-ului există doar în editor.
        /// </summary>
        static Material EnsureWorldMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(WorldMaterialPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return null;
            if (!AssetDatabase.IsValidFolder("Assets/Art/Materials")) AssetDatabase.CreateFolder("Assets/Art", "Materials");

            var material = new Material(shader) { name = "WorldLit" };
            material.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(material, WorldMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
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
