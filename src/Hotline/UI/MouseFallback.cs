using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Hotline.UI
{
    /// <summary>
    /// Left clicks read through the game's Input System, for when IMGUI never receives mouse
    /// events (as in this game). Reflection only, like <see cref="KeyFallback"/>.
    /// </summary>
    internal sealed class MouseFallback
    {
        private readonly PropertyInfo mouseCurrent;
        private PropertyInfo leftButton, wasPressed, position;
        private MethodInfo readValue;

        public MouseFallback()
        {
            var mouse = AccessTools.TypeByName("UnityEngine.InputSystem.Mouse");
            mouseCurrent = mouse?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
        }

        public bool Available => mouseCurrent != null;

        /// <summary>True on the frame the left button went down; <paramref name="guiPoint"/> is in IMGUI coordinates (top-left origin).</summary>
        public bool LeftPressedThisFrame(out Vector2 guiPoint)
        {
            guiPoint = default;
            if (mouseCurrent == null) return false;
            try
            {
                var mouse = mouseCurrent.GetValue(null, null);
                if (mouse == null) return false;
                if (leftButton == null)
                {
                    leftButton = mouse.GetType().GetProperty("leftButton", BindingFlags.Public | BindingFlags.Instance);
                    position = mouse.GetType().GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
                    wasPressed = leftButton?.PropertyType.GetProperty("wasPressedThisFrame");
                    readValue = position?.PropertyType.GetMethod("ReadValue", Type.EmptyTypes);
                }
                if (leftButton == null || wasPressed == null || position == null || readValue == null) return false;

                var button = leftButton.GetValue(mouse, null);
                if (button == null || !(bool)wasPressed.GetValue(button, null)) return false;
                var screen = (Vector2)readValue.Invoke(position.GetValue(mouse, null), null);
                // Input System uses a bottom-left origin; IMGUI uses top-left.
                guiPoint = new Vector2(screen.x, Screen.height - screen.y);
                return true;
            }
            catch { return false; }
        }
    }
}
