using UnityEngine;
using UnityEngine.UI;

namespace HarborRacer.UI
{
    /// <summary>Tiny helpers that build uGUI elements from code (the HUD is created at runtime, so no prefab authoring is needed).</summary>
    public static class UiKit
    {
        static Font font;
        public static Font DefaultFont
        {
            get
            {
                if (font == null) font = Resources.Load<Font>("UI/Fonts/FOT-NewRodin Pro B");           // target label font
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        static Font digitFont;
        /// <summary>Target seven-segment style digit font (DS-DIGIT) used for the timer, coins and lap numbers.</summary>
        public static Font DigitFont
        {
            get
            {
                if (digitFont == null) digitFont = Resources.Load<Font>("UI/Fonts/DS-DIGIT");
                return digitFont != null ? digitFont : DefaultFont;
            }
        }

        public static Sprite LoadSprite(string path)
        {
            var s = Resources.Load<Sprite>(path);
            if (s != null) return s;
            var tex = Resources.Load<Texture2D>(path);
            return tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)) : null;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect(name, parent, anchor, pivot, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color; img.raycastTarget = false;
            return img;
        }

        public static Text Label(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, string text, int fontSize,
                                 Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = true, Font fontOverride = null)
        {
            var rt = Rect(name, parent, anchor, pivot, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = fontOverride != null ? fontOverride : DefaultFont; t.text = text; t.fontSize = fontSize; t.color = color; t.alignment = align;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f); o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        public static Button MakeButton(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label, UnityEngine.Events.UnityAction onClick)
        {
            var img = Panel(name, parent, anchor, new Vector2(0.5f, 0.5f), pos, size, new Color(0.15f, 0.15f, 0.2f, 0.95f));
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors; colors.highlightedColor = new Color(1f, 0.85f, 0.2f); colors.pressedColor = new Color(0.9f, 0.6f, 0.1f); b.colors = colors;
            b.onClick.AddListener(onClick);
            Label("Text", img.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, label, 36, Color.white);
            return b;
        }

        public static string Ordinal(int n) => n == 1 ? "st" : n == 2 ? "nd" : n == 3 ? "rd" : "th";
        public static string Time(float t) => $"{(int)(t / 60):00}:{t % 60:00.00}";
    }
}
