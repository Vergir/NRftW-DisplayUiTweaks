# Internals

How the mod works, and the game behaviour it depends on. Method names are from game build 29466 as seen through
MelonLoader's Il2CppInterop assemblies (`Il2Cpp__forsaken.dll`: namespace `Moon.Forsaken` becomes `Il2CppMoon.Forsaken`,
types without a namespace live in `Il2Cpp`).

## Source map

| File | What it does |
|---|---|
| `DisplayUiTweaksMod.cs` | Entry point: preferences, Harmony, re-apply on scene load / resolution change, unload. |
| `Prefs.cs` | MelonPreferences entries (`[DisplayUiTweaks]`). |
| `UiBox.cs` | The UI box: replacement for `UIAspectConstraint.ApplyConstraint`. |
| `UiScaling.cs` | CanvasScaler overrides and PanelSettings fitting. |
| `UiToolkitBoxing.cs` | Inline styles that box the bounty boards and the map's detail bar. |
| `RenderPipelineTweaks.cs` | Letterbox off. |
| `SettingsRows.cs` | The rows in Options > Display. |
| `Patches/*.cs` | Harmony patches. |

## Resolution list

`DisplaySettingsTab.InitializeAvailableResolutions` only lists resolutions with an aspect between 1.6 (16:10) and
3.5556 (32:9). The check (`IsValidAspectRatio`) is inlined into it, so it cannot be hooked on its own. At its end the
method calls `SetResolutionFromCurrentSettings(true)`, which applies and saves `m_allowedResolutions[m_currentResolutionIndex]`
through `Screen.SetResolution`. When the saved resolution is not in the game's narrow list, that index points somewhere
else, and the game switches to (and saves) a different resolution at every launch.

The mod:

1. Suppresses `SetResolutionFromCurrentSettings` while the original runs (prefix sets a flag, a prefix on
   `SetResolutionFromCurrentSettings` skips it).
2. Rebuilds `m_allowedResolutions` / `m_resolutionNames` from `Screen.resolutions`, reusing the game's name format.
3. Selects the saved resolution (`Core.SettingsData.Device.Resolution`), falling back to the window size.
4. Applies it with `SetResolutionFromCurrentSettings(false)` only if the window differs from it, then calls
   `UpdateResolutionDropdownOptions`.

Step 4 matters on the Steam Deck: there Unity's own window prefs said 800x800 while the game's setting said 1280x800.
`CheckCustomResolution` treats a window size that is not in the list as "custom" and leaves it alone, so neither the game
nor a plain "keep the window size" rule corrects it.

## UI aspect modes

`UIAspectMode`: Native (0), Aspect16x9, Aspect21x9, Aspect32x9, Custom (4). The saved value is
`SettingsData.Device.UIAspectMode`; the live one is `UIAspectConstraint.s_globalMode`.

* The Display tab hides the UI Aspect option on non-widescreen monitors and offers only the modes that fit the monitor
  (`SupportsWideScreenUIAspectSetting`, inlined). The game has two debug switches that turn this off:
  `DisplaySettingsTab.s_forceShowUIAspectSettingForTesting` and `s_forceAllUIAspectModesForTesting`.
* Custom exists in the game but is never offered. An `InitializeUIAspectModes` postfix appends it to
  `m_allowedUIAspectModes` / `m_uiAspectModeNames` as "Custom (Display & UI Tweaks)".
* `InitializeUIAspectModes` resets a saved mode that is not in its list (Custom never is) to Native. The prefix captures
  the saved mode, the postfix restores it and calls `SetGlobalMode`.

## The UI box (uGUI)

Every screen root has a `UIAspectConstraint`. Its `ApplyConstraint` (RVA 0x8C22FC0) is, transcribed:

```
parent = GetParentSize(); if (parent.x <= 0 || parent.y <= 0) return;
mode = (m_useGlobalMode && Application.isPlaying) ? s_globalMode : m_mode;
if (mode == Native) { ApplyStretchToParent(rt); w = parent.x; h = parent.y; }
else { target = aspect(mode);
       if (parent.x/parent.y > target) { w = parent.y*target; h = parent.y; } else { w = parent.x; h = parent.x/target; }
       rt.anchorMin = rt.anchorMax = rt.pivot = (0.5,0.5); rt.anchoredPosition = 0; rt.sizeDelta = (w,h); }
ApplySafeZone(w, h);
```

`GetTargetAspect` is inlined into it, so the mod replaces the whole method (Harmony prefix returning false) with the
same logic plus two changes:

* Custom uses the Custom UI Aspect Ratio slider.
* A constraint whose parent is not screen-shaped is stretched instead of boxed. Two screens (the scribe table,
  `researchRecipesScreen`, and `inspectPlayerScreen`) have a fixed 1920x1080 root; boxing those to a narrow box cuts
  their content off.

`SetGlobalMode(mode)` re-applies every live constraint; the mod calls it to refresh after a slider change, and
postfixes it so UI Toolkit panels follow a UI Aspect change.

## Scaling

The game has two kinds of `ScaleWithScreenSize` canvases, both with reference resolution 1920x1080 and
match = 1 (height):

| Kind | `fallbackScreenDPI` | Examples |
|---|---|---|
| HUD | 96 | HUD, overlays, notifications, dialogue (pooled prefabs) |
| Menu | 221.5 | the player menu screens (inventory, stats, settings...) |

`CanvasScaler.HandleScaleWithScreenSize` runs every frame. The prefix computes Unity's own result (same formula as
Unity's source), then:

* HUD: game scale × HUD & Dialogue UI Size.
* Menu: min(game scale, the scale at which 1920x1080 fits the UI box) × Menu UI Size.

When the result equals the game's, the original runs untouched. Otherwise the prefix does what the original does with
our value (`SetScaleFactor`, `SetReferencePixelsPerUnit`) and skips it.

## UI Toolkit screens

Four screens use UI Toolkit (`UIDocument`): `ActivityScreenPanel.Document` and `ActivityVendorScreenPanel.Document`
(bounty and challenge boards), `MapScreen.MapUiToolkitOverlay`, `FastTravelMenuScreen.MapSnippet`.

* **Scale.** Their `PanelSettings` get the same fit as menu canvases: switched to `ConstantPixelSize` with `scale` =
  our factor (for that mode `ResolveScale` returns 1/scale, so `scale` behaves like a uGUI scale factor). The original
  mode and scale are remembered and restored when no fit is needed or on unload. `PanelSettings.ResolveScale` is inlined
  into `ApplyPanelSettings`, so the asset properties are the only knob. The assets load with their screen, so the mod
  applies them in a `UIDocument.OnEnable` postfix as well as on scene load.
* **Box.** The game adds a USS class per UI aspect mode to these screens (`ui-aspect-native`, `-16x9`, `-21x9`, `-32x9`,
  `-custom`; `ActivityScreenPanel.UpdateAspectLayoutClass`, `MapScreen.UpdateChunkDetailsAspectClass`), but its style
  sheets (`ActivityWindow`, `Map` in the static scenes bundle) only use it for two elements:
  `.activity-screen.ui-aspect-16x9 .activity-screen-bottom-bar { width: 1920px; centered }` and
  `.chunk-details-aspect-inner.ui-aspect-16x9 { max-width: 1920px }`. The mod boxes with inline styles, which win over
  style sheets: the activity documents' root element is positioned absolutely inside the UI box (percent insets, so it is
  independent of the panel scale), and the map's `.chunk-details-aspect-inner` gets a percent max-width. The map itself is
  left full-screen: its markers are laid over uGUI map tiles that are not boxed.

## Settings rows

The rows are built with the game's own `SettingsScreenControls.AddSliderItem` and `AddActualDropDownItem` (string[]
overload) in a `DisplaySettingsTab.Initialize` postfix.

* `AddSliderItem` works on a normalized 0..1 value with a fixed increment; the mod maps that to its ranges. Its
  `IPlayerSettingAdapter<float>` argument is only stored by the row, never read, so null is passed.
* Labels are `LocalizedMessage` ScriptableObjects created at runtime with the same text in every language field.
* `SettingsScreenControls.m_categoryToContentToItem` is a per-category dictionary keyed by the label's Id. Adding the
  same Id twice throws after the row prefab was instantiated, which leaves an orphan row labelled "Slider".
* The game builds a settings screen when a scene loads (boot, entering the realm), not when the menu is reopened, and
  `DisplaySettingsTab.Initialize` can run twice for the same screen during boot. Rows are therefore found by their
  GameObject name (`DUT_*`) instead of static state.
* The controls also keep direct references to rows: `m_actualDropDownInstances` (Back calls `IsOpen` on each through
  `IsAnyDropDownOpen`), `m_boundDropDownInstances`, `m_cachedSelectedItemGUI`, `m_modalPreviousElement`. Rows must be
  removed from those before they are destroyed, or Back throws and stops working.

## Letterbox

`MoonRenderPipelineView.Render` letterboxes the world to 16:9 when `MoonRenderPipelineAsset.Enforce169Aspect` says so
(`InBuilds` in the shipped game). The mod sets the asset field to `Never`.

## Per-screen layout checks

Some screens choose a layout from the screen aspect themselves:

* `InventoryScreenMain`, `InventoryItemsElement` and `CommunityChestScreenV2.IsResolutionSupported` (compare-panel
  placement) return true when the saved UI Aspect is 16:9, otherwise when the monitor is 16:9 or 16:10.
* `RealmScreen.IsSixteenByTen`, `WhisperUpgradeScreen` / `PlagueSystemBurnScreen.OnResolutionCheck`,
  `PlayerToPlayerTradeScreenV2.ApplyAspectTweaks1610`, `PlayerSubtitleView.Show` special-case 16:10 only.

The mod leaves them alone; in testing the layouts they pick on non-16:9 screens looked fine.

## Timing

* The world streams dozens of additive scenes (about 95 during a load); `OnSceneWasLoaded` only schedules one re-apply
  0.5 s after the last one.
* `OnUpdate` re-applies immediately when the resolution changes.

## Hot reload

The mod supports MelonLoader HotReload:

* All patches go through the mod's `HarmonyInstance`.
* `OnDeinitializeMelon` restores PanelSettings, removes the inline styles, unregisters and destroys the settings rows,
  and re-applies the constraints.
* `OnInitializeMelon` re-applies to what is on screen and adds the rows to the settings screens that already exist,
  because the game will not build them again until the next scene load.
