namespace SF6_Plugin_Core.UI.Localization;

/// <summary>
///     A registry for managing custom messages and their GUIDs,
///     allowing for the replacement of game messages with custom ones.
/// </summary>
internal static class MessageRegistry
{
    private static readonly ConcurrentDictionary<Guid, CustomMessage> CustomMessages = [];

    [ThreadStatic] private static Stack<CustomMessage?>? s_pendingOverrides;

    /// <summary>
    ///     Registers a custom message.
    /// </summary>
    /// <param name="message">The custom message to register.</param>
    public static void RegisterCustomMessage(CustomMessage message)
    {
        if (!CustomMessages.ContainsKey(message.Id)) CustomMessages[message.Id] = message;
    }

    internal static void UnregisterCustomMessage(Guid id)
    {
        if (CustomMessages.TryRemove(id, out var msg)) msg.ClearCachedEngineStrings();
    }

    [MethodHook(typeof(app.helper.hLocalize), "getMessage(System.Guid)", MethodHookType.Pre)]
    public static PreHookResult ReplaceMessageQuery(Span<ulong> args)
    {
        s_pendingOverrides ??= new Stack<CustomMessage?>();

        try
        {
            if (args.Length > 1 && args[1] != 0)
            {
                var requestedGuid = Marshal.PtrToStructure<Guid>((IntPtr)args[1]);
                if (CustomMessages.TryGetValue(requestedGuid, out var customMsg))
                {
                    s_pendingOverrides.Push(customMsg);
                    return PreHookResult.Skip;
                }
            }
        }
        catch (Exception ex)
        {
            API.LogError($"[MessageRegistry] Exception in ReplaceMessageQuery: {ex.Message}");
        }

        s_pendingOverrides.Push(null);
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.helper.hLocalize), "getMessage(System.Guid)", MethodHookType.Post)]
    public static void ReplaceMessageResponse(ref ulong retval)
    {
        if (s_pendingOverrides == null || !s_pendingOverrides.TryPop(out var pending) || pending == null)
            return;

        try
        {
            // Replace the return value with the memory address of our cached Engine String
            retval = pending.GetEngineStringAddress();
        }
        catch (Exception ex)
        {
            API.LogError($"[MessageRegistry] Exception in ReplaceMessageResponse: {ex.Message}");
        }
    }

    /// <summary>
    ///     Clears all registered custom messages.
    /// </summary>
    public static void Clear()
    {
        foreach (var msg in CustomMessages.Values) msg.ClearCachedEngineStrings();
        CustomMessages.Clear();
    }
}