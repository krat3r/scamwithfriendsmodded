using System.Collections.Generic;
using System.Reflection;
using Hotline.Cheats;
using UnityEngine;

namespace Hotline.UI
{
    /// <summary>Money and level cheats, the value finder, and the automatic-calls toggle.</summary>
    internal static class CheatsPage
    {
        private static string filter = "";
        private static List<ValueRef> found;
        private static List<MethodInfo> callMethods;
        private static string moneyInput = "100000";
        private static string levelInput = "50";

        public static void Draw()
        {
            GUILayout.Label("Cheats only change the host's game in multiplayer. Use them in solo or private lobbies.");

            DrawPinned("Money", GameCheats.Money, ref moneyInput);
            if (GameCheats.Money != null)
            {
                GUILayout.BeginHorizontal();
                foreach (var amount in new[] { 1000, 10000, 100000 })
                    if (GUILayout.Button($"+{amount:N0}")) GameCheats.AddMoney(amount);
                GUILayout.EndHorizontal();
                GameCheats.InfiniteMoney.Value = GUILayout.Toggle(GameCheats.InfiniteMoney.Value,
                    $" Infinite money (keep at least {GameCheats.InfiniteMoneyAmount.Value:N0})");
            }

            DrawPinned("Level / XP", GameCheats.Level, ref levelInput);
            if (GameCheats.Level != null)
            {
                GameCheats.LockLevel.Value = GUILayout.Toggle(GameCheats.LockLevel.Value,
                    $" Lock at {GameCheats.LockedLevel.Value}");
                if (int.TryParse(levelInput, out var lockTo) && GUILayout.Button($"Use {lockTo} as the locked value"))
                    GameCheats.LockedLevel.Value = lockTo;
            }

            GUILayout.Space(10);
            DrawFinder();
            GUILayout.Space(10);
            DrawCalls();
        }

        private static void DrawPinned(string title, ValueRef value, ref string input)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            if (value == null)
            {
                GUILayout.Label($"{title}: not pinned yet. Find it below and press \"Pin as {title.Split(' ')[0].ToLowerInvariant()}\".");
                GUILayout.EndVertical();
                return;
            }
            var current = value.Read();
            GUILayout.Label($"{title}: {value.Label} = {(current.HasValue ? current.Value.ToString("N0") : "not loaded")}");
            GUILayout.BeginHorizontal();
            input = GUILayout.TextField(input, GUILayout.Width(140));
            if (GUILayout.Button("Set", GUILayout.Width(60)) && double.TryParse(input, out var amount)) value.Write(amount);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static void DrawFinder()
        {
            GUILayout.Label("Value finder: lists number fields in the game that look like money, level or XP. Load into a save first.");
            GUILayout.BeginHorizontal();
            filter = GUILayout.TextField(filter, GUILayout.Width(180));
            if (GUILayout.Button("Scan", GUILayout.Width(80))) found = ValueFinder.Scan(filter.Trim());
            GUILayout.EndHorizontal();
            GUILayout.Label("Leave the box empty for the default search, or type part of a name (e.g. \"cash\").");
            if (found == null) return;
            if (found.Count == 0) { GUILayout.Label("Nothing found. Try another word, or load into a save first."); return; }

            foreach (var value in found)
            {
                var current = value.Read();
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{value.Label} = {(current.HasValue ? current.Value.ToString("N0") : "?")}");
                if (GUILayout.Button("Pin as money", GUILayout.Width(110))) GameCheats.PinMoney(value);
                if (GUILayout.Button("Pin as level", GUILayout.Width(100))) GameCheats.PinLevel(value);
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("Tip: change your money in game, rescan, and pin the field that changed with it.");
        }

        private static void DrawCalls()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            CallBlocker.AutomaticCalls.Value = GUILayout.Toggle(CallBlocker.AutomaticCalls.Value, " Automatic incoming calls");
            GUILayout.Label("To turn calls off, pick the game method that rings the phone. Blocked methods are skipped only while the toggle above is off.");
            if (GUILayout.Button("Find call methods")) callMethods = CallBlocker.Candidates();
            if (callMethods != null)
            {
                if (callMethods.Count == 0) GUILayout.Label("No likely methods found.");
                foreach (var method in callMethods)
                {
                    bool blocked = CallBlocker.IsBlocked(method);
                    bool want = GUILayout.Toggle(blocked, $" {method.DeclaringType?.Name}.{method.Name}");
                    if (want && !blocked) CallBlocker.Block(method);
                    else if (!want && blocked) CallBlocker.Unblock(method);
                }
                GUILayout.Label("Blocking the wrong method can break things. Untick it if the game misbehaves.");
            }
            else if (CallBlocker.BlockedKeys.Count > 0)
                GUILayout.Label($"Blocking: {string.Join(", ", CallBlocker.BlockedKeys)}");
            GUILayout.EndVertical();
        }
    }
}
