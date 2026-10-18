# Hotline

Mods for [Scam With Your Friends](https://store.steampowered.com/app/4954910/Scam_With_Your_Friends/) (Steam, Early Access).

The game is a Unity (Mono) game, so mods are [BepInEx 5](https://github.com/BepInEx/BepInEx) plugins that use [Harmony](https://github.com/BepInEx/HarmonyX) to patch the game's C# code (`Assembly-CSharp.dll`).

## What's in here

| Path | What it is |
| --- | --- |
| `src/Hotline/` | The mod: new scam types loaded from JSON, Scamazon/computer app hooks, and the F8 mod menu |
| `src/Hotline/ScamPacks/` | The bundled scam types (one JSON file each) |
| `installer/` | `Hotline-Setup.exe`: finds the game, installs BepInEx and the mod, uninstalls |
| `tests/Hotline.Tests/` | Runs the hooks and installer against stand-in game classes and folders, no game process needed |
| `Directory.Build.props` / `.targets` | Shared build setup: finds the game, references its DLLs, deploys the built mod into `BepInEx/plugins` |
| `tools/decompile.ps1`, `tools/decompile.sh` | Decompiles the game's code into `decompiled/` (gitignored) so you can find things to hook |
| `tools/package-release.ps1`, `tools/package-release.sh` | Builds the release installer and manual-install zip into `dist/` |

## Installing (players)

1. Download `Hotline-Setup.exe` from the releases page.
2. Close the game and run it. It finds the game through Steam (or asks for the folder), then offers **Install**, **Repair**, **Uninstall the mod**, and **Uninstall everything**.
3. Start the game from Steam and press **F8**.

BepInEx loads the mod automatically every time the game starts; there's nothing else to run. Uninstalling the mod keeps BepInEx and your settings. "Uninstall everything" also removes BepInEx and warns you if other BepInEx mods would break.

**Steam Deck / Linux (Proton):** set the game's launch option in Steam to `WINEDLLOVERRIDES="winhttp=n,b" %command%`, or BepInEx won't load.

**Windows SmartScreen** may warn that the .exe is unrecognized, because it isn't code-signed. Choose "More info" → "Run anyway", or install by hand: extract BepInEx 5 (x64) into the game folder, then extract `Hotline-<version>.zip` into `BepInEx/plugins/Hotline/`.

The installer also has flags for scripting: `--install [--repair] [--game "path"]` and `--uninstall [--all] [--yes] [--game "path"]`.

## Building (modders)

1. **Install BepInEx 5** (x64): download `BepInEx_win_x64_5.4.23.x.zip` from the [releases page](https://github.com/BepInEx/BepInEx/releases), extract it into the game folder (next to `Scam With Your Friends.exe`), and start the game once so BepInEx creates its folders.
2. **Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**.
3. **Point the build at your game.** If the game isn't in the default Steam folder, copy `Directory.Build.user.props.example` to `Directory.Build.user.props` and set `GameDir`. You can set the `SWYF_GAME_DIR` environment variable instead.
4. **Build:**
   ```
   dotnet build -c Release
   ```
   The build copies `Hotline.dll` into `<game>/BepInEx/plugins/Hotline/`. Add `-p:DeployToGame=false` to skip that.
5. **Launch the game** and press **F8** to open the mod menu. `BepInEx/LogOutput.log` should contain `Hotline 0.3.0 loaded` and `Loaded 4 scam pack(s)`.

Config is written to `BepInEx/config/com.krat3r.hotline.cfg` after the first launch.

To make a release, run `tools\package-release.ps1` (or `tools/package-release.sh`). It builds the mod, downloads BepInEx 5.4.23.4 (checked against a pinned SHA-256), and writes `dist/Hotline-Setup.exe` with everything embedded, plus `dist/Hotline-<version>.zip` for manual installs. Upload both to a GitHub release.

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

Each caller uses a random mix of the made-up services (say, Glimmail and Zoinbase but not TaskTrove) and only gets backstories for those, so not every scam works on every caller. Check the **Caller** tab to see which.

Run the mod on the **host**. Calls are simulated there.

### Callers and wealth

Every caller rolls a wealth tier: **Broke**, **Regular**, **Well-off** or **Whale**. Richer callers pay more (x0.4 up to x5, applied to the stock scams too) but are warier and harder to convince.

### Heat and getting caught

Crypto scams (packs with a `payoutCoin`) pay in made-up coins (Bitcorn, Ethos, Dohdge, Stabl) instead of cash, and each one adds **heat**: +1 for a broke caller up to +8 for a whale. Regular scams add no heat.

- Heat drops a little every in-game day on its own. Nothing you can buy lowers it.
- The first day you end above 50 heat you get a warning banner. Every day you stay above 50 after that can end in a raid.
- A raid seizes everything in your crypto wallet and shows the **CRIME DOESN'T PAY.** screen. Your cash, levels and apps stay, and heat drops back to 25.

### Mod menu (F8)

- **Scams:** turn each scam on or off, see its objectives, and reload packs without restarting.
- **Caller:** dossier on the current caller: wealth tier, estimated net worth, wariness, and which services they use. Also sets your target preference (any, or one tier).
- **Heat:** heat meter, coin prices, what's in your wallet, and recent payouts and raids.
- **Cheats:** pin your money and level fields (found with the built-in value finder), add money, infinite money, level lock, and turn off automatic incoming calls. In multiplayer these only work for the host.
- **Diagnostics:** see which game classes and hooks were found, and dump the game's live scam catalog, Scamazon products and computer apps to `BepInEx/Hotline-catalog.json`.
- **Settings:** logging, whether stock scams scale with caller wealth, and a preview of the raid screen. The menu key is set in the config file.

**First-time cheats setup:** the mod doesn't know yet where the game stores money and level. Open **Cheats**, press **Scan**, and pin the field whose value matches your money. If several match, earn or spend some money, scan again, and pin the one that changed. The choice is saved.

**Turning off automatic calls** works the same way: press **Find call methods**, tick the one that rings the phone, and switch off **Automatic incoming calls**. Untick it if the game misbehaves.

### Making your own scam

Drop a `.json` file in `BepInEx/config/Hotline/ScamPacks/` and press **Reload scam packs**. Copy one of the [bundled packs](src/Hotline/ScamPacks) as a starting point:

```jsonc
{
  "id": "my-scam",                 // unique; the app id is "my-scam", the Scamazon product is "scam-my-scam"
  "brand": "Brand",
  "title": "Shown in Scamazon and the menu",
  "description": "Shop blurb",
  "reward": 400,                   // paid when the final objective completes
  "shop": { "price": 1000, "unlockDay": 1, "iconText": "MS", "accentColor": "#4A90E2" },
  "callerPersona": "Added to the AI prompt of callers who use this service, so they have a reason to fall for it.",
  "payoutCoin": "BTCO",            // optional: pay in crypto (BTCO, ETHO, DOHG, USDS) instead of cash; adds heat
  "requiresAnyOf": ["glimmail-login"], // optional, crypto only: another scam that must succeed earlier in the call
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
dotnet run -c Release --project tests/Hotline.Tests
```

These check the scam injection, wealth tiers, caller prompts, heat and raids, Scamazon listing and desktop apps against stand-in classes shaped like the game's, plus the installer against temporary Steam and game folders. They can't prove the real game behaves the same, so test in game too.

## Notes

- New scams' final step is judged by the AI from dialogue. The stock scams instead make you type a revealed card number or code into their app. If the game turns out not to pay AI-judged final objectives, use the catalog dump to see how the stock submissions work and we'll wire that up.
- The crypto market advances when the game's day changes (`ScamazonUnlockSchedule.CurrentDay`). If the Diagnostics tab reports it missing, prices and heat won't move.
- The game is in Early Access and updates often, so hooks can break. Check `LogOutput.log` for `Game probe` warnings after an update.
- The game's anti-cheat status for online play is unknown. Test mods in private lobbies with friends who agreed to use them.
