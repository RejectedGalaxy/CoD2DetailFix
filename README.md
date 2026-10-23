# CoD2DetailFix: Detail Fix for Permanent Corpses (Call of Duty 2)

Optional add-on for **Permanent Corpses for Call of Duty 2** on Nexus Mods (`PermaCorpse-CoD2-DetailFix-1.0.0.zip`):
bodies keep their belts, pouches and gear up close.

## The problem

Call of Duty 2 switches models to simpler versions only a few feet from the camera, which strips small parts
(belts, pouches, gear) off bodies right in front of the player. The game's `r_lodBias` setting fixes that, but it
is cheat-protected: the game rejects it from the console, the command line, config files and game scripts
(`setcvar` / `setsavedcvar`), and resets it at every level load. The related settings (`r_lodScale`,
`r_forceLod`, `r_highLodDist` and the other LOD distances) are cheat-protected too.

## What it does

`CoD2DetailFix.exe` is a Steam launch-option wrapper. Steam starts it with the game's own command line
(`"...\CoD2DetailFix.exe" %command% <mod options>`). It starts the game exactly as Steam asked and, while single
player (`CoD2SP_s.exe`) runs, keeps `r_lodBias` at -500 (models keep full detail about 40 feet longer):

* It looks for the setting's record in the game's own image (`VirtualQueryEx` / `ReadProcessMemory`), and accepts it
  only if the name pointer reads `r_lodBias`, the type is float and the allowed range is -1000..0.
* Every 2 seconds it makes sure the record's current, latched and reset values are -500 (`WriteProcessMemory` of
  those 12 bytes only). Nothing else is written, cheats stay off, and no files are changed.
* Any other exe (Multiplayer) is started unchanged. `--lod-bias <-1000..0>` before `%command%` picks another value;
  `--lod-bias 0` only passes the game through.

## Files

| File | |
|---|---|
| `CoD2DetailFix.cs` | the whole program (C#, .NET Framework 4) |

## Build

Requirements: Windows (the .NET Framework 4 C# compiler is built in).

```bat
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -target:winexe -optimize -out:CoD2DetailFix.exe -r:System.Windows.Forms.dll CoD2DetailFix.cs
```

## Released binary (SHA-256)

The file inside `PermaCorpse-CoD2-DetailFix-1.0.0.zip`. A rebuild will not match byte for byte, because the
compiler embeds a build timestamp and ID, but it behaves the same.

```
a2fed4bce58f946e44073701c5dcb9050caa52b5da9871f1b6f45d0f00f9c699  CoD2DetailFix.exe
```

## Use

1. Install Permanent Corpses for Call of Duty 2, then extract this zip into the Call of Duty 2 folder
   (it adds `mods\permacorpse\CoD2DetailFix.exe`).
2. Steam: right-click Call of Duty 2 > Properties > General > Launch Options:
   `"<Call of Duty 2 folder>\mods\permacorpse\CoD2DetailFix.exe" %command% +set fs_game mods/permacorpse +set cg_marksLimit 4096`

## License

MIT, see LICENSE. Call of Duty is a trademark of Activision; this tool is not affiliated with or endorsed by Activision.
