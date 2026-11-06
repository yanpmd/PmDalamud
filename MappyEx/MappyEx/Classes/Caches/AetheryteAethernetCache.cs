using KamiLib.Classes;
using KamiLib.Extensions;
using Lumina.Excel.Sheets;

namespace Mappy.Classes.Caches;

public class AetheryteAethernetCache : Cache<uint, Aetheryte?>
{
    protected override Aetheryte? LoadValue(uint key)
    {
        var aetheryteSheet = Service.DataManager.GetExcelSheet<Aetheryte>();

        // A shard carries the PlaceName row in its aethernet name, a big aetheryte can carry the same name, so the
        // shard is preferred to resolve the group the player actually clicked.
        var shard = aetheryteSheet.FirstOrNull(aetheryte => aetheryte.AethernetName.RowId == key && !aetheryte.IsAetheryte)
                    ?? aetheryteSheet.FirstOrNull(aetheryte => aetheryte.AethernetName.RowId == key);

        if (shard is not { AethernetGroup: var aethernetGroup }) return null;

        // Group 0 is not a network, the crystals of the wilderness zones use it as a placeholder. Resolving it would
        // pick the crystal of an unrelated zone.
        if (aethernetGroup is 0) return null;

        // The shard resolves to the aetheryte of its group, that is the crystal the teleport ends at.
        if (aetheryteSheet.FirstOrNull(aetheryte => aetheryte.IsAetheryte && aetheryte.AethernetGroup == aethernetGroup) is not { } targetAetheryte)
            return null;

        return targetAetheryte;
    }
}