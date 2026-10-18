# ScamWYF.Setup.Core (vendored)

Hotline's installer uses this library to make BepInEx start in Scam With Your Friends. The game ships a stripped .NET class library, so BepInEx needs unstripped copies of eight libraries, plus patched copies of a few game files that no longer match them. This library installs the loader, seeds those libraries and runs the two patch steps. It never writes to the game's `Managed` folder.

- **Upstream:** https://github.com/swyf-modding/Setup
- **Commit:** `02a97179b4db3e59be4d04850411595b170a85e3`
- **Copied:** `src/ScamWYF.Setup.Core/**/*.cs` → `src/`, `vendor/` → `vendor/`, `LICENSE`
- **Changed:** the project file targets `net8.0` instead of `net10.0`, and its resource paths point at `./vendor`. Source files are unmodified.

## Licences

| Part | Licence | File |
| --- | --- | --- |
| `src/` | MIT, © 2026 Ras_rap | [LICENSE](LICENSE) |
| `vendor/corlib/*.dll` (Mono class libraries, from a Unity 6000.3.10f1 editor install per upstream) | MIT | [vendor/corlib/LICENSE-Mono](vendor/corlib/LICENSE-Mono) |
| `vendor/doorstop/` (UnityDoorstop 4.5.0) | LGPL-2.1 | [vendor/doorstop/LICENSE](vendor/doorstop/LICENSE) |

## Binary hashes (SHA-256)

Check these when updating from upstream; any difference means the binaries changed.

```
6c76e06e6d44cf0f39bf59e6a90770968b70093ad4c1572dfbc5e46e431c5fbf  vendor/corlib/mscorlib.dll
7019d6b6dbfa07cf30f62e7d3ccb969f70cbf001fd72112938ffb535c7f75d81  vendor/corlib/System.dll
a5e7d47313da0eca0b92a901a30498f5e970916faf63b70b74dd62b09760ed72  vendor/corlib/System.Core.dll
04907729e87a101e62d3a5c93cfa16688bee745d05440d6daa0c541964be27ab  vendor/corlib/System.Configuration.dll
ac9224b32355a09f09b706cab568384f64cf8116825c5911cef1e13221c61c38  vendor/corlib/System.Numerics.dll
2101a4dd4475285f96b1ac3ba1715a52aa8067690709e675cc49a9eb8ed901a4  vendor/corlib/System.Security.dll
79194220adfcf027a334c87b1adcc6d07b9660a4061e04b3d9500c10d30f7d49  vendor/corlib/System.Xml.dll
618009319dbf3ae374f9b5dc0d0522e1e1bdd172200c120a5471747b7991a584  vendor/corlib/Mono.Security.dll
8c6cdbc38836dee87e3368f5de1994d7c0ccebf29e4ce7aba3c0981f9375412c  vendor/doorstop/winhttp.dll
```

## Updating

The libraries must match the game's Unity version. After a game update that changes it, take the matching upstream release, re-copy the same paths, update the commit and hashes above, and rebuild the installer.
