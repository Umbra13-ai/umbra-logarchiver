# LogArchiver

Keeps every game session's logs. Demeo and BepInEx otherwise overwrite them each time the game starts.

## What it saves

Each session gets a dated folder, such as `LogArchive\2026-11-09_19-30-05\`, holding:

| File | What it is |
| --- | --- |
| `LogOutput.log` | BepInEx and mod log (HouseRules activation, syncs, warnings). |
| `Player.log` | Unity log: the game's own events, errors and frame hitches. |
| `MotherBrainLog{0}.txt` | Demeo's internal log. |
| `session.txt` | Start time, game version and source paths. |

The logs are copied every 2 minutes during play and again when the game closes. At the next launch, Unity's `Player-prev.log` replaces the last copy of `Player.log`, so that file is always complete. If the game crashes, `LogOutput.log` loses at most the last 2 minutes.

Nothing in the archive is ever deleted. A session usually takes 5 to 25 MB.

## Location

By default the archive is next to `Player.log`:
`%USERPROFILE%\AppData\LocalLow\Resolution Games\Demeo\LogArchive`

This survives reinstalling the game.

## Settings

`BepInEx\config\com.demeorevolutions.logarchiver.cfg` is created on first launch:

- `Enabled`: turns archiving on or off (default `true`).
- `ArchiveFolder`: a different archive folder (default empty, meaning the location above).
- `CopyIntervalMinutes`: how often logs are copied during play (default `2`).

## Install

Copy `LogArchiver.dll` into `BepInEx\plugins`. The plugin patches nothing in the game and works without the other plugins. BepInEx only; MelonLoader already keeps its own log history.

## Build

`dotnet build LogArchiver/LogArchiver.csproj -c Release`; the DLL is `LogArchiver/bin/netstandard2.1/LogArchiver.dll`. The BepInEx packages come from the feed in `NuGet.Config`.

## History

Written on 8 November 2026 inside the Demeo Revolutions fork (commit b0f9cd6 there) and moved to its own repository on 9 November 2026. By Umbra, MIT licence.
