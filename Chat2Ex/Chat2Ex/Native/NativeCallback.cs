namespace Chat2Ex.Native;

/// <summary>
///     Helpers for callbacks that are invoked by the game itself.
/// </summary>
internal static class NativeCallback
{
    /// <summary>
    ///     Runs a callback that was triggered by the game.
    /// </summary>
    /// <remarks>
    ///     An exception that escapes into the game's UI event handling is not caught by Dalamud, it
    ///     travels back into the game and terminates the whole process. Every callback that the
    ///     game invokes has to swallow its exceptions.
    /// </remarks>
    /// <param name="callback">The callback to run.</param>
    /// <param name="context">A short description of the callback, used for the log entry.</param>
    public static void Safe(Action callback, string context)
    {
        try
        {
            callback();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, $"Chat2Ex: error while {context}");
        }
    }
}
