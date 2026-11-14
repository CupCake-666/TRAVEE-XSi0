using Travee.TicTacToe.Audio;
using Travee.TicTacToe.Characters;
using Travee.TicTacToe.Core;
using Travee.TicTacToe.UI;
using Travee.TicTacToe.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Travee.TicTacToe
{
    public enum ViewMode { TwoD, ThreeD }

    /// <summary>
    /// Singurul component care trebuie pus in scena: construieste din cod UI-ul, (in 3D) peisajul cu tabla
    /// si personajul, sunetele, apoi controller-ul. Parametrii de terapie / dificultate sunt aici, in Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TicTacToeBootstrap : MonoBehaviour
    {
        [Header("Afișare")]
        [SerializeField, Tooltip("2D = tabla pe ecran; 3D = tabla 3D în fața pacientului și personajul AI animat.")]
        ViewMode viewMode = ViewMode.TwoD;
        [SerializeField, Tooltip("Modelul personajului (FBX Mixamo, rig Humanoid). Doar pentru 3D.")]
        GameObject opponentModel;
        [SerializeField, Tooltip("Animator Controller-ul generat de meniul TRAVEE. Doar pentru 3D.")]
        RuntimeAnimatorController opponentAnimator;
        [SerializeField, Tooltip("Unde stă personajul când așteaptă. Folosit doar dacă personajul NU e în scenă (altfel: setarea de pe obiectul „Personaj”).")]
        OpponentPlacement opponentPlacement = OpponentPlacement.AffectedSide;
        [SerializeField, Tooltip("Material URP transparent (creat de meniul TRAVEE), pentru tabla care devine transparentă.")]
        Material boardSeeThroughMaterial;
        [SerializeField, Tooltip("Material URP Lit opac (creat de meniul TRAVEE), baza pentru toate obiectele 3D. Necesar în build.")]
        Material worldMaterial;
        [SerializeField, Tooltip("Biblioteca de modele și sunete (creată de meniul TRAVEE). Fără ea: fără peisaje și fără sunet.")]
        GameAssets assets;
        [SerializeField, Tooltip("Peisajul de la prima pornire. După aceea se ține minte cel ales din meniu.")]
        WorldTheme worldTheme = WorldTheme.Forest;
        [SerializeField, Range(0.5f, 1f), Tooltip("Cât de întins e brațul personajului când atinge căsuța. " +
            "Mai mic = stă mai aproape de tablă, cu cotul îndoit; 1 = brațul complet întins, stă mai departe.")]
        float opponentReach = 1f;
        [SerializeField, Range(0.6f, 1.3f), Tooltip("Înălțimea centrului tablei față de podea (metri). 1,2 = la nivelul ochilor unui " +
            "pacient așezat. Personajul e mărit automat cât să ajungă la rândul de sus.")]
        float boardHeight = Room3D.DefaultBoardHeight;
        [SerializeField, Range(0.4f, 1.2f), Tooltip("Distanța tablei față de pacient (metri). Pentru atingerea cu mâna în VR " +
            "se potrivește după lungimea brațului pacientului (orientativ 0,5–0,65).")]
        float boardDistance = Room3D.DefaultBoardDistance;

        [Header("Sunet")]
        [SerializeField, Range(0f, 1f), Tooltip("Volumul efectelor (click, piese, pași, rezultat).")]
        float effectsVolume = 0.8f;
        [SerializeField, Range(0f, 1f), Tooltip("Volumul ambianței (păsări, greieri, valuri).")]
        float ambientVolume = 0.35f;

        [Header("Pacient")]
        [SerializeField] AffectedSide affectedSide = AffectedSide.Left;

        [Header("Dificultate")]
        [SerializeField] DifficultySettings difficulty = new DifficultySettings();
        [SerializeField, Min(0f), Tooltip("Cât timp (secunde) „se gândește” AI-ul înainte să mute.")]
        float aiMoveDelay = 0.8f;

        [Header("Date")]
        [SerializeField, Tooltip("Salvează games.csv și moves.csv în persistentDataPath/TRAVEE/XSi0.")]
        bool logData = true;
        [SerializeField, Tooltip("0 = aleator. O valoare fixă face meciurile reproductibile.")]
        int randomSeed;

        const string ThemeKey = "travee.xsi0.theme";    // cheia sub care tinem minte peisajul ales

        void Awake()
        {
            bool is3D = viewMode == ViewMode.ThreeD;

            EnsureEventSystem();
            if (assets != null) Sfx.Create(transform, assets, effectsVolume, ambientVolume);
            // Interfata: daca cele patru ecrane sunt in scena (puse din editor), le folosim pe acelea si le legam
            // doar butoanele de logica. Altfel le construim acum, din cod.
            var game = FindAnyObjectByType<GameScreen>(FindObjectsInactive.Include);
            var coinFlip = FindAnyObjectByType<CoinFlipScreen>(FindObjectsInactive.Include);
            var result = FindAnyObjectByType<ResultScreen>(FindObjectsInactive.Include);
            var menu = FindAnyObjectByType<MenuScreen>(FindObjectsInactive.Include);
            if (game != null && coinFlip != null && result != null && menu != null)
            {
                game.Wire();
                result.Wire();
                menu.Wire();
            }
            else
            {
                if (game != null || coinFlip != null || result != null || menu != null)
                    Debug.LogWarning("[TRAVEE] Interfața din scenă e incompletă (lipsește un ecran): o construiesc din cod. " +
                                     "Rulează TRAVEE → Scena editabilă → 1. Pune totul în scenă.");
                foreach (var stale in new MonoBehaviour[] { game, coinFlip, result, menu })
                    if (stale != null) stale.gameObject.SetActive(false);

                var canvas = UIFactory.CreateCanvas(transform);
                // Ordinea conteaza: ultimul creat e desenat deasupra.
                game = GameScreen.Create(canvas, withBoard: !is3D);
                coinFlip = CoinFlipScreen.Create(canvas);
                result = ResultScreen.Create(canvas, overScene: is3D);
                menu = MenuScreen.Create(canvas, overScene: is3D);
            }

            var controller = gameObject.AddComponent<GameController>();
            if (is3D)
            {
                WorldFactory.Template = worldMaterial;
                // PlayerPrefs = mic „registru” de setari pastrat intre rulari (ca un fisier de config).
                int themeCount = System.Enum.GetValues(typeof(WorldTheme)).Length;
                var theme = (WorldTheme)Mathf.Clamp(PlayerPrefs.GetInt(ThemeKey, (int)worldTheme), 0, themeCount - 1);
                // Tot ce exista deja in scena (pus din editor) e folosit ca atare; ce lipseste se creeaza din cod.
                var board = FindAnyObjectByType<Board3DView>();            // obiectul „Tabla”
                var sceneCamera = Camera.main;
                bool cameraFromScene = sceneCamera != null && sceneCamera.GetComponent<DesktopHeadLook>() != null;
                var boardPosition = board != null ? board.transform.position : new Vector3(0f, boardHeight, boardDistance);

                var room = Room3D.Build(transform, assets, theme, boardPosition, cameraFromScene);
                if (!cameraFromScene) room.Camera.gameObject.AddComponent<DesktopHeadLook>(); // in VR: capul pacientului

                if (board != null)
                {
                    board.Init(room.Camera, boardSeeThroughMaterial);
                }
                else
                {
                    board = Board3DView.Create(room.Root, room.BoardPosition, Room3D.DefaultBoardRotation, room.Camera,
                        boardSeeThroughMaterial);
                    board.transform.localScale = Vector3.one * Room3D.BoardScale;
                }

                var opponent = FindAnyObjectByType<OpponentCharacter>();     // obiectul „Personaj”
                if (opponent == null) opponent = CreateOpponent(room, board);
                else if (!opponent.Attach(opponentAnimator, room, board)) opponent = null;

                controller.Init(difficulty, affectedSide, aiMoveDelay, logData, randomSeed,
                    menu, coinFlip, game, result, board, opponent);
                controller.AddInputSource(board);
                if (opponent != null) opponent.Enter();

                menu.SetTheme(EnvironmentBuilder.DisplayName(room.Theme));
                menu.ThemeToggleRequested += () => NextTheme(room, menu);
            }
            else
            {
                EnsureCamera2D();
                controller.Init(difficulty, affectedSide, aiMoveDelay, logData, randomSeed,
                    menu, coinFlip, game, result, game.Board);
                controller.AddInputSource(game.Board);
            }
        }

        /// <summary>Butonul „Peisaj” din meniu: trece la urmatorul peisaj si il tine minte pentru data viitoare.</summary>
        static void NextTheme(Room3D room, MenuScreen menu)
        {
            int count = System.Enum.GetValues(typeof(WorldTheme)).Length;
            room.SetTheme((WorldTheme)(((int)room.Theme + 1) % count));
            menu.SetTheme(EnvironmentBuilder.DisplayName(room.Theme));
            PlayerPrefs.SetInt(ThemeKey, (int)room.Theme);
        }

        OpponentCharacter CreateOpponent(Room3D room, Board3DView board)
        {
            if (opponentModel == null || opponentAnimator == null)
            {
                Debug.LogWarning("[TRAVEE] Lipsește modelul sau Animator Controller-ul personajului. " +
                                 "Rulează TRAVEE → Creează scena 3D. Jocul merge și fără personaj.");
                return null;
            }

            var model = Instantiate(opponentModel, room.Root);
            model.name = "Opponent";
            return OpponentCharacter.Create(model, opponentAnimator, room, board, opponentPlacement, opponentReach);
        }

        static void EnsureCamera2D()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.orthographic = true;
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Background;
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
    }
}
