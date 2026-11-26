using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TerritoryIO.View
{
    /// <summary>
    /// Small helpers that build uGUI elements in code (canvas, text, buttons, panels, text fields),
    /// so the menus need no prefabs. Layout is designed for a 1920×1080 screen and scales to any size.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color ButtonColor = new Color(0.10f, 0.14f, 0.27f, 0.95f);
        public static readonly Color PanelColor = new Color(0.04f, 0.05f, 0.10f, 0.88f);
        public static readonly Color Accent = new Color(0f, 0.9f, 1f);

        private static Font font;

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return font;
            }
        }

        public static Canvas MakeCanvas(GameObject go, int sortingOrder)
        {
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>Buttons need an EventSystem in the scene. Creates one (for the input system in use) if missing.</summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform Rect(GameObject go, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Stretches a RectTransform over its whole parent.</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static Text Text(Transform parent, string name, string text, int fontSize, TextAnchor align,
            Vector2 anchor, Vector2 pos, Vector2 size, bool outline = true)
        {
            var go = outline
                ? new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline))
                : new GameObject(name, typeof(RectTransform), typeof(Text));
            Rect(go, parent, anchor, pos, size);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            if (outline) go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            return t;
        }

        /// <summary>A centred panel (dark box). Use anchor/pos to place it elsewhere.</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = Rect(go, parent, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            go.GetComponent<Image>().color = color;
            return rt;
        }

        public static Button Button(Transform parent, string label, Vector2 pos, Vector2 size, Action onClick,
            int fontSize = 40, Vector2? anchor = null)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            Rect(go, parent, anchor ?? new Vector2(0.5f, 0.5f), pos, size);
            var img = go.GetComponent<Image>();
            img.color = ButtonColor;

            var b = go.GetComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.6f, 2.2f, 2.6f);   // tints the dark blue toward cyan
            colors.selectedColor = new Color(1.6f, 2.2f, 2.6f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.colorMultiplier = 1.5f;
            colors.fadeDuration = 0.08f;
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(() => onClick());

            var t = Text(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Stretch(t.rectTransform);
            return b;
        }

        public static void SetLabel(Button b, string label)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = label;
        }

        public static InputField InputField(Transform parent, string value, Vector2 pos, Vector2 size, int fontSize,
            int characterLimit, Action<string> onEndEdit)
        {
            var go = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(InputField));
            Rect(go, parent, new Vector2(0.5f, 0.5f), pos, size);
            go.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.07f, 1f);

            var text = Text(go.transform, "Text", "", fontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, size, false);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16, 0);
            text.rectTransform.offsetMax = new Vector2(-16, 0);
            text.supportRichText = false; // required by InputField
            text.horizontalOverflow = HorizontalWrapMode.Wrap;

            var placeholder = Text(go.transform, "Placeholder", "Your name", fontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, size, false);
            Stretch(placeholder.rectTransform);
            placeholder.color = new Color(1f, 1f, 1f, 0.3f);

            var field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = characterLimit;
            field.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
            field.text = value;
            if (onEndEdit != null) field.onEndEdit.AddListener(v => onEndEdit(v));
            return field;
        }

        public static string Hex(Color32 c) => $"{c.r:X2}{c.g:X2}{c.b:X2}";
    }
}
