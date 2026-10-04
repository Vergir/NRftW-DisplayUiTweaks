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
                    case "hud":
                        Prefs.HudScalePercent.Value = float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture);
                        DisplayUiTweaksMod.OnLayoutPrefChanged();
                        break;
                    case "find":
                        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                            if (go != null && go.name.StartsWith(a[1]) && go.scene.IsValid())
                                DisplayUiTweaksMod.Log.Msg($"  {go.name} activeSelf {go.activeSelf} inHierarchy {go.activeInHierarchy} scene {go.scene.name}");
                        break;
                    case "options":
                    {
                        // In game: open Options, or close the player menu.
                        var menu = UnityEngine.Object.FindObjectOfType<Il2Cpp.PlayerMenu>();
                        if (menu == null) break;
                        if (a.Length > 1 && a[1] == "close") menu.CloseMenu(Il2Cpp.PlayerMenuCloseBehaviour.UseClosedCallback);
                        else menu.OpenMenuSelectorScreen(Il2Cpp.PlayerMenuScreenType.Settings, true, false);
                        break;
                    }
                    case "select":
                    {
                        // Select one of our rows by name (as the mouse would), e.g. "select DUT_UiArea".
                        foreach (var row in UnityEngine.Object.FindObjectsOfType<Il2CppMoon.Forsaken.SettingsItemGUIBase>())
                            if (row != null && row.gameObject.name == a[1])
                            {
                                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(row.gameObject);
                                DisplayUiTweaksMod.Log.Msg("selected " + a[1]);
                                break;
                            }
                        break;
                    }
                    case "chatsize":
                        foreach (var w in Hud.HudLayout.Widgets)
                            if (w.Resizable)
                                Hud.HudLayout.ResizeTo(w, new Vector2(float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture),
                                                                      float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture)));
                        break;
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
                    case "chatinfo":
                    {
                        var h = Hud.HudLayout.LiveHud;
                        var c = h != null ? h.ChatWindow : null;
                        if (c == null) break;
                        var rt = c.GetComponent<RectTransform>();
                        var rows = c.m_messageQueue;
                        var row = rows != null && rows.Count > 0 ? rows[0] : null;
                        DisplayUiTweaksMod.Log.Msg($"  chat size {rt.sizeDelta} scale {rt.localScale} lossy {rt.lossyScale} rootScale {c.GetComponentInParent<Canvas>().rootCanvas.scaleFactor}" +
                            $" rowFont {(row != null && row.m_text != null ? row.m_text.fontSize + " auto " + row.m_text.enableAutoSizing + " lossy " + row.m_text.transform.lossyScale : "-")}");
                        var holder = c.transform.Find("history/DUT_ChatSamples") ?? c.transform.Find("history/scrollView/viewport/DUT_ChatSamples");
                        if (holder != null) DisplayUiTweaksMod.Log.Msg($"  holder local {holder.localPosition} world {holder.position} scale {holder.localScale} rot {holder.localRotation.eulerAngles}; chat world {rt.position} cam {c.GetComponentInParent<Canvas>().rootCanvas.worldCamera?.orthographic}");
                        foreach (var t in c.GetComponentsInChildren<Il2CppTMPro.TextMeshProUGUI>(false))
                            if (t.name == "line" && t.gameObject.activeInHierarchy)
                                DisplayUiTweaksMod.Log.Msg($"  z local {t.transform.localPosition.z} world {t.transform.position}; sample font {t.fontSize} lossy {t.transform.lossyScale} rect {t.rectTransform.rect} pos {t.rectTransform.anchoredPosition} bounds {t.bounds} text {t.textBounds.size} pref {t.preferredHeight} lines {t.textInfo?.lineCount} '{t.text[..System.Math.Min(20, t.text.Length)]}'");
                        break;
                    }
                    case "findtext":
                        foreach (var t in Resources.FindObjectsOfTypeAll<Il2CppTMPro.TMP_Text>())
                            if (t != null && t.text != null && t.text.Contains(a[1]) && t.gameObject.scene.IsValid())
                            {
                                string tpath = t.name;
                                for (var p = t.transform.parent; p != null; p = p.parent) tpath = p.name + "/" + tpath;
                                var cv = t.canvas;
                                DisplayUiTweaksMod.Log.Msg($"  {tpath} active {t.gameObject.activeInHierarchy} canvas {(cv != null ? cv.rootCanvas.name + "/" + cv.rootCanvas.renderMode + "/" + cv.rootCanvas.sortingOrder : "-")} lossy {t.transform.lossyScale}");
                            }
                        break;
                    case "chattest":
                    {
                        // A translucent red rectangle stretched over the chat window: does the window draw where its rect is?
                        var c = Hud.HudLayout.LiveHud?.ChatWindow;
                        if (c == null) break;
                        var old = c.transform.Find("DUT_Test");
                        if (old != null) { UnityEngine.Object.Destroy(old.gameObject); break; }
                        var go = new GameObject("DUT_Test");
                        var rt = go.AddComponent<RectTransform>();
                        rt.parent = c.transform;
                        go.layer = c.gameObject.layer;
                        rt.localScale = Vector3.one; rt.localRotation = Quaternion.identity;
                        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero; rt.anchoredPosition3D = Vector3.zero;
                        var img = go.AddComponent<UnityEngine.UI.Image>();
                        img.color = new Color(1f, 0f, 0f, 0.35f);
                        img.raycastTarget = false;
                        break;
                    }
                    case "chatcanvas":
                    {
                        var c = Hud.HudLayout.LiveHud?.ChatWindow;
                        var cv = c != null ? c.GetComponent<Canvas>() : null;
                        if (cv == null) break;
                        DisplayUiTweaksMod.Log.Msg($"  chatWindow canvas scaleFactor {cv.scaleFactor} root {cv.rootCanvas.scaleFactor} override {cv.overrideSorting} pixelPerfect {cv.pixelPerfect} renderMode {cv.renderMode} lossy {cv.transform.lossyScale} rootLossy {cv.rootCanvas.transform.lossyScale}");
                        if (a.Length > 1 && a[1] == "toggle") { cv.enabled = false; cv.enabled = true; }
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
