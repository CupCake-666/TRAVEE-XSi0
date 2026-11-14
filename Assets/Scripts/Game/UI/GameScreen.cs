using System;
using Travee.TicTacToe.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Travee.TicTacToe.UI
{
    /// <summary>
    /// Ecranul din timpul meciului: nivelul, cine e X si cine e 0, al cui e randul, butonul „Meniu”.
    /// In 2D contine si tabla; in 3D e transparent si are doar textele.
    /// </summary>
    public sealed class GameScreen : MonoBehaviour
    {
        [Header("Legături spre obiectele din scenă")]
        [SerializeField] Text header;
        [SerializeField] Text playerInfo;
        [SerializeField] Text status;
        [SerializeField] Button menuButton;

        bool wired;

        public BoardView Board { get; private set; }

        public event Action MenuRequested;

        /// <param name="withBoard">false in 3D: tabla e un obiect din lume, ecranul are doar textele si e transparent.</param>
        public static GameScreen Create(Transform parent, bool withBoard = true)
        {
            var root = UIFactory.Panel("GameScreen", parent, withBoard ? Theme.Background : Color.clear);
            root.rectTransform.Stretch();
            root.raycastTarget = withBoard; // in 3D click-urile trebuie sa treaca spre tabla din lume
            var screen = root.gameObject.AddComponent<GameScreen>();
            screen.Build(withBoard);
            screen.Wire();
            return screen;
        }

        void Build(bool withBoard)
        {
            // Textele de aici sunt doar exemple, ca sa ai ce vedea in editor; jocul le inlocuieste la pornire.
            header = UIFactory.Label("Header", transform, Texts.LevelName(DifficultyLevel.Level1), 48, Theme.Text, FontStyle.Bold);
            playerInfo = UIFactory.Label("PlayerInfo", transform, "Tu: X     AI: 0", 38, Theme.TextMuted);
            status = UIFactory.Label("Status", transform, "Rândul tău – alege o căsuță", 46, Theme.Text, FontStyle.Bold);

            if (withBoard)
            {
                header.rectTransform.Centered(new Vector2(0, 470), new Vector2(1200, 70));
                playerInfo.rectTransform.Centered(new Vector2(0, 405), new Vector2(1200, 60));
                status.rectTransform.Centered(new Vector2(0, -440), new Vector2(1400, 70));
                Board = BoardView.Create(transform, new Vector2(0, -20), 700);
            }
            else
            {
                // 3D: textele stau in dreapta-sus, ca sa nu acopere personajul sau tabla.
                var corner = new Vector2(1, 1);
                foreach (var label in new[] { header, playerInfo, status }) label.alignment = TextAnchor.MiddleRight;
                header.rectTransform.Place(corner, new Vector2(-380, -60), new Vector2(700, 70));
                playerInfo.rectTransform.Place(corner, new Vector2(-380, -120), new Vector2(700, 60));
                status.rectTransform.Place(corner, new Vector2(-380, -190), new Vector2(700, 70));

                // Un card semi-opac in spatele textelor: altfel nu se citesc peste peisaj (mai ales noaptea).
                var infoCard = UIFactory.Panel("InfoCard", transform, Theme.Card);
                infoCard.rectTransform.Place(corner, new Vector2(-380, -125), new Vector2(740, 230));
                infoCard.raycastTarget = false;
                infoCard.transform.SetAsFirstSibling();     // primul copil = desenat sub celelalte

                // Indiciul de control sta in stanga-jos, ca sa nu se suprapuna cu tabla.
                var hintCard = UIFactory.Panel("HintCard", transform, Theme.Card);
                hintCard.rectTransform.Place(new Vector2(0, 0), new Vector2(470, 50), new Vector2(900, 60));
                hintCard.raycastTarget = false;
                var hint = UIFactory.Label("LookHint", hintCard.transform,
                    "Click dreapta + mouse: privește în jur   •   Space: înainte", 28, Theme.TextMuted);
                hint.rectTransform.Stretch();
            }

            menuButton = UIFactory.Button("Menu", transform, "Meniu", 34, Theme.ButtonSecondary, Theme.Text);
            ((RectTransform)menuButton.transform).Place(new Vector2(0, 1), new Vector2(130, -60), new Vector2(200, 80));
        }

        void Awake() => Wire();

        /// <summary>Leaga butonul de logica (se reface la fiecare pornire; a doua apelare nu mai face nimic).</summary>
        public void Wire()
        {
            if (wired || menuButton == null) return;
            wired = true;
            UIFactory.OnClick(menuButton, () => MenuRequested?.Invoke());
        }

        public void Setup(DifficultyLevel level)
        {
            header.text = Texts.LevelName(level);
            playerInfo.text = "";
            status.text = "";
        }

        public void SetPlayers(Mark patientMark)
        {
            playerInfo.text = $"Tu: {patientMark.Symbol()}     AI: {patientMark.Opponent().Symbol()}";
        }

        public void SetStatus(string text) => status.text = text;
    }
}
