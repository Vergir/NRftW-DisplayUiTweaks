using Il2CppMoon.Forsaken;
using UnityEngine;

namespace DisplayUiTweaks.Hud;

/// <summary>
/// The chat window only has the message rows its pool spawned at start (ChatWindow.m_maxDisplayedMessages, 10): a taller
/// window would show the newest 10 lines and empty space above them. When the window's viewport grows, rows are cloned
/// into ChatWindow.m_messageQueue (the list RefreshData maps history entries onto) until they fill it, and
/// m_maxDisplayedMessages follows. Rows are only ever added (a shorter window just shows fewer of them).
/// </summary>
internal static class ChatRows
{
    private static float _lastHeight = -1f;
    private static System.IntPtr _lastChat;
    private static float _nextLog;

    public static void Ensure(PlayerHUD hud)
    {
        var chat = hud.ChatWindow;
        var rows = chat != null ? chat.m_messageQueue : null;
        var parent = chat != null ? chat.m_messageParent : null;
        var viewport = parent != null ? parent.parent?.TryCast<RectTransform>() : null;
        if (rows == null || rows.Count == 0 || viewport == null) return;
        float height = viewport.rect.height;
        if (chat!.Pointer == _lastChat && Mathf.Abs(height - _lastHeight) < 0.5f) return;
        _lastChat = chat.Pointer;
        _lastHeight = height;

        var template = rows[rows.Count - 1];
        if (template == null) return;
        float rowHeight = template.m_text != null ? template.m_text.fontSize * 1.3f : 0f;
        if (rowHeight < 8f) rowHeight = 24f;
        int needed = Mathf.CeilToInt(height / rowHeight) + 2;
        if (rows.Count >= needed) return;
        int before = rows.Count;
        while (rows.Count < needed)
        {
            var go = Object.Instantiate(template.gameObject, template.transform.parent);
            go.name = template.gameObject.name + "_DUT" + rows.Count;
            rows.Add(go.GetComponent<ChatMessageDisplay>());
        }
        chat.m_maxDisplayedMessages = rows.Count;
        chat.RefreshData(-1, true);
        if (Time.unscaledTime >= _nextLog) DisplayUiTweaksMod.Log.Msg($"Chat: {before} -> {rows.Count} message rows for a {height:0}-unit window");
        _nextLog = Time.unscaledTime + 2f;
    }
}
