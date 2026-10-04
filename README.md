# Display & UI Tweaks

![Display & UI Tweaks](docs/pics/nexus/header.jpg)

A [MelonLoader](https://github.com/LavaGang/MelonLoader) mod for **No Rest for the Wicked** that makes the game work on
any screen shape: ultrawide, 16:10, square, tall, and the Steam Deck. It adds UI area and UI size settings to the game's
own Options menu, a drag-and-drop HUD layout editor, and an option to hide the HUD outside combat.

Download: [Nexus Mods](https://www.nexusmods.com/norestforthewicked/mods/102) · [GitHub releases](https://github.com/vergir/NRftW-DisplayUiTweaks/releases/latest)

## Features

Out of the box:

* Every resolution your display supports can be picked in **Options > Display**. The game hides anything narrower than
  16:10 or wider than 32:9, and replaces such a saved resolution with a different one at every launch.
* No black 16:9 bars around the world on non-16:9 screens.
* The whole UI (HUD, menus, dialogue) is kept in a **UI area** of any shape, set with one slider.
* Menus shrink to fit the UI area instead of being cut off.
* Everything stays inside the UI area, also the parts the game pins to the screen edges: the bounty and challenge
  boards, the map's detail bar, bounties and challenges on the HUD, item pickups, the hint bar, the boss bar, the plague
  meter and the area banner.

New rows in Options (at the bottom of **Options > Display**; with the Mod Settings Tab mod installed they are in its
**Mods** tab):

| Row | Range | Default | What it does |
|---|---|---|---|
| UI Area | 1.00-4.00 | 1.78 | Shape of the area the whole UI is kept in: its width divided by its height. 1.00 = square, 1.78 = 16:9, 2.33 = 21:9, 3.56 = 32:9. Replaces the game's own UI Aspect Mode setting. |
| HUD & Dialogue UI Size | 10-150% | 100% | In-game HUD, overlays and dialogue. |
| Menu UI Size | 10-150% | 100% | Inventory, stats, map, settings and the other menus. |
| Edit HUD Layout | button | | Opens the HUD layout editor (below). Works in the main menu too. |
| Reset HUD Layout | button | | Puts every HUD element back where the game has it. |
| Hide HUD Outside Combat | Off / On / On, but show health while hurt | Off | Fades out health, equipment, money, durability, clock and location a few seconds after combat ends; they come back when combat starts. Item pickups, hints and chat stay. |

With the defaults on a 16:9 monitor the game looks exactly as before. On wider monitors the UI area starts at 16:9:
raise UI Area to use more of the screen width.

### HUD layout editor

**Edit HUD Layout** shows the HUD on top of the game (or, from the main menu, a copy of it), with sample content in
elements that are empty right now: chat lines, item pickups, a bounty and a challenge, two teammates, a boss bar, a
worn-out durability figure.

* Drag an element to move it, use the mouse wheel to resize it, right click to put it back.
* Tab picks the next element under the cursor where elements overlap; the picked one draws on top.
* R resets everything, Esc closes the editor.

Elements: health & stamina, party, equipment, money, durability, clock & weather, realm difficulty, location, bounties
& challenges, chat, item pickups, hint, boss health, plague meter, area banner, Crucible floor. Positions are stored
relative to the UI area, so a layout survives resolution, UI Area and HUD size changes.

### Examples

* **Ultrawide:** keep UI Area at 1.78 for a 16:9 UI in the middle of the screen, or raise it (2.33 for 21:9) to spread
  the UI out.
* **Square or tall monitor:** pick your resolution, set UI Area to 1.00, then adjust HUD & Dialogue UI Size to taste.
* **Steam Deck:** works as is; use the size sliders if the text is too small.

## Screenshots

HUD size, 75% / 100% / 125%:

![HUD size](docs/pics/nexus/hud_size.jpg)

Ultrawide: the game's 16:9 UI box and a custom one:

![Ultrawide UI box](docs/pics/nexus/ultrawide_game_vs_custom.jpg)

A 9:8 monitor without and with the mod:

![Square monitor](docs/pics/nexus/square_before_after.jpg)

More: [settings](docs/pics/nexus/settings.jpg), [custom ratios](docs/pics/nexus/ultrawide_custom_ratios.jpg),
[menu](docs/pics/nexus/ultrawide_menu.jpg), [stats](docs/pics/nexus/ultrawide_stats.jpg).

## Install

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader/releases) **0.7.3** or newer into the game
   (`...\steamapps\common\NoRestForTheWicked`) and start the game once.
2. Put `DisplayUiTweaks.dll` into the game's `Mods` folder (the [release zip](https://github.com/vergir/NRftW-DisplayUiTweaks/releases/latest) already has that layout: extract it into
   the game folder).

### Steam Deck / Linux (Proton)

1. Install MelonLoader into the game folder the same way (copy the files from `MelonLoader.x64.zip`, the Windows build,
   into `~/.local/share/Steam/steamapps/common/NoRestForTheWicked`), and the mod into `Mods`.
2. In Steam, open the game's **Properties > General > Launch Options** and enter:
   ```
   WINEDLLOVERRIDES="version=n,b" %command%
   ```
3. Start the game. On the first start MelonLoader downloads and installs the .NET runtime it needs into the Proton
   prefix by itself, and generates its assemblies; that first start takes a minute or two.

## Settings file

Everything is also stored in `<game>/UserData/MelonPreferences.cfg`, section `[DisplayUiTweaks]`: the rows above
(`UiArea`, `HudScalePercent`, `MenuScalePercent`, `HideHudOutsideCombat`, and the editor's layout in `HudLayout`),
`HideHudDelay` (seconds after combat before the HUD fades), switches for each feature (`UnlockResolutions`,
`DisableLetterbox`, `BoxHudWidgets`, `BoxUiToolkitScreens`, `FitMenusToBox`, `FitPanelsToBox`, `StretchMismatchedRoots`,
`AddSettingsRows`, `AlwaysShowGameUiAspectOption`) and a master `Enabled` switch.

## Compatibility

* Tested with game build 29466 and MelonLoader 0.7.3 on Windows and on a Steam Deck.
* Display only: nothing in the game simulation or your save is changed, so it does not affect co-op.
* Coming from 1.0.0: a "Custom (Display & UI Tweaks)" UI aspect setup is carried over to UI Area once.
* Game updates can move things around. If the mod stops working after an update, check the MelonLoader log
  (`<game>/MelonLoader/Latest.log`) and open an issue.

## Build

Requires a .NET SDK (6 or newer) and MelonLoader installed in the game (so `MelonLoader/Il2CppAssemblies` exists).

```
dotnet build -c Release
```

The DLL is copied to `<game>/Mods` after every build (`-p:DeployToGame=false` to skip, `-p:GameDir=...` if the game is
installed elsewhere). `pwsh ./package.ps1` builds the release zip into `dist/`.

## How it works

See [docs/internal.md](docs/internal.md).

## License

[MIT](LICENSE)
