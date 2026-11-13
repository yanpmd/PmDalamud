using System.Collections.Concurrent;

namespace Chat2Ex.Native;

/// <summary>
///     Helpers for callbacks that are invoked by the game itself.
/// </summary>
internal static class NativeCallback
{
    private const long LogWindowMs = 1000;

    private sealed class ThrottleState
    {
        public long WindowStart;
        public int Suppressed;
    }

    private static readonly ConcurrentDictionary<string, ThrottleState> Throttles = new();

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
            LogError(ex, $"error while {context}");
        }
    }

    /// <summary>
    ///     Logs an exception from a game callback at most once per second per context. A failure
    ///     repeating every frame would otherwise flood the log and make the stutter worse.
    /// </summary>
    /// <remarks>
    ///     Logging itself is wrapped as well: during plugin unload Plugin.Log may no longer be usable,
    ///     and an exception from the logger must not escape into the game either.
    /// </remarks>
    public static void LogError(Exception ex, string context)
    {
        try
        {
            var now = Environment.TickCount64;
            var state = Throttles.GetOrAdd(context, static _ => new ThrottleState());

            int suppressed;
            lock (state)
            {
                if (now - state.WindowStart < LogWindowMs)
                {
                    state.Suppressed++;
                    return;
                }

                suppressed = state.Suppressed;
                state.WindowStart = now;
                state.Suppressed = 0;
            }

            Plugin.Log.Error(ex, suppressed > 0
                ? $"Chat2Ex: {context} ({suppressed} more in the last second)"
                : $"Chat2Ex: {context}");
        }
        catch
        {
            // ignored
        }
    }

    private sealed class DeferredAction
    {
        public DeferredAction(Action action, int frames)
        {
            Action = action;
            Frames = frames;
        }

        public Action Action { get; }
        public int Frames { get; set; }
    }

    private static readonly List<DeferredAction> DeferredActions = [];
    private static readonly object DeferredGate = new();

    /// <summary>
    ///     Defers an action by several frames. IFramework.RunOnTick still runs inside the current
    ///     framework tick, which is too early when the game is unwinding a context menu's
    ///     FireCallback(close) / addon-hide chain: allocating a native addon at that point mutates
    ///     the addon list while the game is hiding one and crashes (C0000005 in AtkUnitBase::Hide,
    ///     triggered from the top-right tab menu's "open settings" item).
    /// </summary>
    public static void Defer(Action action, int frames = 4)
    {
        lock (DeferredGate)
            DeferredActions.Add(new DeferredAction(action, Math.Max(1, frames)));
    }

    /// <summary>Called once per frame from Plugin.DrawCore; each due action is guarded.</summary>
    public static void TickDeferred()
    {
        List<DeferredAction>? due = null;
        lock (DeferredGate)
        {
            for (var i = DeferredActions.Count - 1; i >= 0; i--)
            {
                var item = DeferredActions[i];
                if (--item.Frames > 0)
                    continue;

                DeferredActions.RemoveAt(i);
                (due ??= []).Add(item);
            }
        }

        if (due is null)
            return;

        foreach (var item in due)
            Safe(item.Action, "running a deferred UI action");
    }

    public static void ClearDeferred()
    {
        lock (DeferredGate)
            DeferredActions.Clear();
    }
}
