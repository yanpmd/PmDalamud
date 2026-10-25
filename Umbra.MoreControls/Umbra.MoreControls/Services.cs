using Dalamud.Game.ClientState.Conditions;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Umbra.MoreControls;

/// <summary>
/// Lazy accessors for the services Umbra exposes on its own service container.
/// Umbra registers every Dalamud <c>[PluginService]</c> instance plus its own
/// game services, so plugins can pull them with <see cref="Framework.Service{T}()"/>.
///
/// Results are cached, but failures are not, so that services registered later
/// in Umbra's startup sequence are still picked up.
/// </summary>
internal static class Services
{
    public static IPlayer?      Player      => Get(ref _player);
    public static ICondition?   Condition   => Get(ref _condition);
    public static IClientState? ClientState => Get(ref _clientState);
    public static IObjectTable? ObjectTable => Get(ref _objectTable);
    public static IPartyList?   PartyList   => Get(ref _partyList);
    public static IDataManager? DataManager => Get(ref _dataManager);

    private static IPlayer?      _player;
    private static ICondition?   _condition;
    private static IClientState? _clientState;
    private static IObjectTable? _objectTable;
    private static IPartyList?   _partyList;
    private static IDataManager? _dataManager;

    private static T? Get<T>(ref T? cache) where T : class
    {
        if (cache is not null) return cache;

        try {
            cache = Framework.Service<T>();
        } catch {
            return null;
        }

        return cache;
    }

    /// <summary>True if the given Dalamud condition flag is currently set.</summary>
    public static bool HasCondition(ConditionFlag flag)
    {
        ICondition? condition = Condition;

        try {
            return condition is not null && condition[flag];
        } catch {
            return false;
        }
    }

    /// <summary>Resolves a territory type id to its display name, if possible.</summary>
    public static string GetZoneName(uint territoryTypeId)
    {
        try {
            ExcelSheet<TerritoryType>? sheet = DataManager?.GetExcelSheet<TerritoryType>();

            if (sheet is not null && sheet.TryGetRow(territoryTypeId, out TerritoryType row)) {
                string name = row.PlaceName.Value.Name.ToString();

                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
        } catch {
            // Excel lookups are best-effort only.
        }

        return $"#{territoryTypeId}";
    }
}
