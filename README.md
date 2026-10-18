# Scam With Friends Modded

Mods for [Scam With Your Friends](https://store.steampowered.com/app/4954910/Scam_With_Your_Friends/) (Steam, Early Access).

The game is a Unity (Mono) game, so mods are [BepInEx 5](https://github.com/BepInEx/BepInEx) plugins that use [Harmony](https://github.com/BepInEx/HarmonyX) to patch the game's C# code (`Assembly-CSharp.dll`).

## What's in here

| Path | What it is |
| --- | --- |
| `src/ScamWithFriendsModded/` | The mod: new scam types loaded from JSON, Scamazon/computer app hooks, and the F8 mod menu |
| `src/ScamWithFriendsModded/ScamPacks/` | The bundled scam types (one JSON file each) |
| `tests/ScamWithFriendsModded.Tests/` | Runs the hooks against stand-in game classes, no game process needed |
| `Directory.Build.props` / `.targets` | Shared build setup: finds the game, references its DLLs, deploys the built mod into `BepInEx/plugins` |
| `tools/decompile.ps1`, `tools/decompile.sh` | Decompiles the game's code into `decompiled/` (gitignored) so you can find things to hook |

## Setup

1. **Install BepInEx 5** (x64): download `BepInEx_win_x64_5.4.23.x.zip` from the [releases page](https://github.com/BepInEx/BepInEx/releases), extract it into the game folder (next to `Scam With Your Friends.exe`), and start the game once so BepInEx creates its folders.
2. **Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**.
3. **Point the build at your game.** If the game isn't in the default Steam folder, copy `Directory.Build.user.props.example` to `Directory.Build.user.props` and set `GameDir`. You can set the `SWYF_GAME_DIR` environment variable instead.
4. **Build:**
   ```
   dotnet build -c Release
   ```
   The build copies `ScamWithFriendsModded.dll` into `<game>/BepInEx/plugins/ScamWithFriendsModded/`. Add `-p:DeployToGame=false` to skip that.
5. **Launch the game** and press **F8** to open the mod menu. `BepInEx/LogOutput.log` should contain `Scam With Friends Modded 0.2.0 loaded` and `Loaded 4 scam pack(s)`.

Config is written to `BepInEx/config/com.krat3r.scamwithfriendsmodded.cfg` after the first launch.

## New scam types

| Scam | Brand | How it plays |
| --- | --- | --- |
| Glimmail Account Recovery | Glimmail (email) | Pose as security, warn about a suspicious sign-in, get the caller to read back their 6-digit login code |
| Zoinbase Vault Transfer | Zoinbase (crypto exchange) | Report an "unauthorized withdrawal", talk them into moving their balance to your "secure vault wallet". Bonus: their recovery phrase |
| FastLane Unpaid Toll | FastLane Tolls | Follow up on a "final notice" toll text and get them to pay the toll plus a late fee through your link |
| TaskTrove Remote Job | TaskTrove | Recruit them for a product-rating side job, then get them to "top up" their task account to unlock commissions |

All brands are made up. Each scam works like the stock credit-card and gift-card scams:

1. Buy its app in **Scamazon**. The app then appears on the in-game computer.
2. On a call, complete its objectives in order. The game's AI judges each one from the conversation.
3. The final objective pays the scam's reward.

AI callers also get a short backstory for each enabled scam (e.g. they hold crypto on Zoinbase), so they can actually fall for it.

Run the mod on the **host**. Calls are simulated there.

### Mod menu (F8)

- **Scams:** turn each scam on or off, see its objectives, and reload packs without restarting.
- **Diagnostics:** see which game classes and hooks were found, and dump the game's live scam catalog, Scamazon products and computer apps to `BepInEx/ScamWithFriendsModded-catalog.json`.
- **Settings:** logging. The menu key is set in the config file.

### Making your own scam

Drop a `.json` file in `BepInEx/config/ScamWithFriendsModded/ScamPacks/` and press **Reload scam packs**. Copy one of the [bundled packs](src/ScamWithFriendsModded/ScamPacks) as a starting point:

```jsonc
{
  "id": "my-scam",                 // unique; the app id is "my-scam", the Scamazon product is "scam-my-scam"
  "brand": "Brand",
  "title": "Shown in Scamazon and the menu",
  "description": "Shop blurb",
  "reward": 400,                   // paid when the final objective completes
  "shop": { "price": 1000, "unlockDay": 1, "iconText": "MS", "accentColor": "#4A90E2" },
  "callerPersona": "Added to every caller's AI prompt so they have a reason to fall for it.",
  "objectives": [
    { "id": "pose", "text": "Shown to the player", "criteria": "What the AI judge must see in the dialogue" },
    { "id": "win", "text": "...", "criteria": "...", "final": true, "prerequisites": ["pose"] }
  ]
}
```

Objectives can also be `"optional": true`. A pack in the config folder with the same `id` as a bundled one replaces it.

## Finding things to mod

```
tools\decompile.ps1          # Windows
tools/decompile.sh           # Linux / Steam Deck
```

This writes the decompiled `Assembly-CSharp` (and `ScriptsAssDef`, if the game ships it) to `decompiled/`. Open it in your editor and search. Some useful starting points from the current build:

- `ConversationScamSession`: one AI phone call. Tracks objectives and rewards. `SubmitField` runs when the player submits card details or codes.
- `ConversationScamAiDetector`: asks the AI whether the player completed an objective.
- `ConversationScamDefinition`: data for each scam type (`credit-card`, `gift-card`, ...), including objectives and reward tiers.
- `CallerIdentityLibrary.BuildCallerSystemPrompt`: builds the AI caller's personality prompt.

Networking uses Mirror. Gameplay logic usually runs on the host, so most gameplay mods only need to be installed by the host.

**Don't commit decompiled code or game DLLs.** They belong to the developers. `.gitignore` already excludes them.

## Writing a hook

`ScamInjector.cs` and `ScamazonHooks.cs` show the pattern: look the type up by name with `GameProbe`, then patch it with Harmony. If a game update renames something, the mod logs a warning and keeps running instead of crashing. Once you've found a method in `decompiled/`, a typed patch looks like this:

```csharp
[HarmonyPatch(typeof(ConversationScamSession), "SubmitField")]
static class MyPatch
{
    static void Postfix(ConversationScamSession __instance) { /* ... */ }
}
```

Typed patches like this need `Assembly-CSharp.dll`, which the build references automatically when it finds the game. Register them with `harmony.PatchAll()`.

## Tests

```
dotnet run -c Release --project tests/ScamWithFriendsModded.Tests
```

These check the scam injection, caller prompts, Scamazon listing and desktop apps against stand-in classes shaped like the game's. They can't prove the real game behaves the same, so test in game too.

## Notes

- New scams' final step is judged by the AI from dialogue. The stock scams instead make you type a revealed card number or code into their app. If the game turns out not to pay AI-judged final objectives, use the catalog dump to see how the stock submissions work and we'll wire that up.
- The game is in Early Access and updates often, so hooks can break. Check `LogOutput.log` for `Game probe` warnings after an update.
- The game's anti-cheat status for online play is unknown. Test mods in private lobbies with friends who agreed to use them.
