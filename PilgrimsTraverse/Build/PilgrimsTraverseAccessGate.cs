using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PilgrimsTraverse.Build;

/// <summary>
/// Public access decision used by FSD start authorization.
/// Official builds may supply a stricter gate via the build-profile overlay; the
/// public default is explicit allow-all.
/// </summary>
internal sealed record PilgrimsTraverseAccessDecision(bool IsAllowed, string Reason)
{
    internal static PilgrimsTraverseAccessDecision Allowed() =>
        new(true, "Public build profile allows all access.");

    internal static PilgrimsTraverseAccessDecision Denied(string reason) =>
        new(false, reason);
}

/// <summary>
/// Host access gate abstraction. Public builds use <see cref="PilgrimsTraverseAllowAllAccessGate"/>.
/// Official overlays may inject a private implementation without exposing it publicly.
/// </summary>
internal interface IPilgrimsTraverseAccessGate
{
    PilgrimsTraverseAccessDecision Current { get; }

    /// <summary>
    /// Read-only notice for the shared FSD Start/Stop UI when start is denied. Empty when unused.
    /// </summary>
    string FsdStartDenialNotice { get; }

    bool TryAuthorizeFsdStart(out string error);
}

internal sealed class PilgrimsTraverseAllowAllAccessGate : IPilgrimsTraverseAccessGate
{
    public PilgrimsTraverseAccessDecision Current => PilgrimsTraverseAccessDecision.Allowed();

    public string FsdStartDenialNotice => string.Empty;

    public bool TryAuthorizeFsdStart(out string error)
    {
        error = string.Empty;
        return true;
    }
}

internal delegate IPilgrimsTraverseAccessGate PilgrimsTraverseAccessGateFactory(
    IDalamudPluginInterface pluginInterface,
    IPluginLog pluginLog);
