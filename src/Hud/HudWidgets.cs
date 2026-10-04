using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace DisplayUiTweaks.Hud;

/// <summary>
/// One movable HUD element: one or more RectTransforms that move together (the health bars and their background frame
/// live on two canvases). Offset and Scale are the user's layout; the Orig* values are the game's.
/// </summary>
internal sealed class Widget
{
    public string Id = "", Name = "";
    public readonly List<RectTransform> Parts = new();
    public readonly List<Vector2> OrigPos = new();
    public readonly List<Vector3> OrigScale = new();
    /// <summary>What HudLayout wrote last; a different value means the game placed the part itself (e.g. money and
    /// durability move with the gamepad / keyboard equipment layout): that becomes the new game value.</summary>
    public readonly List<Vector2?> LastPos = new();
    public readonly List<Vector3?> LastScale = new();
    public Canvas? Root;
    /// <summary>Positioned by a LayoutGroup on its parent (boss bar, plague meter): moved through the group's padding.</summary>
    public LayoutGroup? Group;
    public RectOffset? OrigPadding;
    /// <summary>The parts' own CanvasGroups are the game's switch between alternatives (Equipment: gamepad / keyboard
    /// layout): the editor never forces them visible.</summary>
    public bool KeepOwnGroups;

    /// <summary>The chat window: its size can be changed (sizeDelta), not only its scale.</summary>
    public bool Resizable;
    public readonly List<Vector2> OrigSize = new();

    /// <summary>Offset in parts of the parent's size (the UI box for almost all), scale factor, and for resizable
    /// elements a size factor per axis.</summary>
    public Vector2 Offset;
    public float Scale = 1f;
    public Vector2 Size = Vector2.one;

    public bool IsDefault => Offset.sqrMagnitude < 1e-10f && Mathf.Abs(Scale - 1f) < 1e-4f && (Size - Vector2.one).sqrMagnitude < 1e-8f;
    public bool Alive => Parts.Count > 0 && Parts[0] != null;
}

/// <summary>
/// The HUD elements the layout editor knows. PlayerHUD's children are mostly root canvases whose "aspectRatio" child
/// (UIAspectConstraint) is the UI box; the box's children are the elements, anchored to a corner of the box. The same
/// paths work on the main-menu copy of the HUD prefab (MenuHud). See docs/internal.md, "HUD layout".
/// </summary>
internal static class HudWidgets
{
    /// <summary>Id (saved in the preferences), name (shown in the editor), paths under PlayerHUD ("x/*" = all children of x).</summary>
    private static readonly (string id, string name, string[] paths)[] Table =
    {
        ("Health", "Health & stamina", new[] { "playerHealthArea/aspectRatio/playerFrame", "playerHealthAreaBackground/aspectRatio/playerFrame" }),
        ("Party", "Party", new[] { "partyUI/main/playerPartySidebar/apectRatio/playerList", "partyUI/main/playerPartySidebarBackground/aspectRatio/*" }),
        ("Equipment", "Equipment", new[] { "playerEquipment/aspectRatio/playerEquipment", "playerEquipment/aspectRatio/playerEquipmentPC" }),
        ("Money", "Money", new[] { "playerMoney/aspectRatio/money", "playerMoney/aspectRatio/layoutGroup" }),
        ("Durability", "Durability", new[] { "playerDurability/aspectRatio/GameObject" }),
        ("Clock", "Clock & weather", new[] { "playerTimeOfDay/Canvas/aspectRatio/GameObject" }),
        ("Realm", "Realm difficulty", new[] { "playerTimeOfDay/Canvas/aspectRatio/realmDifficulty" }),
        ("Location", "Location", new[] { "playerTimeOfDay/playerLocation/apectRatio/GameObject" }),
        ("Activities", "Bounties & challenges", new[] { "PlayerActivities/Content" }),
        ("Chat", "Chat", new[] { "playerChat/apectRatio/chatWindow" }),
        ("Loot", "Item pickups", new[] { "playerNotifications/newItemsView/grid" }),
        ("Hint", "Hint", new[] { "playerNotifications/playerHint" }),
        ("Boss", "Boss health", new[] { "bossStatsView/canvas/statsGroup" }),
        ("Plague", "Plague meter", new[] { "plagueMeter/canvas/statsGroup" }),
        ("Signpost", "Area banner", new[] { "playerSignpostView/background", "playerSignpostView/placesFields" }),
        ("Crucible", "Crucible floor", new[] { "cruciblePlayerHUD/canvasParent/FloorWidget" }),
    };

    private static readonly Il2CppStructArray<Vector3> Corners = new(4);

    /// <summary>Builds the widget list for a HUD root (PlayerHUD's transform or the main-menu copy).</summary>
    public static List<Widget> Discover(Transform root)
    {
        var list = new List<Widget>();
        foreach (var (id, name, paths) in Table)
        {
            var w = new Widget { Id = id, Name = name, KeepOwnGroups = id == "Equipment", Resizable = id == "Chat" };
            foreach (var path in paths)
            {
                if (path.EndsWith("/*"))
                {
                    var parent = Find(root, path[..^2]);
                    if (parent != null) for (int i = 0; i < parent.childCount; i++) AddPart(w, parent.GetChild(i));
                }
                else
                {
                    var t = Find(root, path);
                    if (t != null) AddPart(w, t);
                }
            }
            if (w.Parts.Count > 0) list.Add(w);
        }
        return list;
    }

    /// <summary>A path under the HUD; elements HudBoxing moved into a box are found with the box in between.</summary>
    public static Transform? Find(Transform root, string path)
    {
        var t = root.Find(path);
        if (t != null) return t;
        int slash = path.IndexOf('/');
        return slash > 0 ? root.Find(path[..slash] + "/" + HudBoxing.BoxName + path[slash..]) : null;
    }

    private static void AddPart(Widget w, Transform t)
    {
        var rt = t.TryCast<RectTransform>();
        if (rt == null) return;
        if (w.Root == null)
        {
            var c = rt.GetComponentInParent<Canvas>(true);
            w.Root = c != null ? c.rootCanvas : null;
        }
        w.Parts.Add(rt);
        w.OrigPos.Add(rt.anchoredPosition);
        w.OrigScale.Add(rt.localScale);
        w.LastPos.Add(null);
        w.LastScale.Add(null);
        w.OrigSize.Add(rt.sizeDelta);
        var group = rt.parent != null ? rt.parent.GetComponent<LayoutGroup>() : null;
        if (group != null && w.Group == null)
        {
            w.Group = group;
            var p = group.padding;
            w.OrigPadding = new RectOffset(p.left, p.right, p.top, p.bottom);
        }
    }

    public static Camera? Cam(Widget w) => w.Root != null && w.Root.renderMode != RenderMode.ScreenSpaceOverlay ? w.Root.worldCamera : null;

    /// <summary>Screen rect (bottom-left origin) of a RectTransform.</summary>
    public static Rect ScreenRect(RectTransform rt, Camera? cam)
    {
        rt.GetWorldCorners(Corners);
        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, Corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, Corners[2]);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    /// <summary>Screen rect of the parts' own RectTransforms (for elements that draw nothing right now).</summary>
    public static Rect PartsRect(Widget w)
    {
        var cam = Cam(w);
        bool any = false;
        Rect r = default;
        foreach (var p in w.Parts)
        {
            if (p == null) continue;
            var pr = ScreenRect(p, cam);
            r = any ? Rect.MinMaxRect(Mathf.Min(r.xMin, pr.xMin), Mathf.Min(r.yMin, pr.yMin), Mathf.Max(r.xMax, pr.xMax), Mathf.Max(r.yMax, pr.yMax)) : pr;
            any = true;
        }
        return r;
    }

    public static string Path(Transform t, Transform? root)
    {
        var s = t.name;
        for (var p = t.parent; p != null && p != root; p = p.parent) s = p.name + "/" + s;
        return s;
    }
}
