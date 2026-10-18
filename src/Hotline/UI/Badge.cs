using UnityEngine;

namespace Hotline.UI
{
    /// <summary>
    /// Small "HOTLINE activated" tag in the top-left corner. Doubles as a load check:
    /// if players can't see it, the mod isn't running at all.
    /// </summary>
    internal static class Badge
    {
        private static GUIStyle brand, text;
        private static readonly Color Flame = new Color(1f, 0.42f, 0.13f);

        public static void Draw(string menuKey, int startupErrors)
        {
            EnsureStyles();
            string detail = startupErrors > 0
                ? $"activated · {startupErrors} startup problem{(startupErrors == 1 ? "" : "s")}, see {menuKey} › Diagnostics"
                : $"activated · {menuKey} for menu";

            var brandContent = new GUIContent("HOTLINE");
            var detailContent = new GUIContent($"v{Plugin.Version} {detail}");
            var brandSize = brand.CalcSize(brandContent);
            var detailSize = text.CalcSize(detailContent);
            var rect = new Rect(10, 10, brandSize.x + detailSize.x + 22, Mathf.Max(brandSize.y, detailSize.y) + 8);

            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Flame;
            GUI.DrawTexture(new Rect(rect.x, rect.y, 3, rect.height), Texture2D.whiteTexture);
            GUI.color = old;

            GUI.Label(new Rect(rect.x + 9, rect.y + 4, brandSize.x, brandSize.y), brandContent, brand);
            GUI.Label(new Rect(rect.x + 13 + brandSize.x, rect.y + 4 + (brandSize.y - detailSize.y) / 2, detailSize.x, detailSize.y), detailContent, text);
        }

        private static void EnsureStyles()
        {
            if (brand != null) return;
            brand = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 15, wordWrap = false };
            brand.normal.textColor = Flame;
            text = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false };
            text.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
        }
    }
}
