using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Hotline.UI.Toolkit
{
    /// <summary>
    /// Hosts a UI Toolkit overlay on its own UIDocument, on top of the game's UI. The game
    /// renders its interface with UI Toolkit and IMGUI gets no usable input here, so the menu,
    /// badge and overlays are all built on this instead of IMGUI.
    /// </summary>
    /// <remarks>
    /// A runtime panel needs a PanelSettings with a theme. Rather than build one (which needs a
    /// theme asset we don't have), it clones one the game already uses, so the theme is valid.
    /// </remarks>
    internal sealed class OverlayPanel
    {
        public readonly VisualElement Root;
        private readonly UIDocument document;
        private static bool loggedSettingsSource;

        private OverlayPanel(UIDocument document, VisualElement root)
        {
            this.document = document;
            Root = root;
        }

        /// <summary>Creates a panel at the given sort order, or null if no PanelSettings could be found.</summary>
        public static OverlayPanel Create(string name, float sortingOrder)
        {
            var settings = FindTemplateSettings();
            if (settings == null)
            {
                Mod.Log.LogError("UI Toolkit: no PanelSettings found in the game, so the menu can't be shown.");
                return null;
            }

            var clone = Object.Instantiate(settings);
            clone.name = "Hotline " + name + " PanelSettings";
            clone.sortingOrder = sortingOrder; // above the game's UI
            clone.hideFlags = HideFlags.HideAndDontSave;
            // Our own scale, independent of whatever the game set.
            try { clone.scaleMode = PanelScaleMode.ConstantPixelSize; } catch { }

            var host = new GameObject("Hotline " + name);
            host.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(host);

            // UIDocument builds its visual tree in OnEnable. Keep the object inactive until
            // panelSettings is set, or the tree is built with no settings and nothing renders.
            host.SetActive(false);
            var document = host.AddComponent<UIDocument>();
            document.panelSettings = clone;
            host.SetActive(true);

            var root = document.rootVisualElement;
            if (root == null)
            {
                Mod.Log.LogError("UI Toolkit: UIDocument produced no root element; the menu can't be shown.");
                Object.Destroy(host);
                Object.Destroy(clone);
                return null;
            }
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            // The root itself never eats clicks; only the children we add do.
            root.pickingMode = PickingMode.Ignore;

            if (!loggedSettingsSource)
            {
                loggedSettingsSource = true;
                Mod.Log.LogInfo($"UI Toolkit: cloned the game's PanelSettings for the overlay (sort order {sortingOrder}).");
            }
            return new OverlayPanel(document, root);
        }

        private static PanelSettings FindTemplateSettings()
        {
            // Prefer settings actually in use by a UIDocument, then any loaded PanelSettings.
            var fromDocuments = Resources.FindObjectsOfTypeAll<UIDocument>()
                .Select(d => d.panelSettings)
                .FirstOrDefault(s => s != null);
            if (fromDocuments != null) return fromDocuments;
            return Resources.FindObjectsOfTypeAll<PanelSettings>().FirstOrDefault();
        }

        public void Destroy()
        {
            if (document != null)
            {
                if (document.panelSettings != null) Object.Destroy(document.panelSettings);
                Object.Destroy(document.gameObject);
            }
        }
    }
}
