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
| `Hud/HudWidgets.cs` | The HUD elements the layout knows: paths under PlayerHUD, geometry helpers. |
| `Hud/HudLayout.cs` | The user's HUD layout: binding to the live HUD, saving, applying every frame. |
| `Hud/HudBoxing.cs` | Puts the HUD elements the game pins to the screen edges into a UI box. |
| `Hud/HudEditor.cs` | Edit HUD Layout: picking, dragging, resizing, the instructions panel. |
| `Hud/HudSamples.cs` | Sample content while editing the live HUD. |
| `Hud/MenuHud.cs` | The HUD copy the editor uses in the main menu. |
| `Hud/HideOutsideCombat.cs` | Hide HUD Outside Combat. |
| `DevCommands.cs` | Development commands (only with `UserData/DisplayUiTweaks/.dev`). |
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

## UI Area

`UIAspectMode`: Native (0), Aspect16x9, Aspect21x9, Aspect32x9, Custom (4). The game's saved value is
`SettingsData.Device.UIAspectMode`, the live one `UIAspectConstraint.s_globalMode`.

* The game's UI Aspect Mode option is shown only on widescreen monitors with the modes that fit the monitor
  (`SupportsWideScreenUIAspectSetting`, inlined). The game's debug switches `DisplaySettingsTab.s_forceShowUIAspectSettingForTesting`
  / `s_forceAllUIAspectModesForTesting` turn that off; the mod sets them only with the `AlwaysShowGameUiAspectOption`
  preference.
* The mod's **UI Area** (a ratio, 1.00-4.00) replaces it: every constraint in global mode gets the UI Area box, whatever
  `s_globalMode` says (`UiBox.GlobalAspect`). The game's option stays where it is, and its description
  (`DisplaySettingsTab.s_uiAspectModeDescription`, one static `LocalizedMessage`) gets a line saying UI Area replaces it;
  restored on unload.
* 1.0.0 offered the game's unused Custom mode as "Custom (Display & UI Tweaks)" with its own ratio (`UiBoxAspect`).
  `InitializeUIAspectModes` resets a saved mode that is not in its list (Custom never is) to Native; its prefix captures
  the saved mode, and once (`UiAreaMigrated`) a saved Custom mode becomes UI Area = the old ratio. The same check runs at
  start-up with the live mode, for a hot reload.

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

* Constraints in global mode use UI Area.
* The box is the overlap of the parent with the screen's UI box, in pixels (canvas scale taken from the scaler the
  mod computes, so it is right in the frame a setting changes). For a screen-sized parent that equals the game's rule.
  The game's rule fits the aspect inside the *parent*, which breaks parents that are not screen-sized:
  * full-width parents with their own height (the Knowledge screen `learnedRecipesScreen`, the status screen, the
    compendium) would get a box sized from their height;
  * fixed 1920x1080 roots (`researchRecipesScreen`, `inspectPlayerScreen`) were squashed to 1080 wide under a square
    box, cutting their left-anchored content off.
  Fixed-size parents (anchors collapsed, so their size does not follow the screen) keep their size and are scaled down (`localScale`,
  never up) only when they are larger than the box, e.g. with Menu UI Size above 100%. Undone on unload.

`SetGlobalMode(mode)` re-applies every live constraint; the mod calls it to refresh after a slider change, and
postfixes it so UI Toolkit panels follow a change.

## Scaling

The game's `ScaleWithScreenSize` canvases all use reference resolution 1920x1080. They fall into two kinds, told
apart by `fallbackScreenDPI` (a value the game never uses for scaling, so it serves as a marker):

| Kind | `fallbackScreenDPI` | Examples |
|---|---|---|
| HUD | 96 | HUD, overlays, notifications, dialogue (pooled prefabs); match = 1 (height) |
| Menu | 100 | the main menu canvas (with its settings screen); match = 0 (width) |
| Menu | 221.5 | the player menu screens (inventory, stats, settings...); match = 1 (height) |

Anything above 96 (`HudMaxDpi`) counts as a menu, so the main menu follows Menu UI Size like the in-game menus.

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

The rows are built with the game's own `SettingsScreenControls.AddSliderItem`, `AddButtonItem` and
`AddActualDropDownItem` (string[] overload) in a `DisplaySettingsTab.Initialize` postfix. Mod Settings Tab, if installed,
moves them to its Mods tab, so the mod finds its rows anywhere in the screen when it removes them.

* `AddSliderItem` works on a normalized 0..1 value with a fixed increment; the mod maps that to its ranges. Its
  `IPlayerSettingAdapter<float>` argument is only stored by the row, never read, so null is passed.
* An empty divider row (`AddDividerItem`, the same spacer the game uses between its groups) separates the block from
  the game's rows.
* Labels are `LocalizedMessage` ScriptableObjects created at runtime with the same text in every language field.
* `SettingsScreenControls.m_categoryToContentToItem` is a per-category dictionary keyed by the label's Id. Adding the
  same Id twice throws after the row prefab was instantiated, which leaves an orphan row labelled "Slider".
* The game builds a settings screen when a scene loads (boot, entering the realm), not when the menu is reopened, and
  `DisplaySettingsTab.Initialize` can run twice for the same screen during boot. Rows are therefore found by their
  GameObject name (`DUT_*`) instead of static state.
* The controls also keep direct references to rows: `m_actualDropDownInstances` (Back calls `IsOpen` on each through
  `IsAnyDropDownOpen`), `m_boundDropDownInstances`, `m_cachedSelectedItemGUI`, `m_modalPreviousElement`. Rows must be
  removed from those before they are destroyed, or Back throws and stops working.
* The main menu and the game each build their own settings screen, so each has its own copy of the rows, showing the
  value it was built with. A `SettingsScreen.Show` postfix sets every row of ours to the current value
  (`SliderSettingsItemGUI.SetValue(n, false)`, `ActualDropDownSettingsItemGUI.SetIndex(i, false)`: no callback).

## HUD layout

The HUD (`PlayerHUD`, a pooled prefab under the player's controller view) is a set of root canvases (ScreenSpaceCamera,
own CanvasScaler). Most have an `aspectRatio` / `apectRatio` child with a `UIAspectConstraint` (the UI box), whose
children are the elements, anchored to a corner of the box. The health bars and their background frame are on two
canvases (`playerHealthArea`, `playerHealthAreaBackground`), as are the party list and its background masks; such
pairs move together. `HudWidgets` lists the 16 elements by path.

* **Layout.** Per element an offset in parts of its parent's size (the UI box) and a scale factor, saved as
  `HudLayout = Id=x,y,scale;...`. `HudLayout.Apply` runs every frame (late) for every part: game position + offset ×
  parent size, game scale × factor, writing only what differs. Because the offset is relative to the box, a layout
  follows UI Area, HUD size and resolution.
* **The game's own moves.** The game moves some elements itself: money, crucible currency and durability jump between
  two positions when the equipment HUD switches between its gamepad and keyboard layouts (`PlayerEquipmentHUD`
  `kbmMoneyX` / `controllerMoneyX`..., `RefreshControlsLayout`: keyboard layout when the input is keyboard and mouse and
  "Show Controller HUD" is off), and `PlayerHUD.UpdateRealmDifficultyLayoutBasedOnTimeOfDay` moves the realm difficulty
  icon. The mod remembers what it wrote last; a different value is a move by the game and becomes the new game value,
  with the user's offset on top. The same goes for scale (`PlayerHUD.UpdateScale` applies the game's `UI_Scale*` fields).
* **Layout-driven elements.** The boss bar and plague meter (`bossStatsView/canvas/statsGroup`,
  `plagueMeter/canvas/statsGroup`) are placed by a `VerticalLayoutGroup` on their canvas; they move through its padding.
* **Boxing.** Some canvases have no UI box, so their elements sit at the screen edges outside the box:
  `PlayerActivities` (bounties and challenges), `playerNotifications` (hint bar, item pickups), `playerSignpostView`
  (area banner). `HudBoxing` adds a child `DUT_Box` with the game's own `UIAspectConstraint` (global mode) and moves
  those elements into it with their anchored values unchanged (the `parent` setter keeps the world position, so
  `anchoredPosition3D`, `sizeDelta`, scale and rotation are put back after it). The boss bar and plague meter canvases
  get the box margins added to their layout padding instead. Undone on unload.
* The live HUD is `PlayerUIService.Instance.PlayerHud` (`FindObjectOfType` misses it while a menu is open). A new HUD
  instance (session change) is boxed and bound again.

## HUD layout editor

* **Picking.** Every visible graphic of every element (enabled, not culled, colour alpha × inherited CanvasGroup alpha
  ≥ 0.05, not larger than 40% of the screen) is a hit area; the smallest one under the cursor wins, elements that draw
  nothing get a faint placeholder at their rect, and placeholders lose to visible art. Tab cycles through the elements
  under the cursor. The picked element's root canvas sorts 100 higher while picked.
* **Moving.** A screen-pixel delta is converted into the first part's parent space
  (`RectTransformUtility.ScreenPointToLocalPointInRectangle` with the canvas camera). The element's centre stays on
  screen, so it can always be grabbed again.
* **In game.** The editor runs over the settings screen it was opened from:
  * the player menu's foreground and background canvases are switched off (the menu stays open);
  * the settings screen hides the HUD every frame (`PlayerHUD.Hide` deactivates its parts): a prefix skips `Hide` while
    editing, `Show` is called on entry and `Hide` again on exit;
  * other menus fade the HUD through `PlayerUIService.PlayerHudSettings` CanvasGroups, and some disable root canvases:
    both are forced on after the service's late update;
  * gameplay input is blocked with the game's own `PlayerOverlay.BlockInputThisFrame()`, every frame;
  * Esc: `get_IgnoreBack` of `MainMenuSettingsScreen` and `SettingsScreenPlayerMenu` returns true while editing and
    for two frames after, so Esc closes only the editor;
  * the area banner's canvas is the HUD's only ScreenSpaceOverlay canvas, and overlays draw above every camera canvas
    whatever their order: while editing it draws through the HUD camera.
* **Sample content** (`HudSamples`), all recorded before it is changed and undone on exit:
  * CanvasGroups from each element up to the HUD forced to alpha 1; the boss bar and plague meter fade themselves in
    their own updates, so postfixes on `BossStatsView.UpdateHud` and `PlagueMeterHUD.Update` keep them up;
  * chat: our own lines in the chat's viewport. The chat history is never touched: its virtual list parks its rows
    off screen while the history is empty, and reading `ChatWindow.m_chatHistory` entries
    (`ValueTuple<float, ChatMessage>`) through interop crashed the game natively;
  * item pickups: copies of `PlayerNewItemsView.NewItemViewTemplate` with the Animator off and the visual moved in
    (its slide-in starts at x -513);
  * bounties, challenges, teammates: the game's own hidden row templates, shown with sample text (copies of the bounty
    row never drew); the teammate row's `PlayerTeammateUI` is switched off;
  * durability: the doll's images coloured as worn and broken (the game recolours them every frame, so this repeats).
* **Main menu.** No `PlayerHUD` exists there, but the `playerHUD` prefab is in memory (an asset: `scene.IsValid()` is
  false). `MenuHud` copies it under an inactive holder, so nothing wakes up, and while the copy's scripts are still
  there it uses their references: item pickups from the pickup template, the equipment layout the player uses (the
  live HUD's last one, else the game's rule above) with the money and durability positions that go with it. Then every
  script that is not plain UI is destroyed, the root canvases become overlays, nested canvases are removed (a nested
  canvas in the copy draws nothing), everything that is not an element is switched off, and the prefab's placeholder
  texts ("9999 / 9999", "300/900", "Heal hn") get sensible values. All other root canvases (the main menu) are
  switched off while editing.

## Hide HUD Outside Combat

`PlayerUIService.FadeOutHudSpecificElement(flags)` ORs `PlayerUiFadeFlags` into this frame's fade flags; the service's
late update fades the matching CanvasGroups out, and back in once the flags stop coming. The mod calls it from
`OnUpdate` (before that late update) with PlayerEquipment, PlayerMoney, PlayerHealth, TimeOfDay, PlayerLocation,
PlayerJournal and PlayerDurability once `PlayerControllerView.LocalPlayerInCombat` has been false for `HideHudDelay`
seconds. "On, but show health while hurt" leaves PlayerHealth out while `PlayerHealthUI.m_cachedCurrentHealth` is below
the maximum. Item pickups (ItemGetNotifications), hints and chat are never faded.

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
* `OnDeinitializeMelon` closes the HUD editor, restores the HUD layout and boxes, restores PanelSettings, removes the
  inline styles, unregisters and destroys the settings rows, restores the UI Aspect Mode description, and re-applies
  the constraints.
* `OnInitializeMelon` re-applies to what is on screen and adds the rows to the settings screens that already exist,
  because the game will not build them again until the next scene load. The HUD layout binds to the live HUD on the
  next frame.

## Development commands

With a file `UserData/DisplayUiTweaks/.dev`, commands in `UserData/DisplayUiTweaks/cmd.txt` (one per line, deleted
after reading): `edit` / `done` (HUD editor), `shot NAME` (screenshot), `layout` (bound elements and saved layout),
`vis [Id]` (an element's graphics), `pos` (money / durability / equipment positions), `pad on|off` (flip Show Controller
HUD in memory and re-evaluate the equipment layout), `menutest` / `menuoff` (show the main-menu HUD copy on top of the
game).
