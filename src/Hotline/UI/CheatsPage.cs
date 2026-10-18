using System.Collections.Generic;
using System.Reflection;
using Hotline.Cheats;
using Hotline.UI.Toolkit;
using UnityEngine.UIElements;

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

        public static void Build(VisualElement page)
        {
            W.Note(page, "Cheats only change the host's game in multiplayer. Use them in solo or private lobbies.");

            BuildPinned(page, "Money", GameCheats.Money, moneyInput, v => moneyInput = v);
            if (GameCheats.Money != null)
            {
                var row = W.Row(page);
                foreach (var amount in new[] { 1000, 10000, 100000 })
                {
                    int a = amount;
                    W.Button(row, $"+{a:N0}", () => { GameCheats.AddMoney(a); ModMenu.Refresh(); });
                }
                W.Toggle(page, $" Infinite money (keep at least {GameCheats.InfiniteMoneyAmount.Value:N0})",
                    GameCheats.InfiniteMoney.Value, v => GameCheats.InfiniteMoney.Value = v);
            }

            BuildPinned(page, "Level / XP", GameCheats.Level, levelInput, v => levelInput = v);
            if (GameCheats.Level != null)
            {
                W.Toggle(page, $" Lock at {GameCheats.LockedLevel.Value}", GameCheats.LockLevel.Value, v => GameCheats.LockLevel.Value = v);
                if (int.TryParse(levelInput, out var lockTo))
                    W.Button(page, $"Use {lockTo} as the locked value", () => { GameCheats.LockedLevel.Value = lockTo; ModMenu.Refresh(); });
            }

            W.Space(page);
            BuildFinder(page);
            W.Space(page);
            BuildCalls(page);
        }

        private static void BuildPinned(VisualElement page, string title, ValueRef value, string input, System.Action<string> setInput)
        {
            var box = W.Box(page);
            if (value == null)
            {
                W.Note(box, $"{title}: not pinned yet. Find it below and press \"Pin as {title.Split(' ')[0].ToLowerInvariant()}\".");
                return;
            }
            var current = value.Read();
            W.Label(box, $"{title}: {value.Label} = {(current.HasValue ? current.Value.ToString("N0") : "not loaded")}");
            var row = W.Row(box);
            W.TextField(row, input, 140, setInput);
            W.Button(row, "Set", () =>
            {
                if (double.TryParse(input, out var amount)) value.Write(amount);
                ModMenu.Refresh();
            });
        }

        private static void BuildFinder(VisualElement page)
        {
            W.Label(page, "Value finder: lists number fields in the game that look like money, level or XP. Load into a save first.");
            var row = W.Row(page);
            W.TextField(row, filter, 180, v => filter = v);
            W.Button(row, "Scan", () => { found = ValueFinder.Scan(filter.Trim()); ModMenu.Refresh(); });
            W.Note(page, "Leave the box empty for the default search, or type part of a name (e.g. \"cash\").");
            if (found == null) return;
            if (found.Count == 0) { W.Note(page, "Nothing found. Try another word, or load into a save first."); return; }

            foreach (var value in found)
            {
                var current = value.Read();
                var itemRow = W.Row(page);
                var label = W.Label(itemRow, $"{value.Label} = {(current.HasValue ? current.Value.ToString("N0") : "?")}", 12);
                label.style.flexGrow = 1;
                label.style.marginBottom = 0;
                W.Button(itemRow, "Pin as money", () => { GameCheats.PinMoney(value); ModMenu.Refresh(); });
                W.Button(itemRow, "Pin as level", () => { GameCheats.PinLevel(value); ModMenu.Refresh(); });
            }
            W.Note(page, "Tip: change your money in game, rescan, and pin the field that changed with it.");
        }

        private static void BuildCalls(VisualElement page)
        {
            var box = W.Box(page);
            W.Toggle(box, " Automatic incoming calls", CallBlocker.AutomaticCalls.Value, v => CallBlocker.AutomaticCalls.Value = v);
            W.Note(box, "To turn calls off, pick the game method that rings the phone. Blocked methods are skipped only while the toggle above is off.");
            W.Button(box, "Find call methods", () => { callMethods = CallBlocker.Candidates(); ModMenu.Refresh(); });
            if (callMethods != null)
            {
                if (callMethods.Count == 0) W.Note(box, "No likely methods found.");
                foreach (var method in callMethods)
                {
                    var m = method;
                    bool blocked = CallBlocker.IsBlocked(m);
                    W.Toggle(box, $" {m.DeclaringType?.Name}.{m.Name}", blocked, want =>
                    {
                        if (want) CallBlocker.Block(m); else CallBlocker.Unblock(m);
                    });
                }
                W.Note(box, "Blocking the wrong method can break things. Untick it if the game misbehaves.");
            }
            else if (CallBlocker.BlockedKeys.Count > 0)
                W.Note(box, $"Blocking: {string.Join(", ", CallBlocker.BlockedKeys)}");
        }
    }
}
