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

        CallContext.Bind(config);
        CallContext.ForcedTier.Value = "regular"; // fixed x1 rewards keep the checks below exact

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
        TestTierScaling();
        TestRaid();

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
        CallContext.Reset();
        var prompt = CallerIdentityLibrary.BuildCallerSystemPrompt(new[] { Stock() }, "Pat");
        Check(prompt.StartsWith("You are Pat.") && prompt.Contains(TargetTier.Regular.PersonaHint), "caller prompt gets the tier persona");
        Check(CallContext.Accounts.Count >= 1, "every caller uses at least one modded service");
        bool onlyOwn = true;
        foreach (var pack in ScamRegistry.Packs)
            if (prompt.Contains(pack.callerPersona.Trim()) != CallContext.Uses(pack)) onlyOwn = false;
        Check(onlyOwn, "caller prompt only includes backstories for services this caller uses");
        var tier = CallContext.Current;
        Check(CallContext.NetWorthEstimate >= tier.NetWorthMin && CallContext.NetWorthEstimate <= tier.NetWorthMax, "net worth estimate is within the tier range");
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

    private static void TestTierScaling()
    {
        CallContext.ForcedTier.Value = "whale";
        CallContext.Reset();
        var stock = Stock();
        var session = new ConversationScamSession(new[] { stock }, null);
        Check(session.Catalog[0].reward == 2000 && stock.reward == 400, "whale target scales a per-call copy of the stock reward (400 -> 2000)");
        Check(session.Catalog.First(d => d.id == "glimmail-login").reward == 1750, "whale target scales modded rewards too");
        CallContext.ForcedTier.Value = "regular";
        CallContext.Reset();
    }

    private static void TestRaid()
    {
        var folder = Path.Combine(Paths.ConfigPath, "raid-test");
        ScamWithFriendsModded.Economy.CryptoBank.Init(folder, seed: 42);
        var bank = ScamWithFriendsModded.Economy.CryptoBank.Instance;
        bank.Wallet.Credit("BTCO", 1);
        bool raided = false, warned = false;
        int raidDay = 0;
        bank.Raided += _ => raided = true;
        bank.Heat.WarningIssued += () => warned = true;

        bank.Heat.Value = 100;
        bank.OnDay(1);
        Check(warned && !raided && bank.Wallet.Value(bank.Market) > 0, "first day over the line is a warning, not a raid");

        for (int day = 2; day <= 30 && !raided; day++) { bank.Heat.Value = 100; bank.OnDay(day); raidDay = day; }
        Check(raided && bank.Wallet.Value(bank.Market) == 0, "staying at max heat after the warning gets you raided and the wallet seized");

        bank.Heat.Value = 40;
        warned = false;
        bank.OnDay(raidDay + 1);
        Check(!warned && !bank.Heat.Warned, "dropping below the line clears the warning");
        Check(File.Exists(Path.Combine(folder, "crypto-state.json")), "crypto state is saved");
    }

    private static void Check(bool condition, string name)
    {
        if (condition) passed++; else failed++;
        Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
    }
}
