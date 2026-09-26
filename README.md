# MoreAspectRatios

MelonLoader mod for **No Rest for the Wicked** that makes the game usable on any monitor shape.

Out of the box:

* every resolution your display supports is selectable in Options > Display (the game hides anything narrower than 16:10 or wider than 32:9),
* the "UI Aspect" option is always shown, with all modes plus a new "Custom (More Aspect Ratios)" mode,
* no 16:9 letterbox on non-16:9 screens,
* menus and UI Toolkit panels (map, fast travel, activities) are scaled so they fit inside the HUD box instead of being cropped.

Two new sliders in Options > Display:

* **HUD Size** (10%-150%, game default 100%),
* **HUD Box Aspect** (1.00-4.00; the box used by the "Custom (More Aspect Ratios)" UI aspect mode - set 1.00 for a square HUD on a 9:8 or 1:1 screen).

Everything else lives in `UserData/MelonPreferences.cfg`, section `[MoreAspectRatios]`.

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

* `UIAspectConstraint.ApplyConstraint` is replaced (Harmony prefix) so the "Custom" mode uses the slider aspect, and so screens with a fixed-size root are stretched instead of boxed.
* `CanvasScaler.HandleScaleWithScreenSize` is prefixed: HUD canvases get the game's scale times the HUD Size slider; menu canvases are capped to "reference width fits the HUD box".
* `DisplaySettingsTab.InitializeAvailableResolutions` is postfixed to rebuild the resolution list from `Screen.resolutions`.
* `DisplaySettingsTab.Initialize` is postfixed to add the two sliders through the game's own `SettingsScreenControls.AddSliderItem`.
* The letterbox is a flag on `MoonRenderPipelineAsset`; UI Toolkit panels are switched to constant pixel size when they need to shrink.
* The UI aspect dropdown is unlocked through the game's own `s_forceShowUIAspectSettingForTesting` / `s_forceAllUIAspectModesForTesting` switches; the "Custom" entry is appended in an `InitializeUIAspectModes` postfix.
