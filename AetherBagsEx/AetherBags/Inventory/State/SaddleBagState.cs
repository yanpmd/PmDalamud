using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AetherBags.Inventory.State;

public class SaddleBagState : InventoryStateBase
{
    public override InventorySourceType SourceType => HasPremiumSaddlebag
        ? InventorySourceType.AllSaddleBags
        :  InventorySourceType.SaddleBag;

    public override InventoryType[] Inventories => HasPremiumSaddlebag
        ? InventorySourceDefinitions.AllSaddleBags
        : InventorySourceDefinitions.SaddleBag;

    /// <summary>
    /// True when the saddlebag is actually open and its items can be moved.
    /// The saddlebag containers stay resident for the whole session, so their
    /// presence alone is not a reliable signal - the game only allows access
    /// while its own saddlebag addon is up.
    /// </summary>
    public static unsafe bool AreContainersLoaded
    {
        get
        {
            if (!Services.ClientState.IsLoggedIn) return false;

            var addonManager = RaptureAtkUnitManager.Instance();
            if (addonManager == null) return false;
            if (addonManager->GetAddonByName("InventoryBuddy") == null) return false;

            var inventoryManager = InventoryManager.Instance();
            if (inventoryManager == null) return false;

            foreach (var type in InventorySourceDefinitions.AllSaddleBags)
            {
                var container = inventoryManager->GetInventoryContainer(type);
                if (container != null && container->Size > 0) return true;
            }

            return false;
        }
    }

    private static unsafe bool HasPremiumSaddlebag
    {
        get
        {
            if (!Services.ClientState.IsLoggedIn) return false;

            var playerState = PlayerState.Instance();
            return playerState != null && playerState->HasPremiumSaddlebag;
        }
    }
}