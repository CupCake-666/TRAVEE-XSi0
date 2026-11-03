using Travee.TicTacToe.Core;
using Travee.TicTacToe.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Travee.TicTacToe
{
    /// <summary>
    /// Singurul component care trebuie pus în scenă: construiește din cod camera, UI-ul și controller-ul.
    /// Toți parametrii de terapie / dificultate sunt aici, editabili din Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TicTacToeBootstrap : MonoBehaviour
    {
        [Header("Pacient")]
        [SerializeField] AffectedSide affectedSide = AffectedSide.Left;

        [Header("Dificultate")]
        [SerializeField] DifficultySettings difficulty = new DifficultySettings();
        [SerializeField, Min(0f), Tooltip("Pauza (secunde) înainte ca AI-ul să mute, ca pacientul să urmărească mutarea.")]
        float aiMoveDelay = 0.8f;

        [Header("Date")]
        [SerializeField, Tooltip("Salvează games.csv și moves.csv în persistentDataPath/TRAVEE/XSi0.")]
        bool logData = true;
        [SerializeField, Tooltip("0 = aleator. O valoare fixă face meciurile reproductibile.")]
        int randomSeed;

        void Awake()
        {
            EnsureCamera();
            EnsureEventSystem();
            var canvas = CreateCanvas();

            // Ordinea contează: ultimul creat e desenat deasupra.
            var game = GameScreen.Create(canvas);
            var coinFlip = CoinFlipScreen.Create(canvas);
            var result = ResultScreen.Create(canvas);
            var menu = MenuScreen.Create(canvas);

            var controller = gameObject.AddComponent<GameController>();
            controller.Init(difficulty, affectedSide, aiMoveDelay, logData, randomSeed, menu, coinFlip, game, result);
        }

        static void EnsureCamera()
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
