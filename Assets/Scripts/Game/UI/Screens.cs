using System;
using System.Collections;
using Travee.TicTacToe.Audio;
using Travee.TicTacToe.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Travee.TicTacToe.UI
{
    public static class Texts
    {
        public static string LevelName(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Level1: return "Nivel 1 – Foarte ușor";
                case DifficultyLevel.Level2: return "Nivel 2 – Ușor";
                case DifficultyLevel.Level3: return "Nivel 3 – Mediu";
                default: return "Nivel 4 – Provocare";
            }
        }

        public static string SideName(AffectedSide side) => side == AffectedSide.Left ? "Stânga" : "Dreapta";
    }

    // ------------------------------------------------------------------------------------------ Meniu

    public sealed class MenuScreen : MonoBehaviour
    {
        Text sideLabel;
        Text themeLabel;

        public event Action<DifficultyLevel> LevelChosen;
        public event Action SideToggleRequested;
        /// <summary>Doar în 3D: pacientul/terapeutul cere următorul peisaj.</summary>
        public event Action ThemeToggleRequested;

        /// <param name="overScene">3D: meniul e un card în stânga, iar scena (cu personajul) se vede în dreapta.</param>
        public static MenuScreen Create(Transform parent, bool overScene = false)
        {
            var root = UIFactory.Panel("MenuScreen", parent, overScene ? Color.clear : Theme.Background);
            root.rectTransform.Stretch();
            var screen = root.gameObject.AddComponent<MenuScreen>();

            Transform container = root.transform;
            if (overScene)
            {
                var card = UIFactory.Panel("Card", root.transform, Theme.Card);
                card.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(470, 0), new Vector2(860, 1020));
                container = card.transform;
            }
            screen.Build(container, withTheme: overScene);
            return screen;
        }

        void Build(Transform container, bool withTheme)
        {
            UIFactory.Label("Title", container, "X și 0", 110, Theme.Text, FontStyle.Bold)
                .rectTransform.Centered(new Vector2(0, 400), new Vector2(1200, 140));
            UIFactory.Label("Subtitle", container, "Alege nivelul de dificultate", 44, Theme.TextMuted)
                .rectTransform.Centered(new Vector2(0, 300), new Vector2(1200, 70));

            var levels = (DifficultyLevel[])Enum.GetValues(typeof(DifficultyLevel));
            for (int i = 0; i < levels.Length; i++)
            {
                var level = levels[i];
                var button = UIFactory.Button($"Level{(int)level}", container, Texts.LevelName(level), 46,
                    Theme.Button, Theme.ButtonText, () => LevelChosen?.Invoke(level));
                ((RectTransform)button.transform).Centered(new Vector2(0, 170 - i * 135), new Vector2(760, 110));
            }

            var sideButton = UIFactory.Button("AffectedSide", container, "", 36, Theme.ButtonSecondary, Theme.Text,
                () => SideToggleRequested?.Invoke());
            ((RectTransform)sideButton.transform).Centered(new Vector2(0, withTheme ? -440 : -420),
                new Vector2(620, withTheme ? 70 : 80));
            sideLabel = UIFactory.LabelOf(sideButton);

            if (!withTheme) return;
            var themeButton = UIFactory.Button("WorldTheme", container, "", 36, Theme.ButtonSecondary, Theme.Text,
                () => ThemeToggleRequested?.Invoke());
            ((RectTransform)themeButton.transform).Centered(new Vector2(0, -355), new Vector2(620, 70));
            themeLabel = UIFactory.LabelOf(themeButton);
        }

        public void SetTheme(string themeName)
        {
            if (themeLabel != null) themeLabel.text = $"Peisaj: {themeName}";
        }

        public void SetAffectedSide(AffectedSide side) => sideLabel.text = $"Mâna afectată: {Texts.SideName(side)}";
    }

    // ------------------------------------------------------------------------------------------ Coin flip

    public sealed class CoinFlipScreen : MonoBehaviour
    {
        static readonly Color PatientFace = new Color(0.25f, 0.55f, 0.90f);
        static readonly Color AIFace = new Color(0.95f, 0.62f, 0.25f);

        Text title;
        Image coin;
        Text coinLabel;
        Text resultLabel;

        public static CoinFlipScreen Create(Transform parent)
        {
            var root = UIFactory.Panel("CoinFlipScreen", parent, Theme.Overlay);
            root.rectTransform.Stretch();
            var screen = root.gameObject.AddComponent<CoinFlipScreen>();
            screen.Build();
            return screen;
        }

        void Build()
        {
            title = UIFactory.Label("Title", transform, "", 60, Theme.Text, FontStyle.Bold);
            title.rectTransform.Centered(new Vector2(0, 330), new Vector2(1400, 90));

            coin = UIFactory.Panel("Coin", transform, PatientFace);
            coin.sprite = UIFactory.Circle;
            coin.rectTransform.Centered(new Vector2(0, 20), new Vector2(340, 340));

            coinLabel = UIFactory.Label("Face", coin.transform, "", 110, Color.white, FontStyle.Bold);
            coinLabel.rectTransform.Stretch();

            resultLabel = UIFactory.Label("Result", transform, "", 56, Theme.Text, FontStyle.Bold);
            resultLabel.rectTransform.Centered(new Vector2(0, -300), new Vector2(1400, 90));
        }

        /// <summary>Animația aruncării monedei; rezultatul e deja decis de <see cref="CoinFlip"/>.</summary>
        public IEnumerator Play(bool patientStarts, float resultHoldSeconds = 1.6f)
        {
            title.text = "Aruncăm moneda…";
            resultLabel.text = "";

            int halfTurns = UnityEngine.Random.Range(7, 10);
            // Fața inițială e aleasă astfel încât după halfTurns întoarceri să ajungă la rezultatul corect.
            bool showingPatient = (halfTurns % 2 == 0) == patientStarts;
            SetFace(showingPatient);

            for (int i = 0; i < halfTurns; i++)
            {
                float duration = Mathf.Lerp(0.06f, 0.22f, i / (float)(halfTurns - 1)); // încetinește spre final
                yield return Scale(1f, 0f, duration);
                showingPatient = !showingPatient;
                SetFace(showingPatient);
                Sfx.Play(Sound.CoinTick, 0.7f);
                yield return Scale(0f, 1f, duration);
            }

            title.text = patientStarts ? "Ai câștigat aruncarea!" : "AI-ul a câștigat aruncarea";
            resultLabel.text = patientStarts ? "Începi tu – tu ești X" : "Începe AI-ul – tu ești 0";
            Sfx.Play(Sound.CoinResult);
            yield return new WaitForSeconds(resultHoldSeconds);
        }

        void SetFace(bool patient)
        {
            coin.color = patient ? PatientFace : AIFace;
            coinLabel.text = patient ? "TU" : "AI";
        }

        IEnumerator Scale(float from, float to, float duration)
        {
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                coin.transform.localScale = new Vector3(Mathf.Lerp(from, to, t / duration), 1f, 1f);
                yield return null;
            }
            coin.transform.localScale = new Vector3(to, 1f, 1f);
        }
    }

    // ------------------------------------------------------------------------------------------ Joc

    public sealed class GameScreen : MonoBehaviour
    {
        Text header;
        Text playerInfo;
        Text status;

        public BoardView Board { get; private set; }

        public event Action MenuRequested;

        /// <param name="withBoard">false în 3D: tabla e pe masă, ecranul are doar textele și e transparent.</param>
        public static GameScreen Create(Transform parent, bool withBoard = true)
        {
            var root = UIFactory.Panel("GameScreen", parent, withBoard ? Theme.Background : Color.clear);
            root.rectTransform.Stretch();
            root.raycastTarget = withBoard; // în 3D click-urile trebuie să treacă spre tabla de pe masă
            var screen = root.gameObject.AddComponent<GameScreen>();
            screen.Build(withBoard);
            return screen;
        }

        void Build(bool withBoard)
        {
            header = UIFactory.Label("Header", transform, "", 48, Theme.Text, FontStyle.Bold);
            playerInfo = UIFactory.Label("PlayerInfo", transform, "", 38, Theme.TextMuted);
            status = UIFactory.Label("Status", transform, "", 46, Theme.Text, FontStyle.Bold);

            if (withBoard)
            {
                header.rectTransform.Centered(new Vector2(0, 470), new Vector2(1200, 70));
                playerInfo.rectTransform.Centered(new Vector2(0, 405), new Vector2(1200, 60));
                status.rectTransform.Centered(new Vector2(0, -440), new Vector2(1400, 70));
                Board = BoardView.Create(transform, new Vector2(0, -20), 700);
            }
            else
            {
                // 3D: textele stau în dreapta-sus, ca să nu acopere personajul sau tabla.
                var corner = new Vector2(1, 1);
                foreach (var label in new[] { header, playerInfo, status }) label.alignment = TextAnchor.MiddleRight;
                header.rectTransform.Place(corner, new Vector2(-380, -60), new Vector2(700, 70));
                playerInfo.rectTransform.Place(corner, new Vector2(-380, -120), new Vector2(700, 60));
                status.rectTransform.Place(corner, new Vector2(-380, -190), new Vector2(700, 70));

                // Un card semi-opac în spatele textelor: altfel nu se citesc peste peisaj (mai ales noaptea).
                var infoCard = UIFactory.Panel("InfoCard", transform, Theme.Card);
                infoCard.rectTransform.Place(corner, new Vector2(-380, -125), new Vector2(740, 230));
                infoCard.raycastTarget = false;
                infoCard.transform.SetAsFirstSibling();     // primul copil = desenat sub celelalte

                // Indiciul de control stă în stânga-jos, ca să nu se suprapună cu tabla.
                var hintCard = UIFactory.Panel("HintCard", transform, Theme.Card);
                hintCard.rectTransform.Place(new Vector2(0, 0), new Vector2(470, 50), new Vector2(900, 60));
                hintCard.raycastTarget = false;
                var hint = UIFactory.Label("LookHint", hintCard.transform,
                    "Click dreapta + mouse: privește în jur   •   Space: înainte", 28, Theme.TextMuted);
                hint.rectTransform.Stretch();
            }

            var menu = UIFactory.Button("Menu", transform, "Meniu", 34, Theme.ButtonSecondary, Theme.Text,
                () => MenuRequested?.Invoke());
            ((RectTransform)menu.transform).Place(new Vector2(0, 1), new Vector2(130, -60), new Vector2(200, 80));
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

    // ------------------------------------------------------------------------------------------ Rezultat

    public sealed class ResultScreen : MonoBehaviour
    {
        Text title;
        Text message;

        public event Action PlayAgainRequested;
        public event Action MenuRequested;

        /// <param name="overScene">3D: rezultatul e un card jos, ca personajul (care aplaudă) să rămână vizibil.</param>
        public static ResultScreen Create(Transform parent, bool overScene = false)
        {
            // Rădăcina rămâne țintă de raycast (chiar transparentă), ca să blocheze click-urile spre tablă.
            var root = UIFactory.Panel("ResultScreen", parent, overScene ? Color.clear : Theme.Overlay);
            root.rectTransform.Stretch();
            var screen = root.gameObject.AddComponent<ResultScreen>();
            if (overScene)
            {
                var card = UIFactory.Panel("Card", root.transform, Theme.Card);
                card.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0, 260), new Vector2(1500, 440));
                screen.Build(card.transform, compact: true);
            }
            else
            {
                screen.Build(root.transform, compact: false);
            }
            return screen;
        }

        void Build(Transform container, bool compact)
        {
            title = UIFactory.Label("Title", container, "", compact ? 72 : 90, Theme.Text, FontStyle.Bold);
            title.rectTransform.Centered(new Vector2(0, compact ? 140 : 200), new Vector2(1400, 110));

            message = UIFactory.Label("Message", container, "", compact ? 38 : 44, Theme.TextMuted);
            message.rectTransform.Centered(new Vector2(0, compact ? 50 : 70), new Vector2(1300, 110));

            var again = UIFactory.Button("PlayAgain", container, "Joacă din nou", 46, Theme.Button, Theme.ButtonText,
                () => PlayAgainRequested?.Invoke());
            ((RectTransform)again.transform).Centered(compact ? new Vector2(-330, -120) : new Vector2(0, -120),
                new Vector2(620, 110));

            var menu = UIFactory.Button("Menu", container, "Alege alt nivel", 40, Theme.ButtonSecondary, Theme.Text,
                () => MenuRequested?.Invoke());
            ((RectTransform)menu.transform).Centered(compact ? new Vector2(330, -120) : new Vector2(0, -260),
                new Vector2(620, compact ? 110 : 100));
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
