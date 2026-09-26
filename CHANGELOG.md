# Changelog

## 1.0.1

* Fix: after a hot reload, Back (Esc / B) stopped working in the settings screen. The unloaded build destroyed its
  dropdown row but left it in the settings screen's dropdown list, so Back hit a destroyed object. Rows are now
  unregistered before they are destroyed, and stale references left by older builds are cleaned up.

## 1.0.0

First public release.

* Every display resolution selectable in Options > Display; the game no longer replaces a saved resolution outside
  16:10-32:9 at launch. A saved resolution that differs from the window (seen on the Steam Deck) is applied.
* UI Aspect Mode always shown, with all modes plus "Custom (More Aspect Ratios)"; the saved Custom mode survives restarts.
* No 16:9 letterbox on non-16:9 screens.
* Menus and UI Toolkit panels shrink to fit the UI box.
* Bounty and challenge boards and the map's detail bar kept inside the UI box (toggle).
* New rows in Options > Display: HUD & Dialogue UI Size, Menu UI Size, Custom UI Aspect Ratio,
  Box Bounty Boards & Map Details.
* MelonLoader HotReload support.
