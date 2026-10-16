namespace SF6_Plugin_Core.UI.Localization;

/// <summary>
/// A registry for managing custom messages and their GUIDs, 
/// allowing for the replacement of game messages with custom ones.
/// </summary>
internal static class MessageRegistry
{

    private static readonly ConcurrentDictionary<Guid, CustomMessage> CustomMessages = [];

    [ThreadStatic]
    private static CustomMessage? s_pendingMessageOverride;

    /// <summary>
    /// Registers a custom message.
    /// </summary>
    /// <param name="message">The custom message to register.</param>
    public static void RegisterCustomMessage(CustomMessage message)
    {
        if (!CustomMessages.ContainsKey(message.Id))
        {
            CustomMessages[message.Id] = message;
        }
    }

    internal static void UnregisterCustomMessage(Guid id)
    {
        if (CustomMessages.TryRemove(id, out var msg))
        {
            msg.ClearCachedEngineStrings();
        }
    }

    [MethodHook(typeof(app.helper.hLocalize), "getMessage(System.Guid)", MethodHookType.Pre)]
    public static PreHookResult ReplaceMessageQuery(Span<ulong> args)
    {
        // read argument
        IntPtr guidAddress = (IntPtr)args[1];
        Guid requestedGuid = Marshal.PtrToStructure<Guid>(guidAddress);

        // API.LogInfo($"getMessage called with GUID: {requestedGuid}");

        // Check if we have a custom message for this GUID
        if (CustomMessages.TryGetValue(requestedGuid, out var customMsg))
        {
            s_pendingMessageOverride = customMsg;
            return PreHookResult.Skip;
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.helper.hLocalize), "getMessage(System.Guid)", MethodHookType.Post)]
    public static void ReplaceMessageResponse(ref ulong retval)
    {
        if (s_pendingMessageOverride != null)
        {
            // Replace the return value with the memory address of our cached Engine String
            retval = s_pendingMessageOverride.GetEngineStringAddress();

            // Reset the override for the next call to prevent race conditions
            s_pendingMessageOverride = null;
        }
    }

    /// <summary>
    /// Clears all registered custom messages.
    /// </summary>
    public static void Clear()
    {
        foreach (var msg in CustomMessages.Values)
        {
            msg.ClearCachedEngineStrings();
        }
        CustomMessages.Clear();
    }
}