using System.Collections.Generic;
using Il2CppMoon.Forsaken;
using Il2CppTMPro;
using UnityEngine;

namespace DisplayUiTweaks.Hud;

/// <summary>
/// The HUD for the editor in the main menu, where no PlayerHUD exists: the playerHUD prefab is in memory there (an
/// asset, not a scene object). It is copied under an inactive holder, so nothing in it wakes up. While its scripts are
/// still there (but asleep) their references are used to set it up like the player's HUD: sample item pickups from the
/// pickup template, the equipment layout the player uses (gamepad or keyboard, and the money / durability positions
/// that go with it). Then every script that is not plain UI is destroyed, the canvases draw as overlays, everything
/// that is not one of the editor's elements is switched off, and the prefab's placeholder texts get sensible values.
/// See docs/internal.md, "HUD layout editor".
/// </summary>
internal static class MenuHud
{
    private const string HolderName = "DUT_MenuHud";
    private static GameObject? _holder;

    /// <summary>The equipment layout the live HUD showed last (null: none seen this session).</summary>
    public static bool? LastControllerLayout;

    private static readonly HashSet<string> KeepTypes = new()
    {
        "Transform", "RectTransform", "CanvasRenderer", "Canvas", "CanvasScaler", "CanvasGroup",
        "Image", "RawImage", "Text", "TextMeshProUGUI", "TMP_SubMeshUI", "Slider",
        "VerticalLayoutGroup", "HorizontalLayoutGroup", "GridLayoutGroup", "ContentSizeFitter", "LayoutElement",
        "Mask", "RectMask2D", "UIAspectConstraint", "AspectRatioFitter",
    };

    private static PlayerHUD? Prefab()
    {
        foreach (var h in Resources.FindObjectsOfTypeAll<PlayerHUD>())
            if (h != null && !h.gameObject.scene.IsValid()) return h;
        return null;
    }

    /// <summary>Builds the copy and returns its root (or null when the prefab is not in memory).</summary>
    public static Transform? Build()
    {
        Destroy();
        var src = Prefab();
        if (src == null) return null;
        _holder = new GameObject(HolderName);
        Object.DontDestroyOnLoad(_holder);
        _holder.SetActive(false); // nothing in the copy wakes up
        var copy = Object.Instantiate(src.gameObject, _holder.transform);
        var hud = copy.GetComponent<PlayerHUD>();
        if (hud != null)
        {
            try { PickupSamples(hud); } catch (System.Exception e) { DisplayUiTweaksMod.Log.Warning("Main menu HUD pickups: " + e.Message); }
            try { EquipmentLayout(hud); } catch (System.Exception e) { DisplayUiTweaksMod.Log.Warning("Main menu HUD equipment: " + e.Message); }
        }
        int removed = 0;
        for (int pass = 0; pass < 4; pass++)
        {
            bool any = false;
            foreach (var c in copy.GetComponentsInChildren<Component>(true))
            {
                if (c == null || KeepTypes.Contains(c.GetIl2CppType().Name)) continue;
                try { Object.DestroyImmediate(c); removed++; any = true; }
                catch { /* required by another component: next pass */ }
            }
            if (!any) break;
        }
        foreach (var cv in copy.GetComponentsInChildren<Canvas>(true))
        {
            if (cv.transform.parent != null && cv.transform.parent.GetComponentInParent<Canvas>(true) != null)
            {
                // A nested canvas in the copy draws nothing (the chat window's): its graphics draw through the root instead.
                Object.DestroyImmediate(cv);
                continue;
            }
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder += 20000;
        }
        DisplayUiTweaksMod.Log.Msg($"Main menu HUD: copied the HUD prefab, removed {removed} scripts");
        return copy.transform;
    }

    /// <summary>Three sample rows from the pickup template (a separate prefab the pickups view spawns from).</summary>
    private static void PickupSamples(PlayerHUD hud)
    {
        var view = hud.NewItemsView;
        if (view == null || view.NewItemViewTemplate == null || view.ItemsLayoutGroup == null) return;
        foreach (var name in new[] { "Sample Sword", "Iron Ore x3", "12 Silver" })
        {
            var go = Object.Instantiate(view.NewItemViewTemplate.gameObject, view.ItemsLayoutGroup.transform);
            go.name = "DUT_Sample";
            go.SetActive(true); // still asleep: the holder is inactive
            var item = go.GetComponent<PlayerNewItemView>();
            // The slide-in animation starts the visual off to the left: put it in place (the Animator goes with the scripts).
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var crt = go.transform.GetChild(i).TryCast<RectTransform>();
                if (crt != null) crt.anchoredPosition = new Vector2(0f, crt.anchoredPosition.y);
            }
            foreach (var cg in go.GetComponentsInChildren<CanvasGroup>(true)) cg.alpha = 1f;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.text = "";
            if (item != null)
            {
                if (item.ItemImage != null) item.ItemImage.color = new Color(1f, 1f, 1f, 0f);
                if (item.ItemNameText != null) item.ItemNameText.text = name;
                if (item.ItemTypeText != null) item.ItemTypeText.text = "Sample";
            }
        }
    }

    /// <summary>Gamepad or keyboard equipment layout, as PlayerEquipmentHUD.RefreshControlsLayout does it: keyboard layout
    /// when the input is keyboard and mouse and "Show Controller HUD" is off. Also moves money, crucible currency and
    /// durability to that layout's positions.</summary>
    private static void EquipmentLayout(PlayerHUD hud)
    {
        var eq = hud.playerEquipmentHUD;
        if (eq == null) return;
        bool controller;
        if (LastControllerLayout is bool seen) controller = seen;
        else
        {
            bool showControllerHud = false;
            try { showControllerHud = Core.SettingsData?.Account?.ShowControllerHUD ?? false; } catch { }
            var poller = Il2CppMoon.ARevisedInputManualPoller.Instance;
            bool keyboard = poller == null || poller.CurrentInputStyleDetected != CurrentInputStyle.Gamepad;
            controller = showControllerHud || !keyboard;
        }
        if (eq.m_controllerCanvasGroup != null) { eq.m_controllerCanvasGroup.gameObject.SetActive(true); eq.m_controllerCanvasGroup.alpha = controller ? 1f : 0f; }
        if (eq.m_kbmCanvasGroup != null) { eq.m_kbmCanvasGroup.gameObject.SetActive(true); eq.m_kbmCanvasGroup.alpha = controller ? 0f : 1f; }
        if (eq.m_pcOnlyElements != null) foreach (var go in eq.m_pcOnlyElements) if (go != null) go.SetActive(!controller);
        if (eq.m_gamepadOnlyElements != null) foreach (var go in eq.m_gamepadOnlyElements) if (go != null) go.SetActive(controller);
        if (!controller && eq.m_kbmCanvasGroup != null && eq.m_controllerCanvasGroup != null)
        {
            // Only one layout object is meant to be active.
            eq.m_controllerCanvasGroup.gameObject.SetActive(false);
        }
        else if (eq.m_kbmCanvasGroup != null) eq.m_kbmCanvasGroup.gameObject.SetActive(false);
        SetXY(eq.moneyTransform, controller ? eq.controllerMoneyX : eq.kbmMoneyX, controller ? eq.controllerMoneyY : eq.kbmMoneyY);
        SetXY(eq.crucibleMoneyTransform, controller ? eq.controllerCrucibleX : eq.kbmCrucibleX, controller ? eq.controllerCrucibleY : eq.kbmCrucibleY);
        SetXY(eq.durabilityTransform, controller ? eq.controllerDurabilityX : eq.kmbDurabilityX, controller ? eq.controllerDurabilityY : eq.kbmDurabilityY);
    }

    private static void SetXY(RectTransform? t, float x, float y)
    {
        if (t != null) t.anchoredPosition = new Vector2(x, y);
    }

    /// <summary>After HudLayout bound the copy: switch off everything that is not one of the widgets, add sample content, show.</summary>
    public static void Prepare(Transform root, List<Widget> widgets)
    {
        var parts = new HashSet<int>();
        var ancestors = new HashSet<int>();
        foreach (var w in widgets)
            foreach (var p in w.Parts)
            {
                if (p == null) continue;
                parts.Add(p.GetInstanceID());
                for (var t = p.parent; t != null; t = t.parent) ancestors.Add(t.GetInstanceID());
            }
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            int id = t.GetInstanceID();
            if (parts.Contains(id) || ancestors.Contains(id) || t.parent == null) continue;
            if (ancestors.Contains(t.parent.GetInstanceID())) t.gameObject.SetActive(false);
        }
        foreach (var w in widgets)
            if (w.Id != "Equipment")
                foreach (var p in w.Parts) if (p != null) p.gameObject.SetActive(true);
        Samples(root);
        if (_holder != null) _holder.SetActive(true);
    }

    private static void Text(Transform root, string path, string text)
    {
        var t = HudWidgets.Find(root, path);
        var tmp = t != null ? t.GetComponent<TMP_Text>() : null;
        if (tmp != null) tmp.text = text;
    }

    private static void Off(Transform root, string path)
    {
        var t = HudWidgets.Find(root, path);
        if (t != null) t.gameObject.SetActive(false);
    }

    private static void Samples(Transform root)
    {
        // The prefab's placeholders ("9999 / 9999", "300/900", "Heal hn"...) -> numbers like a real character's.
        const string frame = "playerHealthArea/aspectRatio/playerFrame";
        Text(root, frame + "/healthBarDriver/HealthNumber", "264/264");
        Text(root, frame + "/healthBarDriver/HealthNumberShadow", "264/264");
        Text(root, frame + "/container/focusBarDriver/focusCellsGroup/focusCell/FocusNumber", "120/185");
        Text(root, frame + "/container/focusBarDriver/focusCellsGroup/focusCell/FocusNumberShadow", "120/185");
        Text(root, frame + "/container/playerExperiencePoints/pointsText", "1051 / 6400");
        Off(root, frame + "/container/playerExperiencePoints/pointsText/maxlevelText");
        Off(root, frame + "/container/classContainer");
        Off(root, frame + "/healthBarDriver/reminderGroup");
        Off(root, "playerMoney/aspectRatio/layoutGroup/crucibleCurrency");
        Off(root, "playerChat/apectRatio/chatWindow/input"); // only shown while typing
        // Key labels of the keyboard layout come from the key bindings at run time.
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            if (t != null && t.text != null && t.text.Contains("NO MAPPING")) t.text = "";
        var hint = HudWidgets.Find(root, "playerNotifications/playerHint");
        if (hint != null) foreach (var t in hint.GetComponentsInChildren<TMP_Text>(true)) t.text = "Sample hint: hold to interact";

        var viewport = HudWidgets.Find(root, "playerChat/apectRatio/chatWindow/history/scrollView/viewport");
        if (viewport != null)
        {
            TMP_Text? style = null;
            foreach (var t in viewport.GetComponentsInChildren<TMP_Text>(true)) if (t != null && t.font != null) { style = t; break; }
            if (style == null) foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) if (t != null && t.font != null) { style = t; break; }
            var messages = viewport.Find("messages");
            if (messages != null) messages.gameObject.SetActive(false);
            if (style != null) HudSamples.ChatLinesInto(viewport, style);
        }
        // The chat's history background only shows while the chat is open: show it.
        var history = HudWidgets.Find(root, "playerChat/apectRatio/chatWindow/history");
        if (history != null) foreach (var g in history.GetComponentsInChildren<CanvasGroup>(true)) g.alpha = 1f;
        foreach (var (path, title, progress) in new[]
                 {
                     ("PlayerActivities/Content/BountyContainer", "Sample bounty: slay the Warden", "0 / 1"),
                     ("PlayerActivities/Content/ChallengesContainer", "Sample challenge: gather iron ore", "3 / 5"),
                 })
        {
            var container = HudWidgets.Find(root, path);
            if (container == null || container.childCount == 0) continue;
            var row = container.GetChild(0).gameObject;
            row.SetActive(true);
            foreach (var t in row.GetComponentsInChildren<TMP_Text>(true)) t.text = t.name.StartsWith("BountyProgress") ? progress : title;
        }
        var list = HudWidgets.Find(root, "partyUI/main/playerPartySidebar/apectRatio/playerList");
        if (list != null && list.childCount > 0)
        {
            var mate = list.GetChild(0).gameObject;
            mate.SetActive(true);
            var map = mate.transform.Find("portrait/mapPortrait");
            if (map != null) map.gameObject.SetActive(false);
            foreach (var t in mate.GetComponentsInChildren<TMP_Text>(true)) if (t.name == "playerName") t.text = "Ana";
        }
        foreach (var g in root.GetComponentsInChildren<CanvasGroup>(true))
            if (g.alpha < 0.01f && !IsEquipmentLayout(g.transform)) g.alpha = 1f;
    }

    /// <summary>The two equipment layouts are shown / hidden through their CanvasGroups: leave those as EquipmentLayout set them.</summary>
    private static bool IsEquipmentLayout(Transform t) => t.name is "playerEquipment" or "playerEquipmentPC" && t.parent != null && t.parent.name == "aspectRatio";

    public static bool Exists => _holder != null;

    /// <summary>Show / hide a built copy without rebuilding it (the settings preview).</summary>
    public static void SetVisible(bool visible)
    {
        if (_holder != null) _holder.SetActive(visible);
    }

    public static void Destroy()
    {
        if (_holder == null) { var old = GameObject.Find(HolderName); if (old != null) _holder = old; }
        if (_holder != null) Object.Destroy(_holder);
        _holder = null;
    }
}
