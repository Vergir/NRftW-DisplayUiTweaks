# More Aspect Ratios

A [MelonLoader](https://github.com/LavaGang/MelonLoader) mod for **No Rest for the Wicked** that makes the game work on
any screen shape: ultrawide, 16:10, square, tall, and the Steam Deck. It adds UI size and UI aspect settings to the
game's own Options menu.

## Features

Out of the box:

* Every resolution your display supports can be picked in **Options > Display**. The game hides anything narrower than
  16:10 or wider than 32:9, and replaces such a saved resolution with a different one at every launch.
* The **UI Aspect Mode** option is always shown (the game hides it on non-widescreen monitors), with all modes plus a
  new **Custom (More Aspect Ratios)** mode.
* No black 16:9 bars around the world on non-16:9 screens.
* Menus shrink to fit the UI box instead of being cut off.
* The bounty and challenge boards and the map's detail bar stay inside the UI box as well. The game's UI Aspect option
  only boxes parts of these screens.

New rows at the bottom of **Options > Display**:

| Row | Range | Default | What it does |
|---|---|---|---|
| HUD & Dialogue UI Size | 10-150% | 100% | In-game HUD, overlays and dialogue. |
| Menu UI Size | 10-150% | 100% | Inventory, stats, map, settings and the other menus. |
| Custom UI Aspect Ratio | 1.00-4.00 | 1.78 | Width-to-height ratio the whole UI is kept in when UI Aspect Mode is "Custom (More Aspect Ratios)". 1.00 = square, 1.78 = 16:9, 2.33 = 21:9. |
| Box Bounty Boards & Map Details | Off / On | On | Keep those screens inside the UI box. |

With the defaults on a 16:9 monitor the game looks exactly as before; the mod only adds options.

### Examples

* **Ultrawide:** set UI Aspect Mode to 16:9 (or Custom with any ratio) to keep the HUD and menus in the middle of the screen.
* **Square or tall monitor:** pick your resolution, set UI Aspect Mode to Custom and Custom UI Aspect Ratio to 1.00, then
  adjust HUD & Dialogue UI Size to taste.
* **Steam Deck:** works as is; use the size sliders if the text is too small.

## Install

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader/releases) **0.7.3** or newer into the game
   (`...\steamapps\common\NoRestForTheWicked`) and start the game once.
2. Put `MoreAspectRatios.dll` into the game's `Mods` folder (the release zip already has that layout: extract it into
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

Everything is also stored in `<game>/UserData/MelonPreferences.cfg`, section `[MoreAspectRatios]`. Besides the four
rows above it has switches for each feature (`UnlockResolutions`, `UnlockUiAspectModes`, `DisableLetterbox`,
`FitMenusToBox`, `FitPanelsToBox`, `StretchMismatchedRoots`, `AddSettingsRows`) and a master `Enabled` switch.

## Compatibility

* Tested with game build 29466 and MelonLoader 0.7.3 on Windows and on a Steam Deck.
* Display only: nothing in the game simulation or your save is changed, so it does not affect co-op.
* Game updates can move things around. If the mod stops working after an update, check the MelonLoader log
  (`<game>/MelonLoader/Latest.log`) and open an issue.

## Known limitations

* The map itself and the fast-travel map stay full-screen (their markers sit on top of map tiles that are not boxed).
  Only the map's detail bar is boxed.
* The inventory and community chest pick their compare-panel layout from the monitor's shape in Custom mode, like the
  game does for every mode except its own 16:9.

## Build

Requires a .NET SDK (6 or newer) and MelonLoader installed in the game (so `MelonLoader/Il2CppAssemblies` exists).

```
dotnet build -c Release
```

The DLL is copied to `<game>/Mods` after every build (`-p:DeployToGame=false` to skip, `-p:GameDir=...` if the game is
installed elsewhere). `pwsh ./package.ps1` builds the release zip into `dist/`.

## How it works

* `UIAspectConstraint.ApplyConstraint` is replaced (Harmony prefix): the Custom mode uses the slider aspect, and screens
  with a fixed-size root are stretched instead of boxed. `SetGlobalMode` is postfixed so everything else follows a UI
  Aspect change.
* `CanvasScaler.HandleScaleWithScreenSize` is prefixed: HUD canvases (fallback DPI 96) get the game's scale times HUD &
  Dialogue UI Size; menu canvases (fallback DPI 221.5) are capped so 1920x1080 fits the UI box, times Menu UI Size.
* UI Toolkit: `PanelSettings` get the same fit, applied when a `UIDocument` enables. The game gives these screens a
  `ui-aspect-*` USS class, but its style sheets only use it for the activity bottom bar and the map's chunk details, so
  the mod boxes the activity documents' root and caps the chunk-details width with inline styles.
* `DisplaySettingsTab.InitializeAvailableResolutions` is wrapped: the game's own resolution apply is deferred until the
  list is rebuilt from `Screen.resolutions`; then the saved resolution is selected, and applied if the window differs
  from it. The saved UI aspect mode is captured before `InitializeUIAspectModes` and restored, because the game resets
  an unknown mode to Native.
* `DisplaySettingsTab.Initialize` is postfixed to add the rows through the game's own
  `SettingsScreenControls.AddSliderItem` / `AddActualDropDownItem`.
* The letterbox is a flag on `MoonRenderPipelineAsset`. The UI aspect dropdown is unlocked through the game's own
  `s_forceShowUIAspectSettingForTesting` / `s_forceAllUIAspectModesForTesting` switches; the "Custom" entry is
  appended in an `InitializeUIAspectModes` postfix.
* Supports [MelonLoader HotReload](../MelonLoader_HotReload): all patches go through the mod's `HarmonyInstance`;
  `OnDeinitializeMelon` removes the settings rows, inline styles and panel changes, and the new build re-applies on
  init. The game only builds a settings screen when a scene loads, so the new build adds its rows to the live screens
  itself.

## License

[MIT](LICENSE)
