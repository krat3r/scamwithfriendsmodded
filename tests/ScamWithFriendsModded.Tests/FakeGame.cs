using System.Collections.Generic;

// Minimal stand-ins shaped like the game's types (same names and member names).
// Global namespace, like the game's Assembly-CSharp types.

public class ConversationScamObjective
{
    public string id, text, criteria, requiredSentinel;
    public bool final, optional, requiresSubmission;
    public string[] prerequisites;
}

public class ConversationScamDefinition
{
    public string id, appId, productId;
    public int reward;
    public int[] rewardTiers;
    public ConversationScamObjective[] objectives;
}

public interface IConversationScamDetector { }

public class ConversationScamSession
{
    private readonly IReadOnlyList<ConversationScamDefinition> catalog;
    public ConversationScamSession(IReadOnlyList<ConversationScamDefinition> catalog, IConversationScamDetector detector)
    {
        this.catalog = catalog;
    }
    public IReadOnlyList<ConversationScamDefinition> Catalog => catalog;
}

public class ConversationScamAiDetector { }

public static class CallerIdentityLibrary
{
    public static string BuildCallerSystemPrompt(IReadOnlyList<ConversationScamDefinition> catalog, string name) => "You are " + name + ".";
}

public static class ScamazonUnlockSchedule
{
    public static int CurrentDay { get; set; } = 1;
}

public class KolkataOsApplicationProductDefinition
{
    public string id, appId, title, description, iconText, accentColor, category = "scam";
    public int price, unlockDay;
}

public class KolkataOsApplicationStoreManifest
{
    public KolkataOsApplicationProductDefinition[] products;
}

public class KolkataOsApplicationAccess
{
    public KolkataOsApplicationStoreManifest Seen;
    public void Configure(KolkataOsApplicationStoreManifest manifest) => Seen = manifest;
}

public class KolkataOsApplicationStoreCatalog
{
    public void SetManifest(KolkataOsApplicationStoreManifest manifest) { }
}

public class KolkataOsAppAccessDefinition
{
    public string mode = "catalog", productId;
    public int unlockDay;
}

public class KolkataOsAppDefinition
{
    public string id, title, iconText, accentColor, contentType = "scam-form";
    public KolkataOsAppAccessDefinition access;
}

public class KolkataOsController
{
    private List<KolkataOsAppDefinition> m_appDefinitions = new List<KolkataOsAppDefinition>();
    public List<KolkataOsAppDefinition> Apps => m_appDefinitions;
    public void BuildDesktop() { }
}
