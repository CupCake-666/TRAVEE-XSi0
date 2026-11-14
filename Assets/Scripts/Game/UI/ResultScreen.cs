using System;
using UnityEngine;
using UnityEngine.UI;

namespace Travee.TicTacToe.UI
{
    /// <summary>Ecranul de la sfarsitul meciului: rezultatul, „Joaca din nou” si „Alege alt nivel”.</summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        [Header("Legături spre obiectele din scenă")]
        [SerializeField] Text title;
        [SerializeField] Text message;
        [SerializeField] Button againButton;
        [SerializeField] Button menuButton;

        bool wired;

        public event Action PlayAgainRequested;
        public event Action MenuRequested;

        /// <param name="overScene">3D: rezultatul e un card in dreapta, ca tabla (cu linia castigatoare) si personajul sa ramana vizibile.</param>
        public static ResultScreen Create(Transform parent, bool overScene = false)
        {
            // Radacina ramane tinta de raycast (chiar transparenta), ca sa blocheze click-urile spre tabla.
            var root = UIFactory.Panel("ResultScreen", parent, overScene ? Color.clear : Theme.Overlay);
            root.rectTransform.Stretch();
            var screen = root.gameObject.AddComponent<ResultScreen>();
            if (overScene)
            {
                var card = UIFactory.Panel("Card", root.transform, Theme.Card);
                card.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-320, -130), new Vector2(580, 560));
                screen.Build(card.transform, compact: true);
            }
            else
            {
                screen.Build(root.transform, compact: false);
            }
            screen.Wire();
            return screen;
        }

        void Build(Transform container, bool compact)
        {
            // Textele de aici sunt doar exemple pentru editor; jocul le inlocuieste (vezi Show).
            // compact (3D): un card ingust, cu textele si butoanele unul sub altul.
            title = UIFactory.Label("Title", container, "Felicitări, ai câștigat!", compact ? 50 : 90, Theme.X, FontStyle.Bold);
            title.rectTransform.Centered(new Vector2(0, compact ? 190 : 200), compact ? new Vector2(540, 140) : new Vector2(1400, 110));

            message = UIFactory.Label("Message", container, "Ai jucat excelent.", compact ? 30 : 44, Theme.TextMuted);
            message.rectTransform.Centered(new Vector2(0, compact ? 60 : 70), compact ? new Vector2(520, 120) : new Vector2(1300, 110));

            againButton = UIFactory.Button("PlayAgain", container, "Joacă din nou", compact ? 40 : 46, Theme.Button, Theme.ButtonText);
            ((RectTransform)againButton.transform).Centered(compact ? new Vector2(0, -80) : new Vector2(0, -120),
                compact ? new Vector2(500, 100) : new Vector2(620, 110));

            menuButton = UIFactory.Button("Menu", container, "Alege alt nivel", compact ? 34 : 40, Theme.ButtonSecondary, Theme.Text);
            ((RectTransform)menuButton.transform).Centered(compact ? new Vector2(0, -200) : new Vector2(0, -260),
                compact ? new Vector2(500, 90) : new Vector2(620, 100));
        }

        void Awake() => Wire();

        /// <summary>Leaga butoanele de logica (se reface la fiecare pornire; a doua apelare nu mai face nimic).</summary>
        public void Wire()
        {
            if (wired || againButton == null) return;
            wired = true;
            UIFactory.OnClick(againButton, () => PlayAgainRequested?.Invoke());
            UIFactory.OnClick(menuButton, () => MenuRequested?.Invoke());
        }

        public void Show(string titleText, string messageText, Color titleColor)
        {
            title.text = titleText;
            title.color = titleColor;
            message.text = messageText;
            gameObject.SetActive(true);
        }
    }
}
