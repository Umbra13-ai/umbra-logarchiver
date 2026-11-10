# LogArchiver

Keeps every game session's logs. Demeo and BepInEx otherwise overwrite them each time the game starts.

## What it saves

Each session gets a dated folder, such as `LogArchive\2026-11-09_19-30-05\`, holding:

| File | What it is |
| --- | --- |
| `LogOutput.log` | BepInEx and mod log (HouseRules activation, syncs, warnings). |
| `Player.log` | Unity log: the game's own events, errors and frame hitches. |
| `MotherBrainLog{0}.txt` | Demeo's internal log. |
| `session.txt` | Start time, game and Unity versions, and the paths of the source logs. |

The logs are copied every 2 minutes during play and again when the game closes. At the next launch, Unity's `Player-prev.log` replaces the last copy of `Player.log` in the previous session's folder, so that file is complete. If the previous session ran without this plugin, its Unity log is kept in a folder of its own, named with a `_unity-log-only` suffix. If the game crashes, the BepInEx and Demeo logs lose at most the last copy interval.

Nothing in the archive is ever deleted. A short launch takes a few hundred KB; a long session takes roughly 10 to 20 MB.

## Location

By default the archive is next to `Player.log`:
`%USERPROFILE%\AppData\LocalLow\Resolution Games\Demeo\LogArchive`

This survives reinstalling the game.

## Settings

`BepInEx\config\com.demeorevolutions.logarchiver.cfg` is created on first launch:

- `Enabled`: turns archiving on or off (default `true`).
- `ArchiveFolder`: a different archive folder (default empty, meaning the location above).
- `CopyIntervalMinutes`: how often logs are copied during play (default `2`; values below `1` are treated as `1`).

## Tested with

The Windows version of Demeo (game version 1.42, Unity 6000.0.60f1; its install folder is named `demeorift_release`), played on a Meta Quest 2 connected to the PC, with BepInEx 5.4.22. It has not been tried on other versions or headsets.

## Install

Copy `LogArchiver.dll` into `BepInEx\plugins`. The plugin patches nothing in the game and works without the other plugins. BepInEx only; MelonLoader already keeps its own log history.

## Build

`dotnet build LogArchiver/LogArchiver.csproj -c Release`; the DLL is `LogArchiver/bin/netstandard2.1/LogArchiver.dll`. The BepInEx packages come from the feed in `NuGet.Config`.

## History

Written on 8 November 2026 for a Demeo Revolutions setup and moved to its own repository on 9 November 2026. Version numbers continue from the Demeo Revolutions releases; the first public release is 3.12.0. By Umbra, MIT licence. Not affiliated with Resolution Games.
