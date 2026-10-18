using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Hotline.UI
{
    /// <summary>
    /// Polls a key through the game's input system as a backup to IMGUI key events,
    /// which some games never deliver. Tries the new Input System
    /// (Keyboard.current.&lt;key&gt;Key.wasPressedThisFrame), then the legacy Input class.
    /// All by reflection, so it costs nothing when neither is present.
    /// </summary>
    internal sealed class KeyFallback
    {
        private readonly Func<KeyCode> key;
        private KeyCode resolvedFor = KeyCode.None;
        private PropertyInfo keyboardCurrent, keyControl, wasPressed;
        private MethodInfo legacyGetKeyDown;
        private bool legacyBroken;

        public KeyFallback(Func<KeyCode> key)
        {
            this.key = key;
            var keyboard = AccessTools.TypeByName("UnityEngine.InputSystem.Keyboard");
            keyboardCurrent = keyboard?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            var legacy = AccessTools.TypeByName("UnityEngine.Input");
            legacyGetKeyDown = legacy?.GetMethod("GetKeyDown", new[] { typeof(KeyCode) });
        }

        public string Source =>
            keyboardCurrent != null ? "Input System" : legacyGetKeyDown != null ? "legacy Input" : "none";

        public bool PressedThisFrame()
        {
            var code = key();
            if (keyboardCurrent != null && InputSystemPressed(code)) return true;
            if (legacyGetKeyDown != null && !legacyBroken)
            {
                // Throws InvalidOperationException when the game disables the legacy input manager.
                try { return (bool)legacyGetKeyDown.Invoke(null, new object[] { code }); }
                catch { legacyBroken = true; }
            }
            return false;
        }

        private bool InputSystemPressed(KeyCode code)
        {
            try
            {
                var keyboard = keyboardCurrent.GetValue(null, null);
                if (keyboard == null) return false;
                if (resolvedFor != code)
                {
                    resolvedFor = code;
                    keyControl = keyboard.GetType().GetProperty(InputSystemName(code), BindingFlags.Public | BindingFlags.Instance);
                    wasPressed = keyControl?.PropertyType.GetProperty("wasPressedThisFrame");
                }
                if (keyControl == null || wasPressed == null) return false;
                var control = keyControl.GetValue(keyboard, null);
                return control != null && (bool)wasPressed.GetValue(control, null);
            }
            catch { return false; }
        }

        // KeyCode.F8 -> "f8Key", KeyCode.Insert -> "insertKey", KeyCode.Alpha1 -> "digit1Key".
        private static string InputSystemName(KeyCode code)
        {
            var name = code.ToString();
            if (name.StartsWith("Alpha")) name = "Digit" + name.Substring(5);
            return char.ToLowerInvariant(name[0]) + name.Substring(1) + "Key";
        }
    }
}
