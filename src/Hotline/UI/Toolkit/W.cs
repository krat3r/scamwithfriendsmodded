using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hotline.UI.Toolkit
{
    /// <summary>
    /// Builders for the UI Toolkit menu, with inline styles so no stylesheet asset is needed.
    /// Each returns the element it added, so callers can tweak it further.
    /// </summary>
    internal static class W
    {
        public static readonly Color Flame = new Color(1f, 0.42f, 0.13f);
        public static readonly Color Text = new Color(0.92f, 0.92f, 0.92f);
        public static readonly Color Muted = new Color(0.65f, 0.65f, 0.68f);
        public static readonly Color PanelBg = new Color(0.10f, 0.11f, 0.15f, 0.98f);
        public static readonly Color BoxBg = new Color(1f, 1f, 1f, 0.05f);
        public static readonly Color ButtonBg = new Color(0.22f, 0.24f, 0.30f);

        public static Label Label(VisualElement parent, string text, int size = 13, Color? color = null, bool bold = false)
        {
            var label = new Label(text);
            label.style.color = color ?? Text;
            label.style.fontSize = size;
            label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 2;
            parent.Add(label);
            return label;
        }

        public static Label Heading(VisualElement parent, string text)
        {
            var label = Label(parent, text, 16, Flame, bold: true);
            label.style.marginTop = 6;
            label.style.marginBottom = 4;
            return label;
        }

        public static Label Note(VisualElement parent, string text) => Label(parent, text, 11, Muted);

        public static VisualElement Box(VisualElement parent)
        {
            var box = new VisualElement();
            box.style.backgroundColor = BoxBg;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius =
                box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 6;
            SetPadding(box, 8);
            box.style.marginBottom = 6;
            parent.Add(box);
            return box;
        }

        public static VisualElement Row(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            parent.Add(row);
            return row;
        }

        public static void FieldRow(VisualElement parent, string label, string value, Color? valueColor = null)
        {
            var row = Row(parent);
            var name = Label(row, label, 13, Muted);
            name.style.width = 150;
            name.style.marginBottom = 0;
            var val = Label(row, value, 13, valueColor);
            val.style.marginBottom = 0;
            val.style.flexGrow = 1;
        }

        public static Button Button(VisualElement parent, string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.backgroundColor = ButtonBg;
            button.style.color = Text;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            SetPadding(button, 6);
            button.style.marginRight = 6;
            button.style.marginBottom = 4;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius =
                button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 5;
            parent.Add(button);
            return button;
        }

        public static Toggle Toggle(VisualElement parent, string text, bool value, Action<bool> onChange)
        {
            var toggle = new Toggle(text) { value = value };
            toggle.style.color = Text;
            toggle.style.marginBottom = 3;
            toggle.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            parent.Add(toggle);
            return toggle;
        }

        public static TextField TextField(VisualElement parent, string value, float width, Action<string> onChange = null)
        {
            var field = new TextField { value = value };
            field.style.width = width;
            field.style.marginRight = 6;
            if (onChange != null) field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            parent.Add(field);
            return field;
        }

        /// <summary>A row of buttons where one is highlighted. Calls back with the chosen index.</summary>
        public static void Segmented(VisualElement parent, string[] labels, int selected, Action<int> onSelect)
        {
            var row = Row(parent);
            row.style.flexWrap = Wrap.Wrap;
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                var button = Button(row, labels[i], () => onSelect(index));
                button.style.backgroundColor = i == selected ? Flame : ButtonBg;
            }
        }

        /// <summary>A horizontal fill bar, 0..1, coloured green/amber/red by level.</summary>
        public static void Bar(VisualElement parent, float fraction)
        {
            var track = new VisualElement();
            track.style.height = 14;
            track.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius =
                track.style.borderBottomLeftRadius = track.style.borderBottomRightRadius = 3;
            track.style.marginBottom = 4;
            var fill = new VisualElement();
            fill.style.height = 14;
            fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
            fill.style.backgroundColor = fraction < 0.2f ? new Color(0.3f, 0.75f, 0.35f)
                : fraction < 0.5f ? new Color(0.95f, 0.75f, 0.2f)
                : new Color(0.85f, 0.2f, 0.2f);
            fill.style.borderTopLeftRadius = fill.style.borderBottomLeftRadius = 3;
            track.Add(fill);
            parent.Add(track);
        }

        public static void Space(VisualElement parent, int height = 8)
        {
            var spacer = new VisualElement();
            spacer.style.height = height;
            parent.Add(spacer);
        }

        private static void SetPadding(VisualElement element, int padding)
        {
            element.style.paddingLeft = element.style.paddingRight =
                element.style.paddingTop = element.style.paddingBottom = padding;
        }
    }
}
