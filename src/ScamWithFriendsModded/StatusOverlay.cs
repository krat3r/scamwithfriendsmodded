using BepInEx.Configuration;
using UnityEngine;

namespace ScamWithFriendsModded
{
    /// <summary>
    /// Minimal IMGUI overlay. IMGUI reads its own key events, so this works even
    /// though the game uses the new Input System.
    /// </summary>
    internal sealed class StatusOverlay
    {
        private readonly ConfigEntry<KeyCode> toggleKey;
        private bool visible;
        private Rect window = new Rect(20, 20, 340, 200);

        public StatusOverlay(ConfigEntry<KeyCode> toggleKey) => this.toggleKey = toggleKey;

        public void OnGUI()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == toggleKey.Value)
            {
                visible = !visible;
                e.Use();
            }
            if (visible) window = GUILayout.Window(0x5357_4F46, window, Draw, $"{Plugin.Name} {Plugin.Version}");
        }

        private void Draw(int id)
        {
            GUILayout.Label($"Known game types: {GameProbe.Found.Count} found, {GameProbe.Missing.Count} missing");
            foreach (var name in GameProbe.Missing) GUILayout.Label($"  missing: {name}");
            GUILayout.Label($"Scam sessions started: {GameHooks.ScamSessionsStarted}");
            GUILayout.Label($"Fields submitted: {GameHooks.FieldsSubmitted}");
            Plugin.LogGameEvents.Value = GUILayout.Toggle(Plugin.LogGameEvents.Value, "Log game events");
            GUILayout.Label($"Press {toggleKey.Value} to hide.");
            GUI.DragWindow();
        }
    }
}
