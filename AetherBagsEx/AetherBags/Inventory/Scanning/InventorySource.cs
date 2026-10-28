using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Inventory.Scanning;

public enum InventorySourceType
{
    MainBags,
    SaddleBag,
    PremiumSaddleBag,
    AllSaddleBags,
    Retainer,
    Armoury,
}

public static class InventorySourceDefinitions
{
    public static readonly InventoryType[] MainBags =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
    ];

    public static readonly InventoryType[] SaddleBag =
    [
        InventoryType.SaddleBag1,
        InventoryType.SaddleBag2,
    ];

    public static readonly InventoryType[] PremiumSaddleBag =
    [
        InventoryType.PremiumSaddleBag1,
        InventoryType.PremiumSaddleBag2,
    ];

    public static readonly InventoryType[] AllSaddleBags =
    [
        InventoryType.SaddleBag1,
        InventoryType.SaddleBag2,
        InventoryType.PremiumSaddleBag1,
        InventoryType.PremiumSaddleBag2,
    ];

    public static readonly InventoryType[] Retainer =
    [
        InventoryType.RetainerPage1,
        InventoryType.RetainerPage2,
        InventoryType.RetainerPage3,
        InventoryType.RetainerPage4,
        InventoryType.RetainerPage5,
        InventoryType.RetainerPage6,
        InventoryType.RetainerPage7,
    ];

    /// <summary>
    /// The armoury chest containers, in the order the game's armoury board lists
    /// them. <see cref="InventoryType.ArmoryWaist"/> is intentionally missing: the
    /// waist slot no longer exists in game and has no UI page.
    /// </summary>
    public static readonly InventoryType[] Armoury =
    [
        InventoryType.ArmoryMainHand,
        InventoryType.ArmoryOffHand,
        InventoryType.ArmoryHead,
        InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,
        InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets,
        InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings,
        InventoryType.ArmorySoulCrystal,
    ];

    public static InventoryType[] GetInventories(InventorySourceType source) => source switch
    {
        InventorySourceType.MainBags => MainBags,
        InventorySourceType.SaddleBag => SaddleBag,
        InventorySourceType.PremiumSaddleBag => PremiumSaddleBag,
        InventorySourceType.AllSaddleBags => AllSaddleBags,
        InventorySourceType.Retainer => Retainer,
        InventorySourceType.Armoury => Armoury,
        _ => MainBags,
    };

    public static InventoryType[] GetContainersForSource(InventorySourceType source) => source switch
    {
        InventorySourceType.MainBags => MainBags,
        InventorySourceType.SaddleBag => SaddleBag,
        InventorySourceType.PremiumSaddleBag => PremiumSaddleBag,
        InventorySourceType.AllSaddleBags => AllSaddleBags,
        InventorySourceType.Retainer => Retainer,
        InventorySourceType.Armoury => Armoury,
        _ => MainBags,
    };

    public static int GetTotalSlots(InventorySourceType source) => source switch
    {
        InventorySourceType.MainBags => 140,                    // 4 * 35
        InventorySourceType.SaddleBag => 70,                    // 2 * 35
        InventorySourceType.PremiumSaddleBag => 70,             // 2 * 35
        InventorySourceType.AllSaddleBags => 140,               // 2 * 35
        InventorySourceType.Retainer => Retainer.Length * 25,   // 7 * 25
        InventorySourceType.Armoury => Armoury.Length * 50,     // 12 * 50
        _ => 140,
    };
}