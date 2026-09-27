# Hot reload: lessons from Display & UI Tweaks

Problems met while making a real mod (Display & UI Tweaks for No Rest for the Wicked, MelonLoader 0.7.3, IL2CPP)
reload cleanly, and how each was fixed. Written as an example for the HotReload README.

## 1. UI you add to a game screen disappears after a reload and does not come back

**Symptom.** After a reload, the mod's rows in the game's settings screen were gone. Reopening the menu, and even
quitting to the main menu, did not bring them back; only a game restart did.

**Cause.** The mod added its rows in a postfix on the game's "build settings tab" method. The game builds its settings
screens once, when a scene loads, and reuses them; reopening the menu does not rebuild them. The old build removed its
rows on unload (correctly), but the new build never saw the build event, so it never added its own.

**Fix.** In `OnInitializeMelon`, find the screens that already exist (`Resources.FindObjectsOfTypeAll<SettingsScreen>()`)
and add the rows to them directly, as well as in the postfix. Rule: *anything you attach to game objects from an event
must also be attachable on demand, because after a reload the event has already happened.*

## 2. Do not track your own UI in static fields

**Symptom.** Rows appeared twice, or a new row failed with a duplicate-key exception that left an empty, unlabelled row
behind.

**Cause.** The mod remembered its rows in a static list. Static state starts empty in the new build, so it thought no
rows existed. The game also keys settings rows by label id in a dictionary; adding the same id again throws *after* the
row object was created.

**Fix.** Find your own objects in the scene instead of remembering them: give them a recognisable GameObject name
(`DUT_*`) and look them up by name. Before adding, remove your ids from the game's registry.

## 3. Destroyed objects left in game collections break unrelated features (Esc / B stopped working)

**Symptom.** After a reload, Back (Esc on keyboard, B on a controller) did nothing in the settings screen.

**Cause.** On unload the mod destroyed its dropdown row, but the game also kept that row in its own list of dropdowns.
On Back, the game asks every dropdown in that list whether it is open; the destroyed row threw a
`NullReferenceException` and the Back action never ran. The error was in the game's `Player.log`, not in the MelonLoader
log (Unity exceptions from game code go to `Player.log`).

**Fix.** Before destroying an object you handed to the game, remove it from every game collection and cached reference
that can hold it (here: the dropdown lists and the "selected" / "previous" item fields). On load, also drop dead entries
an older build may have left behind, so a broken session repairs itself on the next reload. Rule: *if the game can reach
your object, unregister it before you destroy it.*

## 4. Scene replay multiplies the cost of `OnSceneWasLoaded`

**Symptom.** A reload took 16.5 s.

**Cause.** HotReload replays `OnSceneWasLoaded` for every open scene (95 in this game, which streams its world as
additive scenes). The mod ran a full re-apply, including several `Resources.FindObjectsOfTypeAll` calls, on each one.
The same cost also hit normal play whenever the world streamed in.

**Fix.** `OnSceneWasLoaded` only schedules the work; `OnUpdate` runs it once, 0.5 s after the last scene event.
Reload time dropped to 0.9 s. Rule: *keep scene callbacks cheap or debounce them; HotReload will call them many times
in a row.*

## 5. Game state changed outside Harmony survives the reload

**Symptom.** After a reload, the old build's changes were still applied on top of the new build's.

**Cause.** Unpatching Harmony does not undo what the mod did directly to game objects: `PanelSettings` scale modes,
inline UI Toolkit styles, `localScale` on screens, the rows mentioned above.

**Fix.** `OnDeinitializeMelon` restores each of them (the mod keeps the original values while it runs), and
`OnInitializeMelon` re-applies everything to what is on screen, because scene events that set things up originally will
not fire again. Some changes are harmless to leave (a render pipeline flag, two static debug switches the new build sets
to the same value); document which ones you decided to leave.

## 6. Testing the new unload code needs two reloads

The first reload into a fixed build runs the *old* build's `OnDeinitializeMelon`. Only the second reload exercises the
new unload code. Rebuilding without code changes produces an identical DLL, which HotReload skips; to force a reload of
the same code, give the build a unique version stamp:

```
dotnet build -c Release -p:InformationalVersion=1.0.0+reload%TIME%
```

(or any value that changes the assembly bytes).

## 7. Some code only runs at game start

Anything that runs once during boot (here: building the resolution list and applying the saved resolution, reading the
saved UI aspect mode) is not re-run by a reload. Changes there still need a game restart to test. Keep such code small
and separate so the rest of the mod stays hot-reloadable.

## 8. Deploying while the game runs

MelonLoader loads `Mods/*.dll` from the original file and keeps it open, so `dotnet build`'s copy step failed while the
game ran. HotReload's shadow copying (`ShadowCopyMods`) solves this for `Mods/`; for `Plugins/` and `UserLibs/`, use the
rename-then-copy deploy target from the HotReload README. Until then, `ContinueOnError="true"` on the copy task at
least keeps the build from failing.

## Checklist

* Patch only through the melon's `HarmonyInstance`.
* Attach UI to existing game objects both from events and on demand in `OnInitializeMelon`.
* Find your own objects by name, not through static fields.
* Unregister your objects from game collections before destroying them; clean up dead references on load.
* Undo non-Harmony changes in `OnDeinitializeMelon`; re-apply in `OnInitializeMelon`.
* Keep `OnSceneWasLoaded` cheap or debounced.
* Look in the game's `Player.log` for exceptions that the MelonLoader log does not show.
* Reload twice to test unload code; stamp the build so identical code still reloads.
