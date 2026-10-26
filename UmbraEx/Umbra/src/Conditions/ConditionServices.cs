using Dalamud.Game.ClientState.Conditions;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Umbra.Conditions;

/// <summary>
/// Lazy service accessors for conditions. Conditions are instantiated by
/// <see cref="ConditionRegistry"/> before the container is fully populated, so
/// dependencies must not be resolved in their constructors.
/// </summary>
internal static class ConditionServices
{
    public static IPlayer?     Player      => Get(ref _player);
    public static ICondition?  Condition   => Get(ref _condition);
    public static IClientState? ClientState => Get(ref _clientState);
    public static IPartyList?  PartyList   => Get(ref _partyList);
    public static IDataManager? DataManager => Get(ref _dataManager);
    public static IPlayerState? PlayerState => Get(ref _playerState);
    public static ITargetManager? TargetManager => Get(ref _targetManager);
    public static IKeyState?    KeyState    => Get(ref _keyState);

    private static IPlayer?      _player;
    private static ICondition?   _condition;
    private static IClientState? _clientState;
    private static IPartyList?   _partyList;
    private static IDataManager? _dataManager;
    private static IPlayerState? _playerState;
    private static ITargetManager? _targetManager;
    private static IKeyState?    _keyState;

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

    public static bool HasCondition(ConditionFlag flag)
    {
        try {
            return Condition?[flag] ?? false;
        } catch {
            return false;
        }
    }

    public static string GetZoneName(uint territoryTypeId)
    {
        try {
            ExcelSheet<TerritoryType>? sheet = DataManager?.GetExcelSheet<TerritoryType>();

            if (sheet is not null && sheet.TryGetRow(territoryTypeId, out TerritoryType row)) {
                string name = row.PlaceName.Value.Name.ToString();

                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
        } catch {
            // Best effort only.
        }

        return $"#{territoryTypeId}";
    }
}
