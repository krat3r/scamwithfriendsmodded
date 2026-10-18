using System.Diagnostics;
using Hotline.Economy;
using UnityEngine;

namespace Hotline.UI
{
    /// <summary>
    /// Full-screen "CRIME DOESN'T PAY." card shown after a raid, plus a smaller banner
    /// when heat first crosses the danger line. Drawn from the plugin's OnGUI.
    /// </summary>
    internal static class BustedScreen
    {
        private const float FadeIn = 1.2f, TextIn = 0.8f, Hold = 5f, FadeOut = 1.5f;
        private const float BannerSeconds = 8f;

        // A real clock: this game's Unity time didn't advance where the mod runs, which would
        // freeze the fades. -1 means "not showing".
        private static readonly Stopwatch clock = Stopwatch.StartNew();
        private static float Now => (float)clock.Elapsed.TotalSeconds;

        private static float bustedAt = -1, bannerAt = -1;
        private static string detail = "";
        private static GUIStyle title, subtitle, banner;

        public static void Hook(CryptoBank bank)
        {
            bank.Raided += lost => Show($"Everything in your wallet was seized ({Format.Money(lost)}).");
            bank.Heat.WarningIssued += () => bannerAt = Now;
        }

        public static void Show(string message)
        {
            detail = message;
            bustedAt = Now;
        }

        public static void OnGUI()
        {
            EnsureStyles();
            DrawBanner();
            DrawBusted();
        }

        private static void DrawBusted()
        {
            if (bustedAt < 0) return;
            float t = Now - bustedAt;
            float total = FadeIn + TextIn + Hold + FadeOut;
            if (t > total) { bustedAt = -1; return; }

            float fadeOutStart = FadeIn + TextIn + Hold;
            float overlay = t < FadeIn ? t / FadeIn : t < fadeOutStart ? 1f : 1f - (t - fadeOutStart) / FadeOut;
            float text = t < FadeIn ? 0f : t < FadeIn + TextIn ? (t - FadeIn) / TextIn : overlay;

            var screen = new Rect(0, 0, Screen.width, Screen.height);
            var old = GUI.color;
            GUI.color = new Color(0.04f, 0.04f, 0.04f, 0.88f * overlay);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);

            title.fontSize = Mathf.Max(32, Screen.height / 9);
            subtitle.fontSize = Mathf.Max(16, Screen.height / 40);
            GUI.color = new Color(0.82f, 0.12f, 0.12f, text);
            GUI.Label(new Rect(0, Screen.height * 0.36f, Screen.width, Screen.height * 0.16f), "CRIME DOESN'T PAY.", title);
            GUI.color = new Color(0.85f, 0.85f, 0.85f, text);
            GUI.Label(new Rect(0, Screen.height * 0.54f, Screen.width, Screen.height * 0.08f), detail, subtitle);
            GUI.color = old;
        }

        private static void DrawBanner()
        {
            if (bannerAt < 0) return;
            float t = Now - bannerAt;
            if (t > BannerSeconds) { bannerAt = -1; return; }
            float alpha = Mathf.Clamp01(Mathf.Min(t / 0.4f, (BannerSeconds - t) / 0.8f));
            var rect = new Rect(Screen.width * 0.2f, 24, Screen.width * 0.6f, 56);
            var old = GUI.color;
            GUI.color = new Color(0.45f, 0.06f, 0.06f, 0.9f * alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(rect, "Investigators are looking into you. Stop now, or expect a raid.", banner);
            GUI.color = old;
        }

        private static void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            title.normal.textColor = Color.white;
            subtitle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            subtitle.normal.textColor = Color.white;
            banner = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 18, wordWrap = true };
            banner.normal.textColor = Color.white;
        }
    }
}
