using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Travee.TicTacToe.UI
{
    public static class Theme
    {
        public static readonly Color Background = new Color(0.93f, 0.95f, 0.97f);
        public static readonly Color Text = new Color(0.13f, 0.16f, 0.22f);
        public static readonly Color TextMuted = new Color(0.38f, 0.43f, 0.50f);
        public static readonly Color Button = new Color(0.20f, 0.45f, 0.78f);
        public static readonly Color ButtonSecondary = new Color(0.80f, 0.84f, 0.89f);
        public static readonly Color ButtonText = Color.white;
        public static readonly Color GridLine = new Color(0.25f, 0.30f, 0.38f);
        public static readonly Color Cell = Color.white;
        public static readonly Color CellWin = new Color(0.72f, 0.92f, 0.74f);
        public static readonly Color X = new Color(0.16f, 0.42f, 0.85f);
        public static readonly Color O = new Color(0.90f, 0.40f, 0.25f);
        public static readonly Color Overlay = new Color(0.93f, 0.95f, 0.97f, 0.92f);
        public static readonly Color Card = new Color(0.97f, 0.98f, 0.99f, 0.94f);

        public static Color ForMark(Core.Mark mark) => mark == Core.Mark.X ? X : O;
    }

    /// <summary>Construiește elementele de UI din cod (fără prefab-uri).</summary>
    public static class UIFactory
    {
        static Font font;
        static Sprite circle;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>Sprite circular generat procedural (pentru monedă).</summary>
        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                float r = size / 2f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d)));
                }
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                return circle;
            }
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Ancorează la <paramref name="anchor"/> (0..1) cu poziție și dimensiune fixe.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Centered(this RectTransform rt, Vector2 position, Vector2 size) =>
            rt.Place(new Vector2(0.5f, 0.5f), position, size);

        public static RectTransform Stretch(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text Label(string name, Transform parent, string text, int fontSize, Color color,
            FontStyle style = FontStyle.Normal)
        {
            var label = Rect(name, parent).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(string name, Transform parent, string text, int fontSize, Color background,
            Color textColor, UnityAction onClick)
        {
            var image = Panel(name, parent, background);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            var label = Label("Label", image.transform, text, fontSize, textColor, FontStyle.Bold);
            label.rectTransform.Stretch();

            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        public static Text LabelOf(Button button) => button.GetComponentInChildren<Text>();
    }
}
