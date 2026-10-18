# Scam With Friends Modded

Mods for [Scam With Your Friends](https://store.steampowered.com/app/4954910/Scam_With_Your_Friends/) (Steam, Early Access).

The game is a Unity (Mono) game, so mods are [BepInEx 5](https://github.com/BepInEx/BepInEx) plugins that use [Harmony](https://github.com/BepInEx/HarmonyX) to patch the game's C# code (`Assembly-CSharp.dll`).

## What's in here

| Path | What it is |
| --- | --- |
| `src/ScamWithFriendsModded/` | Starter plugin: config, an in-game status overlay (F8), and example Harmony hooks on the scam-call session |
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
5. **Launch the game** and press **F8**. You should see the overlay, and `BepInEx/LogOutput.log` should contain `Scam With Friends Modded 0.1.0 loaded`.

Config is written to `BepInEx/config/com.krat3r.scamwithfriendsmodded.cfg` after the first launch.

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

`GameHooks.cs` shows the pattern: look the type up by name with `GameProbe`, then patch it with Harmony. If a game update renames something, the mod logs a warning and keeps running instead of crashing. Once you've found a method in `decompiled/`, a typed patch looks like this:

```csharp
[HarmonyPatch(typeof(ConversationScamSession), "SubmitField")]
static class MyPatch
{
    static void Postfix(ConversationScamSession __instance) { /* ... */ }
}
```

Typed patches like this need `Assembly-CSharp.dll`, which the build references automatically when it finds the game. Register them with `harmony.PatchAll()`.

## Notes

- The game is in Early Access and updates often, so hooks can break. Check `LogOutput.log` for `Game probe` warnings after an update.
- The game's anti-cheat status for online play is unknown. Test mods in private lobbies with friends who agreed to use them.
