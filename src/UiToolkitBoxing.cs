using System.Collections.Generic;
using Il2CppMoon.Forsaken;
using UnityEngine;
using UnityEngine.UIElements;

namespace DisplayUiTweaks;

/// <summary>Boxes the bounty/challenge boards and the map's detail bar with inline styles (see docs/internal.md).</summary>
internal static class UiToolkitBoxing
{
    private const string ChunkDetailsInnerClass = "chunk-details-aspect-inner";

    // Documents of the screens we handle, found by a rescan (scene load / resolution change), not on every change.
    private static readonly List<UIDocument> _activityDocs = new List<UIDocument>();
    private static readonly List<UIDocument> _mapOverlays = new List<UIDocument>();

    private static readonly List<VisualElement> _boxedRoots = new List<VisualElement>();
    private static readonly List<VisualElement> _cappedElements = new List<VisualElement>();

    /// <summary>Call for a document that just enabled; boxes it if it belongs to a screen we handle.</summary>
    public static void OnDocumentEnabled(UIDocument doc)
    {
        if (doc == null) return;
        if (IsActivityDocument(doc)) BoxRoot(doc.rootVisualElement);
        else if (IsMapOverlay(doc)) CapChunkDetails(doc.rootVisualElement);
    }

    /// <summary>Re-apply to every known screen; rescan = look for the screens first (scene load, resolution change).</summary>
    public static void ApplyAll(bool rescan = false)
    {
        if (rescan) Rescan();
        foreach (var d in _activityDocs) if (d != null) BoxRoot(d.rootVisualElement);
        foreach (var d in _mapOverlays) if (d != null) CapChunkDetails(d.rootVisualElement);
    }

    private static void Rescan()
    {
        _activityDocs.Clear();
        _mapOverlays.Clear();
        foreach (var p in Resources.FindObjectsOfTypeAll<ActivityScreenPanel>())
            if (p != null && p.Document != null) _activityDocs.Add(p.Document);
        foreach (var p in Resources.FindObjectsOfTypeAll<ActivityVendorScreenPanel>())
            if (p != null && p.Document != null) _activityDocs.Add(p.Document);
        foreach (var m in Resources.FindObjectsOfTypeAll<MapScreen>())
            if (m != null && m.MapUiToolkitOverlay != null) _mapOverlays.Add(m.MapUiToolkitOverlay);
    }

    /// <summary>Remove every inline style we set (unload / hot reload).</summary>
    public static void RestoreAll()
    {
        foreach (var e in _boxedRoots) if (e != null) ClearBox(e);
        foreach (var e in _cappedElements) if (e != null) e.style.maxWidth = new StyleLength(StyleKeyword.Null);
        _boxedRoots.Clear();
        _cappedElements.Clear();
    }

    private static bool Active => Prefs.Enabled.Value && Prefs.BoxUiToolkitScreens.Value && UiBox.GlobalBoxIsSmallerThanScreen();

    private static bool IsActivityDocument(UIDocument doc) => _activityDocs.Exists(d => d != null && d.Pointer == doc.Pointer);

    private static bool IsMapOverlay(UIDocument doc) => _mapOverlays.Exists(d => d != null && d.Pointer == doc.Pointer);

    /// <summary>Screen-relative insets of the UI box, in percent (left/right, top/bottom).</summary>
    private static Vector2 InsetPercent()
    {
        Vector2 box = UiBox.GlobalBoxSizePx();
        float x = Mathf.Max(0f, (Screen.width - box.x) * 0.5f / Screen.width * 100f);
        float y = Mathf.Max(0f, (Screen.height - box.y) * 0.5f / Screen.height * 100f);
        return new Vector2(x, y);
    }

    private static void BoxRoot(VisualElement? root)
    {
        if (root == null) return;
        if (!Active) { ClearBox(root); return; }
        Vector2 inset = InsetPercent();
        var s = root.style;
        s.position = new StyleEnum<Position>(Position.Absolute);
        s.left = new StyleLength(new Length(inset.x, LengthUnit.Percent));
        s.right = new StyleLength(new Length(inset.x, LengthUnit.Percent));
        s.top = new StyleLength(new Length(inset.y, LengthUnit.Percent));
        s.bottom = new StyleLength(new Length(inset.y, LengthUnit.Percent));
        if (!Contains(_boxedRoots, root)) _boxedRoots.Add(root);
    }

    private static void ClearBox(VisualElement root)
    {
        var s = root.style;
        var none = new StyleLength(StyleKeyword.Null);
        s.left = none; s.right = none; s.top = none; s.bottom = none;
        s.position = new StyleEnum<Position>(StyleKeyword.Null);
    }

    private static void CapChunkDetails(VisualElement? root)
    {
        if (root == null) return;
        var inner = UQueryExtensions.Q(root, null, ChunkDetailsInnerClass);
        if (inner == null) return;
        if (!Active) { inner.style.maxWidth = new StyleLength(StyleKeyword.Null); return; }
        // The parent spans the whole panel, so percent = share of the screen.
        float pct = UiBox.GlobalBoxSizePx().x / Screen.width * 100f;
        inner.style.maxWidth = new StyleLength(new Length(pct, LengthUnit.Percent));
        if (!Contains(_cappedElements, inner)) _cappedElements.Add(inner);
    }

    private static bool Contains(List<VisualElement> list, VisualElement e)
    {
        foreach (var x in list) if (x != null && x.Pointer == e.Pointer) return true;
        return false;
    }
}
