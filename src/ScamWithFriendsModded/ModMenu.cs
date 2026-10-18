using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace ScamWithFriendsModded
{
    /// <summary>
    /// In-game mod menu (IMGUI). Other features add tabs with <see cref="AddPage"/>.
    /// IMGUI reads its own key events, so this works with the game's new Input System.
    /// </summary>
    internal sealed class ModMenu
    {
        private sealed class Page
        {
            public string Title;
            public Action Draw;
        }

        private static readonly List<Page> pages = new List<Page>();

        public static bool IsOpen { get; private set; }

        public static void AddPage(string title, Action draw) => pages.Add(new Page { Title = title, Draw = draw });

        private readonly ConfigEntry<KeyCode> toggleKey;
        private Rect window = new Rect(40, 40, 640, 600);
        private int selected;
        private Vector2 scroll;
        private CursorLockMode previousLock;
        private bool previousVisible;

        public ModMenu(ConfigEntry<KeyCode> toggleKey) => this.toggleKey = toggleKey;

        public void OnGUI()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == toggleKey.Value)
            {
                SetOpen(!IsOpen);
                e.Use();
            }
            if (!IsOpen) return;

            // Keep the cursor free while the menu is open; the game may re-lock it each frame.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            window = GUILayout.Window(0x5357_4F46, window, Draw, $"{Plugin.Name} {Plugin.Version}");
        }

        private void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            if (open)
            {
                previousLock = Cursor.lockState;
                previousVisible = Cursor.visible;
            }
            else
            {
                Cursor.lockState = previousLock;
                Cursor.visible = previousVisible;
            }
        }

        private void Draw(int id)
        {
            if (pages.Count > 0)
            {
                var titles = pages.ConvertAll(p => p.Title).ToArray();
                selected = GUILayout.Toolbar(Mathf.Clamp(selected, 0, pages.Count - 1), titles);
                scroll = GUILayout.BeginScrollView(scroll);
                try { pages[selected].Draw(); }
                catch (Exception ex) { GUILayout.Label($"Page error: {ex.Message}"); }
                GUILayout.EndScrollView();
            }
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{toggleKey.Value} toggles this menu");
            if (GUILayout.Button("Close", GUILayout.Width(80))) SetOpen(false);
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }
    }
}
