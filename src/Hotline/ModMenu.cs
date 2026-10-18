using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Hotline.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hotline
{
    /// <summary>
    /// In-game mod menu, built on UI Toolkit (the game's own UI system). IMGUI gets no usable
    /// input in this game, so the menu is a UI Toolkit overlay instead. Other features add tabs
    /// with <see cref="AddPage"/>; input (the toggle key and badge clicks) is detected through
    /// the Input System in <see cref="Plugin"/> and routed to <see cref="Toggle"/>.
    /// </summary>
    internal sealed class ModMenu
    {
        private sealed class Page
        {
            public string Title;
            public Action<VisualElement> Build;
        }

        private static readonly List<Page> pages = new List<Page>();
        private static ModMenu instance;

        public static bool IsOpen => instance != null && instance.open;

        public static void AddPage(string title, Action<VisualElement> build) => pages.Add(new Page { Title = title, Build = build });

        /// <summary>Rebuilds the current page if the menu is open (e.g. after reloading scam packs).</summary>
        public static void Refresh()
        {
            if (instance != null && instance.open) instance.BuildContent();
        }

        private readonly ConfigEntry<KeyCode> toggleKey;
        private OverlayPanel panel;
        private VisualElement window;
        private VisualElement content;
        private readonly List<Button> tabButtons = new List<Button>();
        private int selected;
        private bool open;
        private bool failed;
        private int lastToggleMs;
        private bool haveToggled;
        private const int DuplicateWindowMs = 120;

        public ModMenu(ConfigEntry<KeyCode> toggleKey)
        {
            this.toggleKey = toggleKey;
            instance = this;
        }

        public KeyCode Key => toggleKey.Value;

        /// <summary>Opens or closes the menu. A second call within a short window (two input paths, one press) is ignored.</summary>
        public void Toggle(string source)
        {
            int now = Environment.TickCount;
            if (haveToggled && unchecked(now - lastToggleMs) < DuplicateWindowMs) return;
            haveToggled = true;
            lastToggleMs = now;
            SetOpen(!open, source);
        }

        private void SetOpen(bool value, string source)
        {
            if (failed) return;
            if (value && window == null && !Build()) return;
            if (value == open) return;
            open = value;
            if (window != null) window.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
            {
                UnityEngine.Cursor.lockState = CursorLockMode.None;
                UnityEngine.Cursor.visible = true;
                BuildContent();
            }
            Mod.Log.LogInfo($"Menu {(open ? "opened" : "closed")} by {source}.");
        }

        /// <summary>Builds the panel and window on first open. Returns false if UI Toolkit is unavailable.</summary>
        private bool Build()
        {
            try
            {
                panel = OverlayPanel.Create("Menu", 30000f);
                if (panel == null) { failed = true; return false; }

                window = new VisualElement();
                window.style.position = Position.Absolute;
                window.style.left = 40;
                window.style.top = 40;
                window.style.width = 660;
                window.style.maxHeight = Screen.height - 80;
                window.style.backgroundColor = W.PanelBg;
                window.style.borderTopLeftRadius = window.style.borderTopRightRadius =
                    window.style.borderBottomLeftRadius = window.style.borderBottomRightRadius = 10;
                SetBorder(window, W.Flame, 1);
                window.style.paddingLeft = window.style.paddingRight =
                    window.style.paddingTop = window.style.paddingBottom = 12;
                window.pickingMode = PickingMode.Position; // capture clicks so they don't fall through to the game

                var titleBar = W.Row(window);
                W.Label(titleBar, "HOTLINE", 18, W.Flame, bold: true).style.marginBottom = 0;
                var version = W.Label(titleBar, "  v" + Plugin.Version, 12, W.Muted);
                version.style.marginBottom = 0;
                version.style.flexGrow = 1;
                W.Button(titleBar, "Close", () => SetOpen(false, "close button"));

                var tabs = W.Row(window);
                tabs.style.flexWrap = Wrap.Wrap;
                tabs.style.marginTop = 6;
                tabs.style.marginBottom = 6;
                tabButtons.Clear();
                for (int i = 0; i < pages.Count; i++)
                {
                    int index = i;
                    var tab = W.Button(tabs, pages[i].Title, () => Select(index));
                    tabButtons.Add(tab);
                }

                var scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.style.flexGrow = 1;
                window.Add(scroll);
                content = new VisualElement();
                scroll.Add(content);

                window.style.display = DisplayStyle.None;
                panel.Root.Add(window);
                HighlightTabs();
                Mod.Log.LogInfo("UI Toolkit menu built.");
                return true;
            }
            catch (Exception e)
            {
                failed = true;
                Mod.Log.LogError("Could not build the UI Toolkit menu: " + e);
                return false;
            }
        }

        private void Select(int index)
        {
            selected = index;
            HighlightTabs();
            BuildContent();
        }

        private void HighlightTabs()
        {
            for (int i = 0; i < tabButtons.Count; i++)
                tabButtons[i].style.backgroundColor = i == selected ? W.Flame : W.ButtonBg;
        }

        private void BuildContent()
        {
            if (content == null || pages.Count == 0) return;
            content.Clear();
            selected = Mathf.Clamp(selected, 0, pages.Count - 1);
            try { pages[selected].Build(content); }
            catch (Exception e)
            {
                W.Label(content, "This page hit an error: " + e.Message, 12, new Color(0.9f, 0.4f, 0.4f));
                Mod.Log.LogError($"Menu page '{pages[selected].Title}' failed: {e}");
            }
        }

        private static void SetBorder(VisualElement element, Color color, float width)
        {
            element.style.borderTopColor = element.style.borderBottomColor =
                element.style.borderLeftColor = element.style.borderRightColor = color;
            element.style.borderTopWidth = element.style.borderBottomWidth =
                element.style.borderLeftWidth = element.style.borderRightWidth = width;
        }
    }
}
