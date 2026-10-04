# Changelog

## 1.1.0

* **UI Area**: one slider (1.00-4.00, default 1.78) for the shape of the area the whole UI is kept in. It replaces the
  game's UI Aspect Mode (whose description now says so) and the 1.0.0 "Custom (Display & UI Tweaks)" mode and Custom UI
  Aspect Ratio row. A saved Custom setup is carried over once. The game's UI Aspect Mode option is back to the game's
  own behaviour (shown on widescreen monitors only).
* **Edit HUD Layout**: drag, resize and reset HUD elements on top of the game, with sample content in empty elements;
  also from the main menu. **Reset HUD Layout** puts everything back.
* **Hide HUD Outside Combat**: Off / On / On, but show health while hurt.
* HUD elements the game pins to the screen edges (bounties, item pickups, hint bar, boss bar, plague meter, area banner)
  are kept inside the UI area.
* The Bounty Board & Map Fix row is gone: the fix is always on (config switch `BoxUiToolkitScreens`).
* The rows show the current values whenever a settings screen opens (the main menu and the game have separate screens).

## 1.0.0

First public release.

* Every display resolution selectable in Options > Display; the game no longer replaces a saved resolution outside
  16:10-32:9 at launch. A saved resolution that differs from the window (seen on the Steam Deck) is applied.
* UI Aspect Mode always shown, with all modes plus "Custom (Display & UI Tweaks)"; the saved Custom mode survives restarts.
* No 16:9 letterbox on non-16:9 screens.
* Menus and UI Toolkit panels shrink to fit the UI box.
* Bounty and challenge boards and the map's detail bar kept inside the UI box (toggle).
* New rows in Options > Display: HUD & Dialogue UI Size, Menu UI Size, Custom UI Aspect Ratio,
  Bounty Board & Map Fix.
