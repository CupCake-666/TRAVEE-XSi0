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
    /// Singurul component care trebuie pus în scenă: construiește din cod UI-ul, (în 3D) camera de joc cu tabla
    /// și personajul, apoi controller-ul. Parametrii de terapie / dificultate sunt aici, în Inspector.
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
        [SerializeField, Tooltip("Unde stă personajul când așteaptă. Doar pentru 3D.")]
        OpponentPlacement opponentPlacement = OpponentPlacement.AffectedSide;
        [SerializeField, Tooltip("Material URP transparent (creat de meniul TRAVEE), pentru tabla care devine transparentă.")]
        Material boardSeeThroughMaterial;

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

        void Awake()
        {
            bool is3D = viewMode == ViewMode.ThreeD;

            EnsureEventSystem();
            var canvas = CreateCanvas();

            // Ordinea contează: ultimul creat e desenat deasupra.
            var game = GameScreen.Create(canvas, withBoard: !is3D);
            var coinFlip = CoinFlipScreen.Create(canvas);
            var result = ResultScreen.Create(canvas, overScene: is3D);
            var menu = MenuScreen.Create(canvas, overScene: is3D);

            var controller = gameObject.AddComponent<GameController>();
            if (is3D)
            {
                var room = Room3D.Build(transform);
                room.Camera.gameObject.AddComponent<DesktopHeadLook>(); // în VR: capul pacientului
                var board = Board3DView.Create(room.Root, room.BoardPosition, room.BoardRotation, room.Camera,
                    boardSeeThroughMaterial);
                board.transform.localScale = Vector3.one * Room3D.BoardScale;
                var opponent = CreateOpponent(room, board);

                controller.Init(difficulty, affectedSide, aiMoveDelay, logData, randomSeed,
                    menu, coinFlip, game, result, board, opponent);
                controller.AddInputSource(board);
                if (opponent != null) opponent.Enter();
            }
            else
            {
                EnsureCamera2D();
                controller.Init(difficulty, affectedSide, aiMoveDelay, logData, randomSeed,
                    menu, coinFlip, game, result, game.Board);
                controller.AddInputSource(game.Board);
            }
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
            return OpponentCharacter.Create(model, opponentAnimator, room, board, opponentPlacement);
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

        Transform CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // scalează după înălțime: tabla rămâne mereu întreagă pe ecran

            go.AddComponent<GraphicRaycaster>();
            return go.transform;
        }
    }
}
