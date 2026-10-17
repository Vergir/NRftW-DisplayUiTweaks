using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppMoon.Chat;
using Il2CppMoon.Forsaken;
using Il2CppMoon.TheLoop;
using Il2CppQuantum;

namespace DisplayUiTweaks.Patches;

/// <summary>
/// Game bug guard. <c>RefreshChatMessages.Tick</c> walks <c>PlayerChatState.ChatBubbles</c> (the speech bubbles over heroes)
/// every frame and calls into each bubble's GameObject. <c>PlayerChatAPI.UnregisterChatBubble(HeroView)</c> only removes
/// the entry when the hero's entity still exists in the simulation, so after a death / respawn or a co-op player leaving
/// the entry can outlive its bubble. Once the bubble is destroyed, Tick throws a NullReferenceException every frame and the
/// game's exception guard ends the session ("Unstable environment, too many exceptions") about 10 s later.
/// The prefix drops entries whose bubble is destroyed before Tick reaches them.
/// </summary>
[HarmonyPatch(typeof(RefreshChatMessages), nameof(RefreshChatMessages.Tick))]
internal static class RefreshChatMessagesPatch
{
    // ViewFrame.Chat is a ValueTuple<PlayerChatState, PlayerChatAPI> stored inline: Item1 is the first pointer.
    private static int _chatOffset = -1;
    private static IntPtr _statePtr;
    private static PlayerChatState? _state;
    private static readonly List<RuntimeCharacterReference> _dead = new();
    private static bool _failed;

    /// <summary>The chat state Tick last ran with (dev commands).</summary>
    internal static PlayerChatState? State => _state;

    static void Prefix(ViewFrame f)
    {
        if (_failed || f == null) return;
        try
        {
            if (_chatOffset < 0)
            {
                var field = IL2CPP.GetIl2CppField(Il2CppClassPointerStore<ViewFrame>.NativeClassPtr, "Chat");
                if (field == IntPtr.Zero) throw new MissingFieldException("ViewFrame.Chat");
                _chatOffset = (int)IL2CPP.il2cpp_field_get_offset(field);
            }
            var ptr = Marshal.ReadIntPtr(f.Pointer + _chatOffset);
            if (ptr == IntPtr.Zero) return;
            if (ptr != _statePtr) { _state = new PlayerChatState(ptr); _statePtr = ptr; }
            var bubbles = _state!.ChatBubbles;
            if (bubbles == null || bubbles.Count == 0) return;

            _dead.Clear();
            foreach (var kv in bubbles)
                if (kv.Value == null) _dead.Add(kv.Key);
            foreach (var key in _dead)
            {
                bubbles.Remove(key);
                DisplayUiTweaksMod.Log.Msg($"Removed the chat bubble of character {key.Raw}: destroyed but still registered (game bug, would end the session)");
            }
        }
        catch (Exception e)
        {
            _failed = true;
            DisplayUiTweaksMod.Log.Warning("Chat bubble guard off: " + e.Message);
        }
    }
}
