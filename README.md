# MoreAspectRatios

MelonLoader mod for **No Rest for the Wicked** that makes the game usable on any monitor shape.

Out of the box:

* every resolution your display supports is selectable in Options > Display (the game hides anything narrower than 16:10 or wider than 32:9, and resets a saved resolution outside that range at every launch),
* the "UI Aspect" option is always shown, with all modes plus a new "Custom (More Aspect Ratios)" mode,
* no 16:9 letterbox on non-16:9 screens,
* menus are scaled so they fit inside the UI box instead of being cropped,
* the bounty and challenge boards and the map's detail bar are kept inside the UI box too (the game's own UI Aspect option only boxes part of them).

New rows in Options > Display:

| Row | Range | Default | What it does |
|---|---|---|---|
| HUD & Dialogue UI Size | 10-150% | 100% | In-game HUD, overlays and dialogue. |
| Menu UI Size | 10-150% | 100% | Inventory, stats, map, settings and the other menus, on top of the fit-to-box shrink. |
| Custom UI Aspect Ratio | 1.00-4.00 | 1.78 | The box the whole UI is kept in when UI Aspect is "Custom (More Aspect Ratios)". 1.00 = square. |
| UI Edge Margin (Left/Right) | 0-25% | 0% | Empty space at the left and right screen edges, per side. |
| UI Edge Margin (Top/Bottom) | 0-25% | 0% | Same for top and bottom. |
| Custom Mode Menu Layouts | Monitor / 16:9 | Monitor | In Custom mode, whether the inventory and community chest pick their layout from the monitor's shape (game behaviour) or use the layout the game uses for its own 16:9 UI Aspect mode. |

Margins and aspect combine: the margins cut the usable area out of the screen, then the aspect box is fitted inside it.
Margins work in every UI Aspect mode, including Native. On an ultrawide, Custom aspect keeps the UI proportional to the
screen height; a left/right margin is a fixed share of the width. Either works, and they can be combined.

The game uses two kinds of canvases: HUD canvases (fallback DPI 96) and menu canvases (fallback DPI 221.5), which is
how the two size sliders tell them apart.

Everything is also in `UserData/MelonPreferences.cfg`, section `[MoreAspectRatios]`.

## Install

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader/releases) 0.7.3 or newer into the game, run the game once.
2. Drop `MoreAspectRatios.dll` into `<game>/Mods`.

Steam Deck / Linux: add `WINEDLLOVERRIDES="version=n,b" %command%` to the game's launch options.

## Build

Requires a .NET SDK (6 or newer) and MelonLoader installed in the game (so `MelonLoader/Il2CppAssemblies` exists).

```
dotnet build -c Release
```

The DLL is copied to `<game>/Mods` after every build (`-p:DeployToGame=false` to skip,
`-p:GameDir=...` if the game is installed elsewhere).

## How it works

* `UIAspectConstraint.ApplyConstraint` is replaced (Harmony prefix): margins, the Custom aspect, and screens with a fixed-size root are stretched instead of boxed. `SetGlobalMode` is postfixed so everything else follows a UI Aspect change.
* `CanvasScaler.HandleScaleWithScreenSize` is prefixed: HUD canvases get the game's scale times HUD & Dialogue UI Size; menu canvases are capped so 1920x1080 fits the UI box, times Menu UI Size.
* UI Toolkit: `PanelSettings` get the same fit, applied when a `UIDocument` enables. The game gives these screens a `ui-aspect-*` USS class but its style sheets only use it for the activity bottom bar and the map's chunk details, so the mod boxes the activity documents' root and caps the chunk-details width with inline styles.
* `DisplaySettingsTab.InitializeAvailableResolutions` is wrapped: the game's own "apply a fallback resolution because the saved one is not in my list" step is suppressed, then the list is rebuilt from `Screen.resolutions`. The saved UI aspect mode is captured before `InitializeUIAspectModes` and restored, because the game resets an unknown mode to Native.
* `InventoryScreenMain` / `InventoryItemsElement` / `CommunityChestScreenV2.IsResolutionSupported` are postfixed for the Custom Mode Menu Layouts option. The game already returns "supported" for its own 16:9 UI Aspect mode.
* `DisplaySettingsTab.Initialize` is postfixed to add the rows through the game's own `SettingsScreenControls.AddSliderItem` / `AddActualDropDownItem`.
* The letterbox is a flag on `MoonRenderPipelineAsset`. The UI aspect dropdown is unlocked through the game's own `s_forceShowUIAspectSettingForTesting` / `s_forceAllUIAspectModesForTesting` switches; the "Custom" entry is appended in an `InitializeUIAspectModes` postfix.
* Hot reload ([MelonLoader HotReload](../MelonLoader_HotReload)): all patches go through the mod's `HarmonyInstance`; `OnDeinitializeMelon` removes the settings rows, inline styles and panel changes, and the new build re-applies on init.
