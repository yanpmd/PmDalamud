using System.ComponentModel;
using AetherBags.Inventory.Scanning;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace AetherBags.Configuration;

/// <summary>
/// Which armoury chest part the window currently shows. Showing every part at
/// once produces ~600 slots, which is far too much on screen, so the window
/// defaults to a single part.
/// </summary>
public enum ArmouryPart : byte
{
    MainHand = 0,
    OffHand = 1,
    Head = 2,
    Body = 3,
    Hands = 4,
    Legs = 5,
    Feet = 6,
    Ears = 7,
    Neck = 8,
    Wrists = 9,
    Rings = 10,
    SoulCrystal = 11,
    All = 12,
}

/// <summary>
/// How the armoury window groups its items. Grouping by slot puts every job's
/// gear into one pile, so the class/job category is the more useful default.
/// </summary>
public enum ArmouryGrouping : byte
{
    [Description("By Role")]
    ByRole = 0,

    [Description("By Slot")]
    BySlot = 1,
}

public static class ArmouryParts
{
    /// <summary>
    /// Parts in display order. <c>nameKey</c> is the long name used in menus,
    /// <c>shortKey</c> the two character label for the sidebar buttons.
    /// </summary>
    public static readonly (ArmouryPart part, string nameKey, string shortKey)[] Items =
    [
        (ArmouryPart.MainHand, "ArmouryPart.MainHand", "ArmouryPartShort.MainHand"),
        (ArmouryPart.OffHand, "ArmouryPart.OffHand", "ArmouryPartShort.OffHand"),
        (ArmouryPart.Head, "ArmouryPart.Head", "ArmouryPartShort.Head"),
        (ArmouryPart.Body, "ArmouryPart.Body", "ArmouryPartShort.Body"),
        (ArmouryPart.Hands, "ArmouryPart.Hands", "ArmouryPartShort.Hands"),
        (ArmouryPart.Legs, "ArmouryPart.Legs", "ArmouryPartShort.Legs"),
        (ArmouryPart.Feet, "ArmouryPart.Feet", "ArmouryPartShort.Feet"),
        (ArmouryPart.Ears, "ArmouryPart.Ears", "ArmouryPartShort.Ears"),
        (ArmouryPart.Neck, "ArmouryPart.Neck", "ArmouryPartShort.Neck"),
        (ArmouryPart.Wrists, "ArmouryPart.Wrists", "ArmouryPartShort.Wrists"),
        (ArmouryPart.Rings, "ArmouryPart.Rings", "ArmouryPartShort.Rings"),
        (ArmouryPart.SoulCrystal, "ArmouryPart.SoulCrystal", "ArmouryPartShort.SoulCrystal"),
        (ArmouryPart.All, "ArmouryPart.All", "ArmouryPartShort.All"),
    ];

    public static string GetName(ArmouryPart part)
    {
        foreach (var item in Items)
        {
            if (item.part == part) return Localization.Get(item.nameKey);
        }

        return part.ToString();
    }

    public static string GetShortName(ArmouryPart part)
    {
        foreach (var item in Items)
        {
            if (item.part == part) return Localization.Get(item.shortKey);
        }

        return part.ToString();
    }

    /// <summary>
    /// Maps the vanilla armoury board tab index onto a part. The board lists the
    /// left hand column first (main hand, head, body, hands, legs, feet) and puts
    /// the off hand at the top of the right hand column, so the order is neither
    /// the InventoryType order nor grouped by left/right slot.
    /// </summary>
    public static ArmouryPart? FromTabIndex(int tabIndex) => tabIndex switch
    {
        0 => ArmouryPart.MainHand,
        1 => ArmouryPart.Head,
        2 => ArmouryPart.Body,
        3 => ArmouryPart.Hands,
        4 => ArmouryPart.Legs,
        5 => ArmouryPart.Feet,
        6 => ArmouryPart.OffHand,
        7 => ArmouryPart.Ears,
        8 => ArmouryPart.Neck,
        9 => ArmouryPart.Wrists,
        10 => ArmouryPart.Rings,
        11 => ArmouryPart.SoulCrystal,
        _ => null,
    };

    // One shared array per part: GetContainers is read on every refresh (scan,
    // counters, drag target), so returning a fresh array each call would allocate
    // continuously.
    private static readonly InventoryType[] MainHandContainers = [InventoryType.ArmoryMainHand];
    private static readonly InventoryType[] OffHandContainers = [InventoryType.ArmoryOffHand];
    private static readonly InventoryType[] HeadContainers = [InventoryType.ArmoryHead];
    private static readonly InventoryType[] BodyContainers = [InventoryType.ArmoryBody];
    private static readonly InventoryType[] HandsContainers = [InventoryType.ArmoryHands];
    private static readonly InventoryType[] LegsContainers = [InventoryType.ArmoryLegs];
    private static readonly InventoryType[] FeetContainers = [InventoryType.ArmoryFeets];
    private static readonly InventoryType[] EarsContainers = [InventoryType.ArmoryEar];
    private static readonly InventoryType[] NeckContainers = [InventoryType.ArmoryNeck];
    private static readonly InventoryType[] WristsContainers = [InventoryType.ArmoryWrist];
    private static readonly InventoryType[] RingsContainers = [InventoryType.ArmoryRings];
    private static readonly InventoryType[] SoulCrystalContainers = [InventoryType.ArmorySoulCrystal];

    public static InventoryType[] GetContainers(ArmouryPart part) => part switch
    {
        ArmouryPart.MainHand => MainHandContainers,
        ArmouryPart.OffHand => OffHandContainers,
        ArmouryPart.Head => HeadContainers,
        ArmouryPart.Body => BodyContainers,
        ArmouryPart.Hands => HandsContainers,
        ArmouryPart.Legs => LegsContainers,
        ArmouryPart.Feet => FeetContainers,
        ArmouryPart.Ears => EarsContainers,
        ArmouryPart.Neck => NeckContainers,
        ArmouryPart.Wrists => WristsContainers,
        ArmouryPart.Rings => RingsContainers,
        ArmouryPart.SoulCrystal => SoulCrystalContainers,
        _ => InventorySourceDefinitions.Armoury,
    };
}
