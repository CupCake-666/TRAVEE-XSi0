using System;
using System.Collections;
using Travee.TicTacToe.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Travee.TicTacToe.UI
{
    /// <summary>
    /// Tabla 2D. Este si sursa de input pentru mouse/touch (prin butoanele uGUI).
    /// In VR se va adauga o alta implementare de <see cref="ICellInputSource"/> (atingerea cu mana).
    /// </summary>
    public sealed class BoardView : MonoBehaviour, IBoardView, ICellInputSource
    {
        readonly Image[] cellImages = new Image[Board.CellCount];
        readonly Text[] cellTexts = new Text[Board.CellCount];

        public event Action<int> CellSelected;

        public bool InputEnabled { get; set; }

        public static BoardView Create(Transform parent, Vector2 position, float size)
        {
            var background = UIFactory.Panel("Board", parent, Theme.GridLine);
            background.rectTransform.Centered(position, new Vector2(size, size));
            var view = background.gameObject.AddComponent<BoardView>();
            view.Build(size);
            return view;
        }

        void Build(float size)
        {
            float gap = size * 0.025f;
            float cell = (size - gap * 4f) / Board.Size;

            for (int i = 0; i < Board.CellCount; i++)
            {
                int index = i;
                float x = -size / 2f + gap + cell / 2f + Board.Column(i) * (cell + gap);
                float y = size / 2f - gap - cell / 2f - Board.Row(i) * (cell + gap);

                var button = UIFactory.Button($"Cell_{i}", transform, "", Mathf.RoundToInt(cell * 0.75f),
                    Theme.Cell, Theme.Text, () => OnCellClicked(index));
                ((RectTransform)button.transform).Centered(new Vector2(x, y), new Vector2(cell, cell));

                cellImages[i] = (Image)button.targetGraphic;
                cellTexts[i] = UIFactory.LabelOf(button);
            }
        }

        void OnCellClicked(int cell)
        {
            if (InputEnabled) CellSelected?.Invoke(cell);
        }

        public void ResetBoard()
        {
            StopAllCoroutines();
            for (int i = 0; i < Board.CellCount; i++)
            {
                cellTexts[i].text = "";
                cellTexts[i].transform.localScale = Vector3.one;
                cellImages[i].color = Theme.Cell;
            }
        }

        public void SetMark(int cell, Mark mark)
        {
            cellTexts[cell].text = mark.Symbol();
            cellTexts[cell].color = Theme.ForMark(mark);
            StartCoroutine(Pop(cellTexts[cell].transform));
        }

        public void HighlightLine(int[] line)
        {
            if (line == null) return;
            foreach (int cell in line) cellImages[cell].color = Theme.CellWin;
        }

        static IEnumerator Pop(Transform target)
        {
            const float duration = 0.25f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float scale = Mathf.LerpUnclamped(0.3f, 1f, 1f + 2.7f * Mathf.Pow(k - 1f, 3) + 1.7f * Mathf.Pow(k - 1f, 2));
                target.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            target.localScale = Vector3.one;
        }
    }
}
