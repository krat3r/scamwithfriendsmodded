using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;

namespace Hotline
{
    /// <summary>
    /// Lists each scam pack as a purchasable app in Scamazon and puts its app on the
    /// in-game computer. The game only offers a scam during a call once the player
    /// owns its product, the same way the stock credit-card and gift-card scams work.
    /// New entries are cloned from the stock credit-card scam so any fields we don't
    /// know about keep sensible values.
    /// </summary>
    internal static class ScamazonHooks
    {
        private const string TemplateAppId = "credit-card";
        private const string TemplateProductId = "scam-credit-card";

        public static readonly List<string> InstalledHooks = new List<string>();
        /// <summary>Last seen store products and desktop apps, for diagnostics.</summary>
        public static Array LastProducts;
        public static IList LastApps;

        public static void Install(Harmony harmony)
        {
            var manifestType = GameProbe.Get("KolkataOsApplicationStoreManifest");
            if (manifestType != null)
            {
                Patch(harmony, AccessTools.Method(GameProbe.Get("KolkataOsApplicationAccess"), "Configure", new[] { manifestType }), nameof(ManifestPrefix));
                Patch(harmony, AccessTools.Method(GameProbe.Get("KolkataOsApplicationStoreCatalog"), "SetManifest", new[] { manifestType }), nameof(ManifestPrefix));
            }
            Patch(harmony, AccessTools.Method(GameProbe.Get("KolkataOsController"), "BuildDesktop"), nameof(DesktopPrefix));
        }

        private static void Patch(Harmony harmony, System.Reflection.MethodBase target, string prefix)
        {
            if (target == null)
            {
                Mod.Log.LogWarning($"Scamazon hook '{prefix}' skipped: target method not found.");
                return;
            }
            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(ScamazonHooks), prefix));
                InstalledHooks.Add($"{target.DeclaringType?.Name}.{target.Name}");
            }
            catch (Exception e)
            {
                Mod.Log.LogError($"Failed to hook {target.DeclaringType?.Name}.{target.Name}: {e}");
            }
        }

        private static void ManifestPrefix([HarmonyArgument(0)] object manifest)
        {
            try
            {
                if (!(Native.Get(manifest, "products") is Array products)) return;
                LastProducts = products;

                var items = new ArrayList();
                object template = null;
                var ids = new HashSet<string>();
                foreach (var product in products)
                {
                    items.Add(product);
                    var id = Native.GetString(product, "id");
                    if (id != null) ids.Add(id);
                    if (id == TemplateProductId) template = product;
                }

                int added = 0;
                foreach (var pack in ScamRegistry.Active())
                {
                    if (ids.Contains(pack.ProductId)) continue;
                    var product = template != null ? Native.Clone(template) : Native.New(products.GetType().GetElementType());
                    Native.Set(product, "id", pack.ProductId);
                    Native.Set(product, "appId", pack.AppId);
                    Native.Set(product, "title", pack.title);
                    Native.Set(product, "description", pack.description);
                    Native.Set(product, "price", pack.shop.price);
                    Native.Set(product, "unlockDay", pack.shop.unlockDay);
                    Native.Set(product, "iconText", IconText(pack));
                    Native.Set(product, "accentColor", pack.shop.accentColor);
                    items.Add(product);
                    added++;
                }
                if (added > 0) Native.Set(manifest, "products", Native.ToArray(products.GetType().GetElementType(), items));
            }
            catch (Exception e)
            {
                Mod.Log.LogError($"Could not add scam apps to Scamazon: {e}");
            }
        }

        private static void DesktopPrefix(object __instance)
        {
            try
            {
                if (!(Native.Get(__instance, "m_appDefinitions") is IList apps)) return;
                LastApps = apps;

                object template = null;
                var ids = new HashSet<string>();
                foreach (var app in apps)
                {
                    var id = Native.GetString(app, "id");
                    if (id != null) ids.Add(id);
                    if (id == TemplateAppId) template = app;
                }
                if (template == null)
                {
                    Mod.Log.LogWarning($"Scam apps not added: stock '{TemplateAppId}' app not found on the computer.");
                    return;
                }

                foreach (var pack in ScamRegistry.Active())
                {
                    if (ids.Contains(pack.AppId)) continue;
                    var app = Native.Clone(template);
                    Native.Set(app, "id", pack.AppId);
                    Native.Set(app, "title", pack.title);
                    Native.Set(app, "iconText", IconText(pack));
                    Native.Set(app, "accentColor", pack.shop.accentColor);
                    // The access block decides which product unlocks the app; give it its own copy.
                    var access = Native.Clone(Native.Get(template, "access"));
                    if (access != null)
                    {
                        Native.Set(access, "productId", pack.ProductId);
                        Native.Set(access, "unlockDay", pack.shop.unlockDay);
                        Native.Set(app, "access", access);
                    }
                    apps.Add(app);
                }
            }
            catch (Exception e)
            {
                Mod.Log.LogError($"Could not add scam apps to the computer: {e}");
            }
        }

        private static string IconText(ScamPack pack)
        {
            if (!string.IsNullOrEmpty(pack.shop.iconText)) return pack.shop.iconText;
            var name = pack.brand ?? pack.title;
            return name.Length <= 2 ? name : name.Substring(0, 2).ToUpperInvariant();
        }
    }
}
