using System.Collections.Generic;
using Il2CppMoon.Forsaken;
using UnityEngine;

namespace DisplayUiTweaks.Hud;

/// <summary>
/// Some HUD canvases have no UI box: their elements are anchored to the screen edges, so on screens wider or taller
/// than the UI box the bounties, item pickups, hint bar and area banner sit outside it (and slide in from the screen
/// edge). This puts them into a box: a child "DUT_Box" with the game's own UIAspectConstraint (global mode), and the
/// elements moved into it with their anchored values unchanged. The boss bar and plague meter canvases arrange their
/// element with a LayoutGroup instead; HudLayout insets their padding by the same margins.
/// </summary>
internal static class HudBoxing
{
    public const string BoxName = "DUT_Box";

    /// <summary>Canvas path under PlayerHUD, children to move into the box.</summary>
    private static readonly (string canvas, string[] children)[] Specs =
    {
        ("PlayerActivities", new[] { "Content" }),
        ("playerNotifications", new[] { "playerHint", "newItemsView" }),
        ("playerSignpostView", new[] { "background", "placesFields", "hintTimeline" }),
    };

    private sealed class Moved
    {
        public RectTransform Child = null!;
        public Transform Canvas = null!;
        public int Sibling;
    }

    private static readonly List<Moved> _moved = new();
    private static readonly List<GameObject> _boxes = new();

    public static void Apply(Transform hudRoot)
    {
        if (!Prefs.BoxHudWidgets.Value) return;
        foreach (var (canvasPath, children) in Specs)
        {
            var canvas = hudRoot.Find(canvasPath);
            if (canvas == null || canvas.Find(BoxName) != null) continue;
            var found = new List<RectTransform>();
            foreach (var name in children)
            {
                var rt = canvas.Find(name)?.TryCast<RectTransform>();
                if (rt != null) found.Add(rt);
            }
            if (found.Count == 0) continue;
            found.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));

            var box = new GameObject(BoxName);
            var brt = box.AddComponent<RectTransform>();
            brt.parent = canvas;               // SetParent(Transform, bool) is stripped in this game
            box.layer = canvas.gameObject.layer;
            brt.localScale = Vector3.one;
            brt.localRotation = Quaternion.identity;
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition3D = Vector3.zero; brt.sizeDelta = Vector2.zero;
            brt.SetSiblingIndex(found[0].GetSiblingIndex());
            var constraint = box.AddComponent<UIAspectConstraint>();
            constraint.m_useGlobalMode = true;
            constraint.m_rectTransform = brt;
            _boxes.Add(box);

            foreach (var child in found)
            {
                var m = new Moved { Child = child, Canvas = canvas, Sibling = child.GetSiblingIndex() };
                Reparent(child, brt);
                _moved.Add(m);
            }
            UiBox.ApplyConstraint(constraint);
        }
    }

    /// <summary>The parent setter keeps the world position: put the anchored values back so anchors do the placing.</summary>
    private static void Reparent(RectTransform child, Transform parent)
    {
        var pos = child.anchoredPosition3D;
        var size = child.sizeDelta;
        var scale = child.localScale;
        var rot = child.localRotation;
        child.parent = parent;
        child.anchoredPosition3D = pos;
        child.sizeDelta = size;
        child.localScale = scale;
        child.localRotation = rot;
    }

    /// <summary>Unload / hot reload: elements back to their canvases, boxes destroyed.</summary>
    public static void RestoreAll()
    {
        for (int i = _moved.Count - 1; i >= 0; i--)
        {
            var m = _moved[i];
            if (m.Child == null || m.Canvas == null) continue;
            Reparent(m.Child, m.Canvas);
            m.Child.SetSiblingIndex(Mathf.Min(m.Sibling, m.Canvas.childCount - 1));
        }
        _moved.Clear();
        foreach (var b in _boxes) if (b != null) Object.Destroy(b);
        _boxes.Clear();
    }

    /// <summary>Forget boxes that went away with their HUD (a new PlayerHUD after a session change).</summary>
    public static void Prune()
    {
        _moved.RemoveAll(m => m.Child == null || m.Canvas == null);
        _boxes.RemoveAll(b => b == null);
    }

    /// <summary>Margins (canvas units) between a screen-sized canvas and the UI box inside it: HudLayout adds them to the
    /// padding of the boss bar / plague meter layout groups.</summary>
    public static Vector2 InsetFor(RectTransform canvasRect)
    {
        if (!Prefs.BoxHudWidgets.Value) return Vector2.zero;
        var size = canvasRect.rect.size;
        if (size.x <= 0f || size.y <= 0f) return Vector2.zero;
        var box = UiBox.FitBox(size, UiBox.GlobalAspect());
        return new Vector2(Mathf.Max(0f, (size.x - box.x) * 0.5f), Mathf.Max(0f, (size.y - box.y) * 0.5f));
    }
}
