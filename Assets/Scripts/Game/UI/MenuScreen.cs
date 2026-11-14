using System;
using Travee.TicTacToe.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Travee.TicTacToe.UI
{
    /// <summary>
    /// Meniul: alegerea nivelului, a mainii afectate si (in 3D) a peisajului.
    /// Obiectele lui (card, texte, butoane) pot fi salvate in scena si aranjate din editor; componenta tine doar
    /// legaturile spre ele (campurile de mai jos) si logica: ce se intampla la click.
    /// </summary>
    public sealed class MenuScreen : MonoBehaviour
    {
        [Header("Legături spre obiectele din scenă")]
        [SerializeField, Tooltip("Butoanele de nivel, în ordine: Nivel 1, 2, 3, 4.")] Button[] levelButtons;
        [SerializeField] Button sideButton;
        [SerializeField] Text sideLabel;
        [SerializeField, Tooltip("Doar în 3D.")] Button themeButton;
        [SerializeField] Text themeLabel;

        bool wired;

        public event Action<DifficultyLevel> LevelChosen;
        public event Action SideToggleRequested;
        /// <summary>Doar in 3D: pacientul/terapeutul cere urmatorul peisaj.</summary>
        public event Action ThemeToggleRequested;

        /// <summary>Creeaza meniul din cod (folosit daca nu exista in scena si de comanda din editor care il pune in scena).</summary>
        /// <param name="overScene">3D: meniul e un card in stanga, iar scena (cu personajul) se vede in dreapta.</param>
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
            screen.Wire();
            return screen;
        }

        void Build(Transform container, bool withTheme)
        {
            UIFactory.Label("Title", container, "X și 0", 110, Theme.Text, FontStyle.Bold)
                .rectTransform.Centered(new Vector2(0, 400), new Vector2(1200, 140));
            UIFactory.Label("Subtitle", container, "Alege nivelul de dificultate", 44, Theme.TextMuted)
                .rectTransform.Centered(new Vector2(0, 300), new Vector2(1200, 70));

            var levels = (DifficultyLevel[])Enum.GetValues(typeof(DifficultyLevel));
            levelButtons = new Button[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                levelButtons[i] = UIFactory.Button($"Level{(int)levels[i]}", container, Texts.LevelName(levels[i]), 46,
                    Theme.Button, Theme.ButtonText);
                ((RectTransform)levelButtons[i].transform).Centered(new Vector2(0, 170 - i * 135), new Vector2(760, 110));
            }

            sideButton = UIFactory.Button("AffectedSide", container, "Mâna afectată: Stânga", 36, Theme.ButtonSecondary, Theme.Text);
            ((RectTransform)sideButton.transform).Centered(new Vector2(0, withTheme ? -440 : -420),
                new Vector2(620, withTheme ? 70 : 80));
            sideLabel = UIFactory.LabelOf(sideButton);

            if (!withTheme) return;
            themeButton = UIFactory.Button("WorldTheme", container, "Peisaj: Pădure", 36, Theme.ButtonSecondary, Theme.Text);
            ((RectTransform)themeButton.transform).Centered(new Vector2(0, -355), new Vector2(620, 70));
            themeLabel = UIFactory.LabelOf(themeButton);
        }

        void Awake() => Wire();

        /// <summary>
        /// Leaga butoanele de logica. Legaturile facute din cod nu se salveaza in scena, deci se refac la fiecare
        /// pornire. Se poate apela de mai multe ori: a doua oara nu mai face nimic.
        /// </summary>
        public void Wire()
        {
            if (wired || levelButtons == null || levelButtons.Length == 0) return;
            wired = true;

            var levels = (DifficultyLevel[])Enum.GetValues(typeof(DifficultyLevel));
            for (int i = 0; i < levelButtons.Length && i < levels.Length; i++)
            {
                var level = levels[i];      // copie locala: fiecare buton isi tine minte propriul nivel
                UIFactory.OnClick(levelButtons[i], () => LevelChosen?.Invoke(level));
            }
            UIFactory.OnClick(sideButton, () => SideToggleRequested?.Invoke());
            UIFactory.OnClick(themeButton, () => ThemeToggleRequested?.Invoke());
        }

        public void SetTheme(string themeName)
        {
            if (themeLabel != null) themeLabel.text = $"Peisaj: {themeName}";
        }

        public void SetAffectedSide(AffectedSide side)
        {
            if (sideLabel != null) sideLabel.text = $"Mâna afectată: {Texts.SideName(side)}";
        }
    }
}
