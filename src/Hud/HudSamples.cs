using System.Collections.Generic;
using Il2CppMoon.Forsaken;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DisplayUiTweaks.Hud;

/// <summary>
/// Sample content while editing the live HUD, so elements that are empty or faded out can be seen while they are moved.
/// Everything is recorded before it is changed and undone in End(). See docs/internal.md, "HUD layout editor".
///  - CanvasGroups from each element up to the HUD forced to alpha 1, disabled root canvases enabled;
///  - chat: our own sample lines in the chat's viewport (the history is never touched: the virtual list parks its rows
///    off screen while it is empty, and reading history entries through interop crashed the game);
///  - hint text, boss name, a durability doll with worn pieces;
///  - item pickups: copies of the game's NewItemViewTemplate; bounties / challenges / teammates: the game's own hidden
///    row templates, shown with sample text.
/// </summary>
internal static class HudSamples
{
    private static readonly List<(CanvasGroup g, float alpha)> _groups = new();
    private static readonly List<GameObject> _spawned = new();
    private static readonly List<Canvas> _canvases = new();
    private static readonly List<GameObject> _activated = new();
    private static readonly List<GameObject> _deactivated = new();
    private static readonly List<(Image img, Color color)> _colors = new();
    private static readonly List<(Image img, Color color)> _durability = new();
    private static readonly List<CanvasGroup> _extraGroups = new();
    private static readonly List<CanvasGroup> _hiddenGroups = new();
    private static readonly List<Behaviour> _paused = new();
    private static readonly List<(Slider s, float value)> _sliders = new();
    private static readonly List<(TMP_Text t, string text)> _texts = new();
    private static bool _recording, _chatFaked;

    public static readonly string[] ChatLines =
    {
        "vergir died", "Ana joined the game", "Ana: anyone up for the crucible?", "vergir: sure, give me a minute",
        "Bram died", "Bram: that boss hits hard", "Ana left the game", "vergir: sample line for the HUD editor",
    };

    /// <summary>Note the original state of everything ForceVisible touches, then place the samples.</summary>
    public static void Begin(PlayerHUD hud)
    {
        _recording = true;
        try { ForceVisible(hud); }
        finally { _recording = false; }
        Chat(hud);
        Hint(hud);
        Loot(hud);
        BossName(hud);
        Durability(hud);
        Activities(hud);
        Party(hud);
        if (hud.ActivitiesHUD != null) Rebuild(hud.ActivitiesHUD.contentGroup);
    }

    private static void Force(CanvasGroup? g, float alpha = 1f)
    {
        if (g == null) return;
        if (!_groups.Exists(x => x.g == g)) _groups.Add((g, g.alpha));
        if (!_recording && Mathf.Abs(g.alpha - alpha) >= 0.001f) g.alpha = alpha;
    }

    private static void Activate(GameObject? go)
    {
        if (go == null || go.activeSelf) return;
        if (!_activated.Contains(go)) _activated.Add(go);
        if (!_recording) go.SetActive(true);
    }

    /// <summary>Every frame while editing, after the game's own fades.</summary>
    public static void ForceVisible(PlayerHUD hud)
    {
        foreach (var w in HudLayout.Widgets)
            foreach (var p in w.Parts)
                for (var t = p != null ? p.transform : null; t != null && t != hud.transform; t = t.parent)
                {
                    var c = t.GetComponent<Canvas>();
                    if (c != null && !c.enabled && !_recording) { c.enabled = true; if (!_canvases.Contains(c)) _canvases.Add(c); }
                    if (w.KeepOwnGroups && t == p!.transform) continue;
                    Force(t.GetComponent<CanvasGroup>());
                }
        var chat = hud.ChatWindow;
        if (chat != null)
        {
            if (chat.m_chatHistoryCanvasGroup != null) { Activate(chat.m_chatHistoryCanvasGroup.gameObject); Force(chat.m_chatHistoryCanvasGroup); }
            Force(chat.m_historyBackgroundCanvasGroup, chat.m_historyBackgroundOpacity);
        }
        if (hud.Hint != null) Force(hud.Hint.ContentCanvasGroup);
        foreach (var g in _extraGroups) Force(g);
        foreach (var g in _hiddenGroups) Force(g, 0f);
        if (_recording) return;
        // The game recolours the durability doll every frame from the real durability.
        foreach (var (img, col) in _durability) if (img != null) img.color = col;
    }

    public static void End(PlayerHUD? hud)
    {
        var chat = hud != null ? hud.ChatWindow : null;
        if (chat != null && _chatFaked) chat.RefreshData(-1, true);
        _chatFaked = false;
        foreach (var (g, a) in _groups) if (g != null) g.alpha = a;
        _groups.Clear();
        foreach (var c in _canvases) if (c != null) c.enabled = false;
        _canvases.Clear();
        foreach (var go in _activated) if (go != null) go.SetActive(false);
        _activated.Clear();
        foreach (var go in _deactivated) if (go != null) go.SetActive(true);
        _deactivated.Clear();
        foreach (var (img, col) in _colors) if (img != null) img.color = col;
        _colors.Clear();
        _durability.Clear();
        _extraGroups.Clear();
        _hiddenGroups.Clear();
        foreach (var b in _paused) if (b != null) b.enabled = true;
        _paused.Clear();
        foreach (var (sl, v) in _sliders) if (sl != null) sl.value = v;
        _sliders.Clear();
        foreach (var go in _spawned) if (go != null) Object.Destroy(go);
        _spawned.Clear();
        foreach (var (t, text) in _texts) if (t != null) t.text = text;
        _texts.Clear();
    }

    /// <summary>Sample chat lines inside a chat viewport, styled like the game's rows, stacked from the bottom.
    /// Also used for the main-menu copy of the HUD.</summary>
    public static GameObject? ChatLinesInto(Transform viewport, TMP_Text style)
    {
        float line = style.fontSize * 1.35f;
        var holder = new GameObject("DUT_ChatSamples");
        var hrt = holder.AddComponent<RectTransform>();
        hrt.parent = viewport;
        holder.layer = viewport.gameObject.layer;
        hrt.localScale = Vector3.one;
        hrt.localRotation = Quaternion.identity;
        hrt.anchorMin = Vector2.zero; hrt.anchorMax = Vector2.one; hrt.sizeDelta = Vector2.zero; hrt.anchoredPosition3D = Vector3.zero;
        for (int i = 0; i < ChatLines.Length; i++)
        {
            string text = ChatLines[ChatLines.Length - 1 - i]; // bottom up
            int colon = text.IndexOf(": ");
            var go = new GameObject("line");
            var rt = go.AddComponent<RectTransform>();
            rt.parent = hrt;
            go.layer = holder.layer;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, line);
            rt.anchoredPosition3D = new Vector3(0f, 4f + i * line, 0f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = style.font;
            t.fontSharedMaterial = style.fontSharedMaterial;
            t.fontSize = style.fontSize;
            t.color = style.color;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.text = colon > 0 ? $"<color=#9DC3E6>{text[..colon]}</color>: {text[(colon + 2)..]}" : text;
        }
        return holder;
    }

    private static void Chat(PlayerHUD hud)
    {
        var c = hud.ChatWindow;
        var rows = c != null ? c.m_messageQueue : null;
        var viewport = c != null && c.m_messageParent != null ? c.m_messageParent.parent : null;
        if (rows == null || rows.Count == 0 || viewport == null || rows[0].m_text == null) return;
        // The game's rows (real lines) step aside while ours show.
        var parent = c!.m_messageParent.GetComponent<CanvasGroup>() ?? c.m_messageParent.gameObject.AddComponent<CanvasGroup>();
        Force(parent, 0f);
        _hiddenGroups.Add(parent);
        var holder = ChatLinesInto(viewport, rows[0].m_text);
        if (holder != null) _spawned.Add(holder);
        _chatFaked = true;
    }

    private static void Hint(PlayerHUD hud)
    {
        var h = hud.Hint;
        if (h == null || h.Text == null) return;
        _texts.Add((h.Text, h.Text.text));
        h.Text.text = "Sample hint: hold to interact";
    }

    private static void Loot(PlayerHUD hud)
    {
        var v = hud.NewItemsView;
        if (v == null || v.NewItemViewTemplate == null || v.ItemsLayoutGroup == null) return;
        if (v.ItemsLayoutGroup.transform.childCount > 0) return; // real pickups showing
        foreach (var name in new[] { "Sample Sword", "Iron Ore x3", "12 Silver" })
        {
            var go = Object.Instantiate(v.NewItemViewTemplate.gameObject);
            go.transform.parent = v.ItemsLayoutGroup.transform;
            go.transform.localScale = Vector3.one;
            go.transform.localPosition = Vector3.zero;
            go.name = "DUT_Sample";
            go.SetActive(true);
            // The slide-in animation starts the visual off to the left (x -513): stop it and put the visual in place.
            var view = go.GetComponent<PlayerNewItemView>();
            if (view != null && view.ItemAnimator != null) view.ItemAnimator.enabled = false;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                var crt = go.transform.GetChild(i).TryCast<RectTransform>();
                if (crt != null) crt.anchoredPosition = new Vector2(0f, crt.anchoredPosition.y);
            }
            foreach (var cg in go.GetComponentsInChildren<CanvasGroup>(true)) cg.alpha = 1f;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.text = "";
            if (view != null)
            {
                if (view.ItemImage != null)
                {
                    var icon = SampleIcon(hud);
                    if (icon != null) view.ItemImage.sprite = icon; else view.ItemImage.color = new Color(1f, 1f, 1f, 0f);
                }
                if (view.ItemNameText != null) view.ItemNameText.text = name;
                if (view.ItemTypeText != null) view.ItemTypeText.text = "Sample";
            }
            _spawned.Add(go);
        }
    }

    /// <summary>An item icon for the sample pickups: the first item sprite shown in the equipment slots.</summary>
    private static Sprite? SampleIcon(PlayerHUD hud)
    {
        var eq = hud.playerEquipmentHUD;
        if (eq == null) return null;
        foreach (var img in eq.GetComponentsInChildren<Image>(false))
            if (img != null && img.sprite != null && img.GetComponentInParent<ItemSlotHUD>() != null && img.rectTransform.rect.width > 40f
                && img.sprite.name.IndexOf("slot", System.StringComparison.OrdinalIgnoreCase) < 0
                && img.sprite.name.IndexOf("frame", System.StringComparison.OrdinalIgnoreCase) < 0)
                return img.sprite;
        return null;
    }

    /// <summary>The durability paper doll: one image per gear slot, coloured in only when that item is worn down.</summary>
    private static void Durability(PlayerHUD hud)
    {
        var d = hud.PlayerDurability;
        if (d == null) return;
        int i = 0;
        foreach (var img in d.GetComponentsInChildren<Image>(true))
        {
            if (img == null || !img.name.StartsWith("playerDurability")) continue;
            _colors.Add((img, img.color));
            var col = (i++ % 3) switch { 0 => new Color(0.95f, 0.75f, 0.2f, 1f), 1 => new Color(0.85f, 0.2f, 0.15f, 1f), _ => new Color(0.6f, 0.6f, 0.6f, 1f) };
            img.color = col;
            _durability.Add((img, col));
        }
    }

    /// <summary>Bounties / challenges: the game's own hidden row templates with sample text (copies of them never drew).</summary>
    private static void Activities(PlayerHUD hud)
    {
        var a = hud.ActivitiesHUD;
        if (a == null) return;
        // The panel's Animator / CanvasController hide it again every frame while there is nothing to show.
        if (a.contentGroup != null)
            foreach (var b in a.contentGroup.GetComponents<Behaviour>())
            {
                string n = b.GetIl2CppType().Name;
                if ((n == "Animator" || n == "CanvasController") && b.enabled) { b.enabled = false; _paused.Add(b); }
            }
        foreach (var (container, title, progress) in new[] { (a.bountiesContainer, "Sample bounty: slay the Warden", "0 / 1"), (a.challengesContainer, "Sample challenge: gather iron ore", "3 / 5") })
        {
            if (container == null) continue;
            var cg = container.GetComponent<CanvasGroup>(); // hidden while empty
            if (cg != null) { Force(cg); _extraGroups.Add(cg); }
            bool anyActive = false;
            for (int i = 0; i < container.childCount; i++) if (container.GetChild(i).gameObject.activeSelf) anyActive = true;
            if (anyActive || container.childCount == 0) continue;
            var go = container.GetChild(0).gameObject;
            Activate(go);
            foreach (var anim in go.GetComponentsInChildren<Animator>(true)) if (anim.enabled) { anim.enabled = false; _paused.Add(anim); }
            foreach (var g in go.GetComponentsInChildren<CanvasGroup>(true)) { Force(g); _extraGroups.Add(g); }
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
            {
                _texts.Add((t, t.text));
                t.text = t.name.StartsWith("BountyProgress") ? progress : title;
            }
            Rebuild(container);
        }
    }

    private static void Rebuild(Transform? t)
    {
        var rt = t != null ? t.TryCast<RectTransform>() : null;
        if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    /// <summary>Two sample teammates from the hidden PlayerTeammateUI row (its script off: it would look for a real
    /// player). Their 3D portraits cannot be faked.</summary>
    private static void Party(PlayerHUD hud)
    {
        var team = hud.TeamUI;
        var list = team != null ? team.transform.Find("apectRatio/playerList") : null;
        if (list == null || list.childCount == 0) return;
        for (int i = 0; i < list.childCount; i++) if (list.GetChild(i).gameObject.activeSelf) return; // real teammates showing
        var template = list.GetChild(0).gameObject;
        string[] names = { "Ana", "Bram" };
        float[] health = { 0.8f, 0.35f };
        for (int n = 0; n < names.Length; n++)
        {
            GameObject go;
            if (n == 0)
            {
                go = template;
                var ui = go.GetComponent<PlayerTeammateUI>();
                if (ui != null && ui.enabled) { ui.enabled = false; _paused.Add(ui); }
                Activate(go);
            }
            else
            {
                go = Object.Instantiate(template);
                var ui = go.GetComponent<PlayerTeammateUI>();
                if (ui != null) Object.DestroyImmediate(ui);
                go.transform.parent = list;
                go.transform.localScale = Vector3.one;
                go.transform.localPosition = Vector3.zero;
                go.name = "DUT_Sample";
                go.SetActive(true);
                _spawned.Add(go);
            }
            var map = go.transform.Find("portrait/mapPortrait");
            if (map != null && map.gameObject.activeSelf) { map.gameObject.SetActive(false); if (n == 0) _deactivated.Add(map.gameObject); }
            foreach (var g in go.GetComponentsInChildren<CanvasGroup>(true)) { Force(g); _extraGroups.Add(g); }
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                if (t.name == "playerName") { _texts.Add((t, t.text)); t.text = names[n]; }
            foreach (var s in go.GetComponentsInChildren<Slider>(true))
                if (s.name == "healthbar") { if (n == 0) _sliders.Add((s, s.value)); s.normalizedValue = health[n]; }
        }
        Rebuild(list);
    }

    private static void BossName(PlayerHUD hud)
    {
        var view = hud.BossStatsView;
        if (view == null) return;
        foreach (var t in view.GetComponentsInChildren<TMP_Text>(true))
            if (string.IsNullOrWhiteSpace(t.text)) { _texts.Add((t, t.text)); t.text = "Sample Boss"; }
    }
}
