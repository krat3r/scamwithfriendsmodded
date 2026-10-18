using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ScamWithFriendsModded;

internal static class Program
{
    private static int passed, failed;

    private static int Main()
    {
        var temp = Path.Combine(Path.GetTempPath(), "swyf-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        // BepInEx normally fills these in at startup.
        AccessTools.PropertySetter(typeof(Paths), nameof(Paths.ConfigPath)).Invoke(null, new object[] { temp });
        AccessTools.PropertySetter(typeof(Paths), nameof(Paths.BepInExRootPath)).Invoke(null, new object[] { temp });

        var log = new ManualLogSource("tests");
        BepInEx.Logging.Logger.Sources.Add(log);
        Mod.Log = log;
        var config = new ConfigFile(Path.Combine(temp, "test.cfg"), true);
        Mod.LogGameEvents = config.Bind("Debug", "LogGameEvents", true, "");

        GameProbe.Scan();
        Check(GameProbe.Missing.Count == 0, "all probed types resolve: missing " + string.Join(",", GameProbe.Missing));

        ScamRegistry.Init(config, AppContext.BaseDirectory);
        Check(ScamRegistry.Packs.Count == 4, "four bundled scam packs load");
        File.WriteAllText(Path.Combine(ScamRegistry.UserFolder, "broken.json"), "{ \"id\": \"x\" }");
        ScamRegistry.Reload();
        Check(ScamRegistry.Packs.Count == 4, "invalid user pack is rejected");

        var harmony = new Harmony("tests");
        ScamInjector.Install(harmony);
        ScamazonHooks.Install(harmony);

        TestSession();
        TestDisabledPack();
        TestCallerPrompt();
        TestScamazon();
        TestDesktop();
        TestDump();

        harmony.UnpatchSelf();
        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    private static ConversationScamDefinition Stock() => new ConversationScamDefinition
    {
        id = "credit-card", appId = "credit-card", productId = "scam-credit-card", reward = 400,
        objectives = new[] { new ConversationScamObjective { id = "number", final = true, requiresSubmission = true } },
    };

    private static void TestSession()
    {
        var session = new ConversationScamSession(new[] { Stock() }, null);
        var ids = session.Catalog.Select(d => d.id).ToArray();
        Check(ids.Length == 5 && ids[0] == "credit-card", "session catalog keeps stock scam and adds 4 modded: " + string.Join(",", ids));
        var zoin = session.Catalog.First(d => d.id == "zoinbase-vault");
        Check(zoin.productId == "scam-zoinbase-vault" && zoin.appId == "zoinbase-vault" && zoin.reward == 900, "definition ids and reward set");
        var transfer = zoin.objectives.First(o => o.id == "transfer");
        Check(transfer.final && !transfer.requiresSubmission && transfer.prerequisites.Length == 3 && transfer.criteria.Length > 20,
            "final objective converted with prerequisites and criteria");
        Check(zoin.objectives.First(o => o.id == "phrase").optional, "optional objective flagged");

        var again = new ConversationScamSession(session.Catalog, null);
        Check(again.Catalog.Count == 5, "already-present scams are not duplicated");
    }

    private static void TestDisabledPack()
    {
        var pack = ScamRegistry.FindById("fastlane-toll");
        ScamRegistry.SetEnabled(pack, false);
        var session = new ConversationScamSession(new[] { Stock() }, null);
        Check(session.Catalog.Count == 4 && session.Catalog.All(d => d.id != "fastlane-toll"), "disabled pack is left out of calls");
        ScamRegistry.SetEnabled(pack, true);
    }

    private static void TestCallerPrompt()
    {
        var prompt = CallerIdentityLibrary.BuildCallerSystemPrompt(new[] { Stock() }, "Pat");
        Check(prompt.StartsWith("You are Pat.") && prompt.Contains("Glimmail") && prompt.Contains("Zoinbase"), "caller prompt gets personas");
    }

    private static void TestScamazon()
    {
        var manifest = new KolkataOsApplicationStoreManifest
        {
            products = new[] { new KolkataOsApplicationProductDefinition { id = "scam-credit-card", appId = "credit-card", price = 500, category = "scam" } },
        };
        var access = new KolkataOsApplicationAccess();
        access.Configure(manifest);
        Check(access.Seen.products.Length == 5, "Scamazon manifest gets 4 products");
        var zoin = access.Seen.products.First(p => p.id == "scam-zoinbase-vault");
        Check(zoin.appId == "zoinbase-vault" && zoin.price == 1500 && zoin.iconText == "ZB" && zoin.category == "scam",
            "product cloned from stock template with pack values");
        new KolkataOsApplicationAccess().Configure(manifest);
        Check(manifest.products.Length == 5, "products not duplicated on second configure");
    }

    private static void TestDesktop()
    {
        var controller = new KolkataOsController();
        var template = new KolkataOsAppDefinition { id = "credit-card", access = new KolkataOsAppAccessDefinition { productId = "scam-credit-card" } };
        controller.Apps.Add(template);
        controller.BuildDesktop();
        Check(controller.Apps.Count == 5, "desktop gets 4 scam apps");
        var glim = controller.Apps.First(a => a.id == "glimmail-login");
        Check(glim.access.productId == "scam-glimmail-login" && glim.contentType == "scam-form" && glim.title == "Glimmail Account Recovery",
            "app cloned from stock app and unlocked by its own product");
        Check(template.access.productId == "scam-credit-card", "stock app's access block is not modified");
        controller.BuildDesktop();
        Check(controller.Apps.Count == 5, "apps not duplicated on rebuild");
    }

    private static void TestDump()
    {
        var path = CatalogDump.Write();
        var text = File.ReadAllText(path);
        Check(text.Contains("\"credit-card\"") && text.Contains("scamazonProducts") && text.Contains("computerApps"), "catalog dump written");
    }

    private static void Check(bool condition, string name)
    {
        if (condition) passed++; else failed++;
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
    }
}
