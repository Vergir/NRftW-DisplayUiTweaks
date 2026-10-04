using System;
using System.IO;
using MelonLoader.Utils;
using UnityEngine;

namespace DisplayUiTweaks;

/// <summary>
/// Development only: active while the file UserData/DisplayUiTweaks/.dev exists. Commands in
/// UserData/DisplayUiTweaks/cmd.txt, one per line, deleted after reading:
///   edit | done          enter / leave Edit HUD Layout
///   shot NAME            screenshot to UserData/DisplayUiTweaks/NAME.png
///   layout               log the bound HUD elements and the saved layout
/// </summary>
internal static class DevCommands
{
    private static float _next;
    private static string Dir => Path.Combine(MelonEnvironment.UserDataDirectory, "DisplayUiTweaks");

    public static void Tick()
    {
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 0.5f;
        if (!File.Exists(Path.Combine(Dir, ".dev"))) return;
        string path = Path.Combine(Dir, "cmd.txt");
        if (!File.Exists(path)) return;
        string[] lines;
        try { lines = File.ReadAllLines(path); File.Delete(path); }
        catch (IOException) { return; }
        foreach (var raw in lines)
        {
            var a = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 0) continue;
            DisplayUiTweaksMod.Log.Msg("cmd " + raw.Trim());
            try
            {
                switch (a[0])
                {
                    case "edit": Hud.HudEditor.Enter(); break;
                    case "done": Hud.HudEditor.Exit(); break;
                    case "shot": ScreenCapture.CaptureScreenshot(Path.Combine(Dir, (a.Length > 1 ? a[1] : "shot") + ".png")); break;
                    case "layout":
                        DisplayUiTweaksMod.Log.Msg($"root '{Hud.HudLayout.Root?.name}', saved '{Prefs.HudLayout.Value}', UI Area {Prefs.UiAreaAspect}");
                        foreach (var w in Hud.HudLayout.Widgets)
                            DisplayUiTweaksMod.Log.Msg($"  {w.Id}: parts {w.Parts.Count} offset {w.Offset} scale {w.Scale} group {(w.Group != null ? w.Group.name : "-")}");
                        break;
                    case "menutest":
                    {
                        // The main-menu copy of the HUD, shown on top of whatever is on screen (no editor, no binding).
                        var copy = Hud.MenuHud.Build();
                        if (copy == null) { DisplayUiTweaksMod.Log.Msg("no HUD prefab"); break; }
                        Hud.HudBoxing.Apply(copy);
                        var widgets = Hud.HudWidgets.Discover(copy);
                        Hud.MenuHud.Prepare(copy, widgets);
                        var chat = Hud.HudWidgets.Find(copy, "playerChat/apectRatio/chatWindow");
                        for (var t = chat; t != null && t != copy; t = t.parent)
                        {
                            var cv = t.GetComponent<Canvas>();
                            var cg = t.GetComponent<CanvasGroup>();
                            DisplayUiTweaksMod.Log.Msg($"  {t.name}: activeSelf {t.gameObject.activeSelf} scale {t.localScale} pos {t.localPosition} layer {t.gameObject.layer}" +
                                (cv != null ? $" CANVAS en {cv.enabled} mode {cv.renderMode} root {cv.isRootCanvas} order {cv.sortingOrder} layerName {cv.sortingLayerName} override {cv.overrideSorting} scaleFactor {cv.scaleFactor} display {cv.targetDisplay}" : "") +
                                (cg != null ? $" GROUP a {cg.alpha}" : ""));
                        }
                        if (chat != null)
                            foreach (var g in chat.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                                DisplayUiTweaksMod.Log.Msg($"    {Hud.HudWidgets.Path(g.transform, copy)} act {g.gameObject.activeInHierarchy} en {g.enabled} a {g.color.a:0.##} " +
                                                           $"inh {g.canvasRenderer.GetInheritedAlpha():0.##} canvas {(g.canvas != null ? g.canvas.name + "/" + g.canvas.renderMode + "/" + g.canvas.sortingOrder + "/" + g.canvas.isActiveAndEnabled : "-")} rect {Hud.HudWidgets.ScreenRect(g.rectTransform, null)}");
                        break;
                    }
                    case "menuoff": Hud.MenuHud.Destroy(); break;
                    case "pad":
                    {
                        // Flip Show Controller HUD in memory and let the equipment HUD re-evaluate its layout.
                        var acc = Il2CppMoon.Forsaken.Core.SettingsData?.Account;
                        var hud = Hud.HudLayout.LiveHud;
                        if (acc == null || hud == null) break;
                        acc.ShowControllerHUD = a.Length > 1 && a[1] == "on";
                        hud.playerEquipmentHUD?.ForceFullInputHUDReevaluation();
                        DisplayUiTweaksMod.Log.Msg($"ShowControllerHUD {acc.ShowControllerHUD}");
                        break;
                    }
                    case "pos":
                        foreach (var w in Hud.HudLayout.Widgets)
                            if (w.Id is "Money" or "Durability" or "Equipment")
                                for (int i = 0; i < w.Parts.Count; i++)
                                    if (w.Parts[i] != null)
                                        DisplayUiTweaksMod.Log.Msg($"  {w.Id}[{w.Parts[i].name}] pos {w.Parts[i].anchoredPosition} game {w.OrigPos[i]} offset {w.Offset} active {w.Parts[i].gameObject.activeInHierarchy}");
                        break;
                    case "vis":
                        // Graphics of one element of the bound HUD: active, enabled, alpha, inherited alpha, screen rect.
                        foreach (var w in Hud.HudLayout.Widgets)
                        {
                            if (a.Length > 1 && !w.Id.Equals(a[1], StringComparison.OrdinalIgnoreCase)) continue;
                            var cam = Hud.HudWidgets.Cam(w);
                            foreach (var p in w.Parts)
                            {
                                if (p == null) continue;
                                DisplayUiTweaksMod.Log.Msg($"  {w.Id} part {Hud.HudWidgets.Path(p, Hud.HudLayout.Root)} active {p.gameObject.activeInHierarchy}");
                                foreach (var g in p.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                                    DisplayUiTweaksMod.Log.Msg($"    {g.name} ({g.GetIl2CppType().Name}) act {g.gameObject.activeInHierarchy} en {g.enabled} a {g.color.a:0.##} " +
                                                               $"inh {g.canvasRenderer.GetInheritedAlpha():0.##} cull {g.canvasRenderer.cull} canvas {(g.canvas != null ? g.canvas.name + "/" + g.canvas.renderMode + "/" + g.canvas.sortingOrder : "-")} rect {Hud.HudWidgets.ScreenRect(g.rectTransform, cam)}");
                            }
                        }
                        break;
                    default: DisplayUiTweaksMod.Log.Warning("unknown command " + a[0]); break;
                }
            }
            catch (Exception e) { DisplayUiTweaksMod.Log.Warning($"cmd '{raw}' failed: {e}"); }
        }
    }
}
