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
        internal const string ScenePath3D = "Assets/Scenes/XSi0_3D.unity";
        internal const string ControllerPath = CharacterImportSettings.CharacterFolder + "OpponentAnimator.controller";
        const string RendererPath = "Assets/Settings/UniversalRenderer3D.asset";
        const string SeeThroughMaterialPath = "Assets/Art/Materials/BoardSeeThrough.mat";
        const string WorldMaterialPath = "Assets/Art/Materials/WorldLit.mat";
        const string SkyMaterialPath = "Assets/Art/Materials/SkyPanoramic.mat";
        const string GlowMaterialPath = "Assets/Art/Materials/Glow.mat";
        const string GameAssetsPath = "Assets/Art/GameAssets.asset";
        const string AudioFolder = "Assets/Audio";

        /// <summary>Creeaza scena jocului 2D (camera + bootstrap) si o pune prima in Build Settings.</summary>
        [MenuItem("TRAVEE/Creează scena X și 0 (2D)")]
        static void CreateScene2D()
        {
            CreateScene(ScenePath2D, ViewMode.TwoD, null, null, null, null, null);
        }

        /// <summary>
        /// Creeaza scena 3D: trece proiectul pe rendererul 3D, genereaza Animator Controller-ul personajului,
        /// aduna modelele si sunetele in biblioteca GameAssets si le pune pe toate in bootstrap.
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

            // Animator Controller-ul se genereaza doar daca lipseste: daca l-ai modificat in fereastra Animator,
            // ramane cum l-ai lasat. (Regenerarea se cere explicit, din meniul „Regenereaza animatiile personajului”.)
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (controller == null) controller = BuildAnimatorController();
            var seeThrough = EnsureSeeThroughMaterial();
            var world = EnsureWorldMaterial();
            var assets = BuildGameAssets();
            CreateScene(ScenePath3D, ViewMode.ThreeD, model, controller, seeThrough, world, assets);
        }

        /// <summary>Pentru linia de comanda: Unity.exe -batchmode -executeMethod Travee.TicTacToe.Editor.SceneSetup.BatchCreateScene3D</summary>
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

            // Daca scena exista deja, o deschidem si doar ii actualizam legaturile: tot ce ai pus de mana in ea
            // (decor, valori schimbate in Inspector) ramane. Doar prima data se creeaza una noua, goala.
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

            // Campurile sunt private ([SerializeField]), deci le setam prin SerializedObject, ca Inspector-ul.
            var so = new SerializedObject(bootstrap);
            so.FindProperty("viewMode").enumValueIndex = (int)mode;
            so.FindProperty("opponentModel").objectReferenceValue = model;
            so.FindProperty("opponentAnimator").objectReferenceValue = controller;
            so.FindProperty("boardSeeThroughMaterial").objectReferenceValue = seeThrough;
            so.FindProperty("worldMaterial").objectReferenceValue = world;
            // Reincarcam biblioteca de pe disc: crearea scenei noi (NewScene) descarca din memorie obiectul primit
            // ca parametru, iar o referinta spre un obiect descarcat s-ar salva ca „None”.
            so.FindProperty("assets").objectReferenceValue =
                mode == ViewMode.ThreeD ? AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath) : null;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (mode == ViewMode.ThreeD)
            {
                // Ceata e pornita din cod la rulare. O activam si in scena salvata, altfel Unity ar considera ca
                // jocul nu foloseste ceata si ar scoate din build variantele de shader care o deseneaza.
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
        /// Obiectele goale sub care pui decor de mana: Decor_All (apare in toate peisajele) si cate unul pentru
        /// fiecare peisaj (Decor_Forest, Decor_Mountain, Decor_Beach). La rulare, Room3D il arata doar pe cel potrivit.
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
        /// Animator Controller cu cate o stare pentru fiecare rol din <see cref="OpponentAnimations"/>.
        /// Fara tranzitii: codul (OpponentCharacter) trece intre stari cu CrossFade.
        /// </summary>
        static RuntimeAnimatorController BuildAnimatorController()
        {
            // Pastram acelasi fisier (si deci aceleasi legaturi din scena): doar ii golim starile si le refacem.
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            foreach (var old in controller.layers[0].stateMachine.states.ToArray())
                controller.layers[0].stateMachine.RemoveState(old.state);
            // IK Pass: necesar ca OpponentCharacter sa-si duca mana la casuta si sa se uite la pacient.
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

                // Pozitiile sunt doar pentru aranjarea starilor in fereastra Animator.
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
        /// Biblioteca GameAssets: toate modelele din Assets/Art/Nature, toate sunetele din Assets/Audio si
        /// materialul de cer. Se reface la fiecare rulare a meniului, deci un fisier nou pus in aceste foldere
        /// ajunge in joc fara alt pas manual.
        /// </summary>
        static GameAssets BuildGameAssets()
        {
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(assets, GameAssetsPath);
            }

            NatureImport.Setup();       // materialele modelelor si setarile panoramelor de cer
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
        /// Material URP Unlit („neluminat”): isi arata culoarea asa cum e, fara lumini si umbre, deci pare ca
        /// lumineaza singur. Folosit pentru flacari si licurici. Asset, ca shader-ul sa ajunga in build.
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
        /// Material de cer cu shader-ul Skybox/Panoramic, in modul „Latitude Longitude” (o imagine 360°).
        /// Imaginea o deseneaza codul la rulare (Sky.cs); aici e doar „rama” ei, salvata ca asset pentru build.
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
        /// scena, ca shader-ul sa ajunga in build: materialul implicit al pipeline-ului exista doar in editor.
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
        /// Material URP Lit transparent, folosit ca sablon pentru tabla 3D care devine transparenta.
        /// E un asset (nu creat din cod la rulare) ca varianta transparenta a shader-ului sa intre in build.
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
        /// Proiectul a pornit din sablonul 2D, al carui renderer nu lumineaza obiecte 3D.
        /// Inlocuim rendererul din pipeline-ul URP cu un Universal Renderer (UI-ul 2D functioneaza la fel).
        /// </summary>
        static void EnsureUniversalRenderer()
        {
            // currentRenderPipeline tine cont si de asset-ul setat pe nivelul de calitate (cazul acestui proiect).
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
                pipeline.LoadBuiltinRendererData(RendererType.UniversalRenderer); // creeaza Assets/UniversalRenderer.asset
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
