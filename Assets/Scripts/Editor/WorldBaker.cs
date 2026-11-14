using System.Linq;
using Travee.TicTacToe.Characters;
using Travee.TicTacToe.Core;
using Travee.TicTacToe.UI;
using Travee.TicTacToe.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Travee.TicTacToe.Editor
{
    /// <summary>
    /// „Scena editabila”: pune in scena XSi0_3D, ca obiecte reale si salvate, tot ce altfel ar fi creat din cod la
    /// pornire – camera, tabla, personajul (cu Animator-ul lui), locurile in care asteapta, cele trei peisaje,
    /// luminile si setarile lor. Dupa aceea le aranjezi din fereastra Scene si din Inspector, iar jocul le
    /// foloseste asa cum le gaseste. Nimic din ce exista deja in scena nu e suprascris.
    /// </summary>
    public static class WorldBaker
    {
        const string Menu = "TRAVEE/Scena editabilă/";

        [MenuItem(Menu + "1. Pune totul în scenă", priority = 100)]
        public static void BuildMissing()
        {
            if (!PrepareScene()) return;
            SceneView3D.Apply();

            var boardPosition = EnsureBoard();
            EnsureCamera(boardPosition);
            EnsureSpots(boardPosition);
            EnsureCharacter();
            EnsureUI();
            EnsureDecorAll();
            foreach (WorldTheme theme in System.Enum.GetValues(typeof(WorldTheme)))
            {
                var decor = DecorRoot(theme);
                var landscape = decor.transform.Find(Room3D.EditableName);
                // Un peisaj pus in scena de o versiune mai veche (fara suporturi selectabile) se reface.
                if (landscape != null && landscape.GetComponentInChildren<PropHolder>(true) == null)
                {
                    Object.DestroyImmediate(landscape.gameObject);
                    landscape = null;
                }
                if (landscape == null) BuildLandscape(theme);
                EnsureThemeSettings(theme);
            }

            Show(WorldTheme.Forest);
            SaveScene();
            Debug.Log("[TRAVEE] Scena e pregătită: Tabla, Personaj, Loc_Stanga/Loc_Dreapta, Main Camera, Interfata și " +
                      "Decor_... (cu Peisaj, Soare și setări). Le poți selecta și modifica în Scene și în Inspector.");
        }

        [MenuItem(Menu + "Arată în editor: Pădure", priority = 120)]
        static void ShowForest() => Show(WorldTheme.Forest);

        [MenuItem(Menu + "Arată în editor: Munte", priority = 121)]
        static void ShowMountain() => Show(WorldTheme.Mountain);

        [MenuItem(Menu + "Arată în editor: Plajă", priority = 122)]
        static void ShowBeach() => Show(WorldTheme.Beach);

        [MenuItem(Menu + "Reconstruiește din cod peisajul afișat (pierzi aranjarea lui)", priority = 140)]
        static void RebuildShown()
        {
            if (!PrepareScene()) return;
            var theme = ShownTheme();
            if (!EditorUtility.DisplayDialog("Reconstruiește peisajul",
                    $"Obiectul „Peisaj” din „{EnvironmentBuilder.DisplayName(theme)}” va fi șters și construit din nou din cod " +
                    "(Environments.cs). Tot ce ai mutat, șters sau adăugat în el se pierde. " +
                    "Ce ai pus lângă el, direct sub Decor_..., rămâne, la fel și setările peisajului.", "Reconstruiește", "Renunță")) return;

            var old = DecorRoot(theme).transform.Find(Room3D.EditableName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            BuildLandscape(theme);
            Show(theme);
            SaveScene();
        }

        [MenuItem(Menu + "Readu setările din cod pentru peisajul afișat (cer, ceață, lumină)", priority = 141)]
        static void ResetShownSettings()
        {
            if (!PrepareScene()) return;
            var theme = ShownTheme();
            var settings = DecorRoot(theme).GetComponent<ThemeSettings>();
            if (settings != null) Object.DestroyImmediate(settings);
            EnsureThemeSettings(theme, resetSun: true);
            Show(theme);
            SaveScene();
        }

        [MenuItem(Menu + "Reconstruiește interfața din cod (pierzi aranjarea ei)", priority = 142)]
        static void RebuildUIFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Reconstruiește interfața",
                    "Obiectul „Interfata” (meniul, moneda, textele, ecranul de rezultat) va fi șters din scenă și " +
                    "construit din nou din cod. Tot ce ai mutat sau schimbat în el se pierde.", "Reconstruiește", "Renunță")) return;
            RebuildUI();
        }

        /// <summary>Sterge interfata din scena si o pune din nou, din cod. (Public: se poate apela si din linia de comanda.)</summary>
        public static void RebuildUI()
        {
            if (!PrepareScene()) return;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.GetComponentInChildren<MenuScreen>(true) != null || canvas.GetComponentInChildren<GameScreen>(true) != null)
                    Object.DestroyImmediate(canvas.gameObject);
            EnsureUI();
            SaveScene();
        }

        // ------------------------------------------------------------------ pregatire

        /// <summary>Verifica ca se poate lucra: Play oprit, scena 3D deschisa, asset-urile de baza prezente.</summary>
        static bool PrepareScene()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Scena editabilă", "Oprește întâi Play-ul.", "OK");
                return false;
            }
            if (SceneManager.GetActiveScene().path != SceneSetup.ScenePath3D)
            {
                if (!System.IO.File.Exists(SceneSetup.ScenePath3D))
                {
                    Debug.LogError("[TRAVEE] Scena 3D nu există. Rulează întâi TRAVEE → Creează scena X și 0 (3D).");
                    return false;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                EditorSceneManager.OpenScene(SceneSetup.ScenePath3D, OpenSceneMode.Single);
            }

            // Materialul de baza al obiectelor construite din cod (in joc il seteaza TicTacToeBootstrap).
            WorldFactory.Template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/WorldLit.mat");
            if (WorldFactory.Template == null || Assets() == null)
            {
                Debug.LogError("[TRAVEE] Lipsesc asset-urile de bază. Rulează întâi TRAVEE → Creează scena X și 0 (3D).");
                return false;
            }
            return true;
        }

        static GameAssets Assets() => AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/Art/GameAssets.asset");

        // ------------------------------------------------------------------ tabla, camera, personajul

        /// <summary>Obiectul „Tabla”: pozitia, rotatia si marimea tablei sunt cele din Transform-ul lui.</summary>
        static Vector3 EnsureBoard()
        {
            var board = Object.FindAnyObjectByType<Board3DView>();
            if (board == null)
            {
                board = new GameObject("Tabla").AddComponent<Board3DView>();
                board.transform.SetPositionAndRotation(
                    new Vector3(0f, Room3D.DefaultBoardHeight, Room3D.DefaultBoardDistance), Room3D.DefaultBoardRotation);
                board.transform.localScale = Vector3.one * Room3D.BoardScale;
            }
            return board.transform.position;
        }

        /// <summary>
        /// Camera = capul pacientului. O asezam o data si ii punem componenta DesktopHeadLook; prezenta acesteia
        /// ii spune jocului ca e o camera asezata de mana, pe care nu trebuie s-o mai mute.
        /// </summary>
        static void EnsureCamera(Vector3 boardPosition)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
            }
            if (camera.GetComponent<DesktopHeadLook>() != null) return;

            Room3D.ConfigureCamera(camera, boardPosition);
            camera.gameObject.AddComponent<DesktopHeadLook>();
            if (Object.FindAnyObjectByType<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
        }

        /// <summary>Locurile in care asteapta personajul: doua obiecte goale, pe care le poti muta.</summary>
        static void EnsureSpots(Vector3 boardPosition)
        {
            var spots = Object.FindObjectsByType<OpponentSpot>(FindObjectsSortMode.None);
            foreach (var side in new[] { AffectedSide.Left, AffectedSide.Right })
            {
                if (spots.Any(spot => spot.side == side)) continue;
                var spot = new GameObject(side == AffectedSide.Left ? "Loc_Stanga" : "Loc_Dreapta").AddComponent<OpponentSpot>();
                spot.side = side;
                spot.transform.position = Room3D.DefaultHome(side, boardPosition);
            }
        }

        /// <summary>
        /// Obiectul „Personaj”: o instanta a modelului, cu Animator (si Animator Controller-ul generat) si cu
        /// componenta OpponentCharacter, ale carei setari apar in Inspector.
        /// </summary>
        static void EnsureCharacter()
        {
            if (Object.FindAnyObjectByType<OpponentCharacter>() != null) return;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterImportSettings.ModelPath);
            if (model == null)
            {
                Debug.LogWarning($"[TRAVEE] Nu am găsit modelul {CharacterImportSettings.ModelPath}: scena rămâne fără „Personaj”.");
                return;
            }

            // InstantiatePrefab pastreaza legatura cu fisierul modelului (daca il reimporti, se actualizeaza si aici).
            var character = (GameObject)PrefabUtility.InstantiatePrefab(model);
            character.name = "Personaj";
            var spot = Object.FindObjectsByType<OpponentSpot>(FindObjectsSortMode.None).FirstOrDefault(s => s.side == AffectedSide.Left);
            if (spot != null) character.transform.position = spot.transform.position;
            character.transform.rotation = Quaternion.Euler(0f, 150f, 0f);     // cu fata spre pacient

            var animator = character.GetComponent<Animator>();
            if (animator == null) animator = character.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SceneSetup.ControllerPath);
            animator.applyRootMotion = false;
            character.AddComponent<OpponentCharacter>();
        }

        // ------------------------------------------------------------------ interfata

        const string CirclePath = "Assets/Art/UI/Circle.png";

        /// <summary>
        /// Obiectul „Interfata”: Canvas-ul cu cele patru ecrane (joc, moneda, rezultat, meniu), construite cu
        /// acelasi cod ca la pornire, dar salvate in scena. Le muti, le recolorezi si le schimbi textele din
        /// editor; ce fac butoanele ramane in cod (metodele Wire ale ecranelor).
        /// </summary>
        static void EnsureUI()
        {
            // Daca exista deja vreun ecran in scena, nu mai punem nimic (ar aparea de doua ori).
            if (Object.FindAnyObjectByType<MenuScreen>(FindObjectsInactive.Include) != null ||
                Object.FindAnyObjectByType<GameScreen>(FindObjectsInactive.Include) != null ||
                Object.FindAnyObjectByType<CoinFlipScreen>(FindObjectsInactive.Include) != null ||
                Object.FindAnyObjectByType<ResultScreen>(FindObjectsInactive.Include) != null) return;

            var canvas = UIFactory.CreateCanvas(null, "Interfata");
            // Ordinea conteaza: ultimul creat e desenat deasupra.
            var game = GameScreen.Create(canvas, withBoard: false);
            var coinFlip = CoinFlipScreen.Create(canvas);
            var result = ResultScreen.Create(canvas, overScene: true);
            MenuScreen.Create(canvas, overScene: true);
            coinFlip.SetCoinSprite(EnsureCircleSprite());

            // In editor lasam vizibil doar meniul (primul ecran din joc); pe celelalte le activezi din Inspector
            // cand vrei sa le aranjezi. La pornire, jocul decide singur care se vede.
            game.gameObject.SetActive(false);
            coinFlip.gameObject.SetActive(false);
            result.gameObject.SetActive(false);
        }

        /// <summary>Imaginea rotunda a monedei, salvata ca fisier PNG (un obiect din scena are nevoie de un asset).</summary>
        static Sprite EnsureCircleSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder("Assets/Art/UI")) AssetDatabase.CreateFolder("Assets/Art", "UI");
            System.IO.File.WriteAllBytes(CirclePath, UIFactory.Circle.texture.EncodeToPNG());
            AssetDatabase.ImportAsset(CirclePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(CirclePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
        }

        // ------------------------------------------------------------------ peisajele

        static void EnsureDecorAll()
        {
            string name = Room3D.DecorPrefix + "All";
            if (!SceneManager.GetActiveScene().GetRootGameObjects().Any(go => go.name == name)) new GameObject(name);
        }

        /// <summary>Obiectul-radacina Decor_... al unui peisaj; il creeaza daca lipseste.</summary>
        static GameObject DecorRoot(WorldTheme theme)
        {
            string name = Room3D.DecorPrefix + theme;
            var root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(go => go.name == name);
            return root != null ? root : new GameObject(name);
        }

        /// <summary>Construieste obiectele unui peisaj sub Decor_... → Peisaj, cu acelasi cod ca jocul.</summary>
        static void BuildLandscape(WorldTheme theme)
        {
            var decor = DecorRoot(theme);
            decor.SetActive(true);      // obiectele se masoara (bounds) corect doar cat sunt active

            var holder = new GameObject(Room3D.EditableName);
            holder.transform.SetParent(decor.transform, false);
            holder.transform.SetAsFirstSibling();
            EnvironmentBuilder.Build(theme, Assets(), holder.transform);
        }

        /// <summary>
        /// Lumina principala („Soare”) si componenta ThemeSettings a unui peisaj, completate cu valorile din cod.
        /// Dupa aceea, valorile din Inspector sunt cele folosite de joc.
        /// </summary>
        static void EnsureThemeSettings(WorldTheme theme, bool resetSun = false)
        {
            var decor = DecorRoot(theme);
            var look = EnvironmentBuilder.Look(theme);

            // Versiunile mai vechi numeau lumina „LuminaPrevizualizare”.
            var legacy = decor.transform.Find("LuminaPrevizualizare");
            if (legacy != null) legacy.name = Room3D.SunName;

            var sunTransform = decor.transform.Find(Room3D.SunName);
            bool newSun = sunTransform == null;
            if (newSun)
            {
                sunTransform = new GameObject(Room3D.SunName).transform;
                sunTransform.SetParent(decor.transform, false);
            }
            var sun = sunTransform.GetComponent<Light>();
            if (sun == null) sun = sunTransform.gameObject.AddComponent<Light>();
            sunTransform.gameObject.SetActive(true);
            if (newSun || resetSun)
            {
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                sun.color = look.SunColor;
                sun.intensity = look.SunIntensity;
                sunTransform.rotation = Quaternion.Euler(look.SunAngles);
            }

            var settings = decor.GetComponent<ThemeSettings>();
            if (settings != null) return;       // exista: valorile din Inspector raman neatinse

            var assets = Assets();
            settings = decor.AddComponent<ThemeSettings>();
            settings.theme = theme;
            settings.look = look;
            settings.skyPanorama = assets.SkyTexture(look.Sky.Panorama);
            settings.ambientSound = assets.Clip(look.AmbientSound);
            settings.sun = sun;
        }

        /// <summary>Arata in editor un singur peisaj (celelalte sunt dezactivate) si ii aplica cerul, ceata si lumina ambienta.</summary>
        static void Show(WorldTheme theme)
        {
            if (EditorApplication.isPlaying) return;
            foreach (WorldTheme other in System.Enum.GetValues(typeof(WorldTheme)))
                DecorRoot(other).SetActive(other == theme);

            var settings = DecorRoot(theme).GetComponent<ThemeSettings>();
            Room3D.ApplyLook(settings != null ? settings.look : EnvironmentBuilder.Look(theme), Assets(), Camera.main,
                settings != null ? settings.skyPanorama : null);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }

        /// <summary>Peisajul afisat acum in editor = primul Decor_... activ.</summary>
        static WorldTheme ShownTheme()
        {
            foreach (WorldTheme theme in System.Enum.GetValues(typeof(WorldTheme)))
                if (DecorRoot(theme).activeSelf) return theme;
            return WorldTheme.Forest;
        }

        static void SaveScene()
        {
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
