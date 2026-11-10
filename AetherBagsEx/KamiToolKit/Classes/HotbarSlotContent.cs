using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace KamiToolKit.Classes;

/// <summary>
/// Describes the content of a hotbar slot.
/// </summary>
/// <param name="Type">The kind of content the slot holds.</param>
/// <param name="Id">
/// The identifier of the content. Its meaning depends on <paramref name="Type"/>: an action id for
/// <c>Action</c>, an item id for <c>Item</c>, a macro index for <c>Macro</c>, and the encoded inventory
/// position for <c>InventoryItem</c>.
/// </param>
public readonly record struct HotbarSlotContent(RaptureHotbarModule.HotbarSlotType Type, uint Id);
