using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DeepDungeon.Fsd.Dalamud;

namespace PilgrimsTraverse.Build;

/// <summary>
/// Explicit online-service and access-gate capabilities for this host build.
/// Public checkouts resolve to <see cref="PublicNoService"/>. Official private
/// builds may stage <c>OfficialBuildProfile.g.cs</c> under <c>Build/</c> to replace these values via
/// <see cref="PilgrimsTraverseBuildProfile.ProvideOfficialCapabilities"/>.
/// </summary>
internal sealed class PilgrimsTraverseBuildCapabilities
{
    public Uri? DetailedMapCatalogEndpoint { get; init; }
    public Uri? CommunityEvidenceEndpoint { get; init; }
    public Uri? UsageTelemetryEndpoint { get; init; }
    public Uri? LongRunLogEndpoint { get; init; }
    public bool ContributesAnonymousEvidence { get; init; }
    public bool DeleteCatalogsWhenDisabled { get; init; }
    public bool SupportsControlledPtSurvey { get; init; }
    public PilgrimsTraverseAccessGateFactory? CreateAccessGate { get; init; }

    public bool HasOnlineCatalogService => DetailedMapCatalogEndpoint != null;
    public bool HasCommunityEvidenceUpload => CommunityEvidenceEndpoint != null;
    public bool HasUsageTelemetry => UsageTelemetryEndpoint != null;
    public bool HasLongRunLogUpload => LongRunLogEndpoint != null;

    public static PilgrimsTraverseBuildCapabilities PublicNoService { get; } = new()
    {
        DetailedMapCatalogEndpoint = null,
        CommunityEvidenceEndpoint = null,
        UsageTelemetryEndpoint = null,
        LongRunLogEndpoint = null,
        ContributesAnonymousEvidence = false,
        DeleteCatalogsWhenDisabled = false,
        SupportsControlledPtSurvey = false,
        CreateAccessGate = null
    };
}

/// <summary>
/// Build-profile extension boundary. Without an official overlay this is the
/// public no-service profile: no catalog URI, no evidence URI, allow-all gate.
/// </summary>
internal static partial class PilgrimsTraverseBuildProfile
{
    internal static PilgrimsTraverseBuildCapabilities Capabilities { get; } = ResolveCapabilities();

    internal static IPilgrimsTraverseAccessGate CreateAccessGate(
        IDalamudPluginInterface pluginInterface,
        IPluginLog pluginLog)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        ArgumentNullException.ThrowIfNull(pluginLog);

        PilgrimsTraverseAccessGateFactory? factory = Capabilities.CreateAccessGate;
        if (factory != null)
            return factory(pluginInterface, pluginLog);

        return new PilgrimsTraverseAllowAllAccessGate();
    }

    internal static DetailedMapHostOptions CreateDetailedMapHostOptions()
    {
        PilgrimsTraverseBuildCapabilities capabilities = Capabilities;
        return new DetailedMapHostOptions(
            capabilities.DetailedMapCatalogEndpoint,
            contributesAnonymousEvidence: capabilities.ContributesAnonymousEvidence,
            deleteCatalogsWhenDisabled: capabilities.DeleteCatalogsWhenDisabled,
            supportsControlledPtSurvey: capabilities.SupportsControlledPtSurvey);
    }

    /// <summary>
    /// Optional official overlay hook. Implemented only by staged
    /// <c>Build/OfficialBuildProfile.g.cs</c>; public builds leave this unimplemented.
    /// </summary>
    static partial void ProvideOfficialCapabilities(
        ref PilgrimsTraverseBuildCapabilities? capabilities);

    private static PilgrimsTraverseBuildCapabilities ResolveCapabilities()
    {
        PilgrimsTraverseBuildCapabilities? official = null;
        ProvideOfficialCapabilities(ref official);
        return official ?? PilgrimsTraverseBuildCapabilities.PublicNoService;
    }
}
