using System.Collections.Generic;
using Il2CppMoon.Forsaken;
using UnityEngine;
using UnityEngine.UIElements;


namespace MoreAspectRatios;

/// <summary>
/// The game's UI aspect option only boxes uGUI. UI Toolkit screens get a USS class per mode instead
/// (ui-aspect-native / -16x9 / -21x9 / -32x9 / -custom), and the style sheets use it for one element each:
///   ActivityWindow.uss: .activity-screen.ui-aspect-16x9 .activity-screen-bottom-bar { width: 1920px; centered }
///   Map.uss:            .chunk-details-aspect-inner.ui-aspect-16x9 { max-width: 1920px }
/// The rest of those screens always spans the whole screen.
///
/// We box them ourselves with inline styles, which win over style sheets:
///  - the bounty/challenge boards (ActivityScreenPanel, ActivityVendorScreenPanel): the document's root element is
///    positioned absolutely inside the UI box (percent of the panel, so it is independent of the panel scale);
///  - the map (MapScreen): only the chunk-details bar gets a max-width, because the map itself is world-like and its
///    markers are laid over uGUI map tiles that are not boxed.
/// Styles are removed again when the box covers the whole screen, and on unload.
/// </summary>
internal static class UiToolkitBoxing
{
    private const string ChunkDetailsInnerClass = "chunk-details-aspect-inner";

    private static readonly List<VisualElement> _boxedRoots = new List<VisualElement>();
    private static readonly List<VisualElement> _cappedElements = new List<VisualElement>();

    /// <summary>Call for a document that just enabled; boxes it if it belongs to a screen we handle.</summary>
    public static void OnDocumentEnabled(UIDocument doc)
    {
        if (doc == null) return;
        if (IsActivityDocument(doc)) BoxRoot(doc.rootVisualElement);
        else if (IsMapOverlay(doc)) CapChunkDetails(doc.rootVisualElement);
    }

    /// <summary>Re-apply to every known screen (settings changed, resolution changed, scene loaded).</summary>
    public static void ApplyAll()
    {
        foreach (var p in Resources.FindObjectsOfTypeAll<ActivityScreenPanel>())
            if (p != null && p.Document != null) BoxRoot(p.Document.rootVisualElement);
        foreach (var p in Resources.FindObjectsOfTypeAll<ActivityVendorScreenPanel>())
            if (p != null && p.Document != null) BoxRoot(p.Document.rootVisualElement);
        foreach (var m in Resources.FindObjectsOfTypeAll<MapScreen>())
            if (m != null && m.MapUiToolkitOverlay != null) CapChunkDetails(m.MapUiToolkitOverlay.rootVisualElement);
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

    private static bool IsActivityDocument(UIDocument doc)
    {
        foreach (var p in Resources.FindObjectsOfTypeAll<ActivityScreenPanel>())
            if (p != null && p.Document != null && p.Document.Pointer == doc.Pointer) return true;
        foreach (var p in Resources.FindObjectsOfTypeAll<ActivityVendorScreenPanel>())
            if (p != null && p.Document != null && p.Document.Pointer == doc.Pointer) return true;
        return false;
    }

    private static bool IsMapOverlay(UIDocument doc)
    {
        foreach (var m in Resources.FindObjectsOfTypeAll<MapScreen>())
            if (m != null && m.MapUiToolkitOverlay != null && m.MapUiToolkitOverlay.Pointer == doc.Pointer) return true;
        return false;
    }

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
        // Its parent (.chunk-details-aspect-root) spans the panel from left 0 to right 0, so percent = share of the screen.
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
