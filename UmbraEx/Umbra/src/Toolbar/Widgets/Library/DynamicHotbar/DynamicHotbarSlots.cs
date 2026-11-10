using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace Umbra.Widgets.Library.DynamicHotbar;

/// <summary>
/// The kind of content that is stored in a dynamic hotbar slot.
/// </summary>
public enum DynamicHotbarSlotKind : byte {
    /// <summary>
    /// The slot is empty.
    /// </summary>
    Empty = 0,

    /// <summary>
    /// The slot holds a player action, addressed by its action id.
    /// </summary>
    Action = 1,

    /// <summary>
    /// The slot holds an item, addressed by its item id.
    /// </summary>
    Item = 2,

    /// <summary>
    /// The slot holds a macro, addressed by its index in the macro list.
    /// </summary>
    Macro = 3,
}

/// <summary>
/// A single slot of the dynamic hotbar.
/// </summary>
/// <param name="Kind">The kind of content in the slot.</param>
/// <param name="Id">The id of the content, interpreted according to <paramref name="Kind"/>.</param>
public readonly record struct DynamicHotbarSlot(DynamicHotbarSlotKind Kind, uint Id) {
    /// <summary>
    /// An empty slot.
    /// </summary>
    public static readonly DynamicHotbarSlot Empty = new(DynamicHotbarSlotKind.Empty, 0);

    /// <summary>
    /// True when this slot holds no content.
    /// </summary>
    public bool IsEmpty => Kind == DynamicHotbarSlotKind.Empty || Id == 0;
}

/// <summary>
/// Holds the contents of the dynamic hotbar for every zone the user has visited.
/// </summary>
/// <remarks>
/// The slots are stored per territory, so that entering a new zone starts with an empty hotbar while
/// returning to a known one restores what was configured for it.
/// </remarks>
internal sealed class DynamicHotbarSlotStore {
    private const string DataHeader = "DHB";

    private Dictionary<uint, List<DynamicHotbarSlot>> _slotsByTerritory = new();

    /// <summary>
    /// Gets the slots that are configured for the given territory.
    /// </summary>
    public List<DynamicHotbarSlot> GetSlots(uint territoryId) {
        if (!_slotsByTerritory.TryGetValue(territoryId, out List<DynamicHotbarSlot>? slots)) {
            slots = [];
            _slotsByTerritory[territoryId] = slots;
        }

        return slots;
    }

    /// <summary>
    /// Replaces the slots that are configured for the given territory.
    /// </summary>
    public void SetSlots(uint territoryId, List<DynamicHotbarSlot> slots) {
        _slotsByTerritory[territoryId] = slots;
    }

    /// <summary>
    /// Serializes the store into a compact string that can be stored in a config variable.
    /// </summary>
    public string Encode() {
        Dictionary<uint, List<SlotEntry>> data = new();

        foreach ((uint territoryId, List<DynamicHotbarSlot> slots) in _slotsByTerritory) {
            data[territoryId] = slots.Select(slot => new SlotEntry { Kind = slot.Kind, Id = slot.Id }).ToList();
        }

        return $"{DataHeader}|{Compress(JsonConvert.SerializeObject(data))}";
    }

    /// <summary>
    /// Restores the store from a string that was produced by <see cref="Encode"/>.
    /// </summary>
    public void Decode(string? data) {
        _slotsByTerritory = new Dictionary<uint, List<DynamicHotbarSlot>>();

        if (string.IsNullOrEmpty(data)) return;

        string[] parts = data.Split('|');
        if (parts.Length != 2 || parts[0] != DataHeader) return;

        try {
            Dictionary<uint, List<SlotEntry>>? restored =
                JsonConvert.DeserializeObject<Dictionary<uint, List<SlotEntry>>>(Decompress(parts[1]));

            if (restored is null) return;

            foreach ((uint territoryId, List<SlotEntry> entries) in restored) {
                _slotsByTerritory[territoryId] = entries
                                                .Select(entry => new DynamicHotbarSlot(entry.Kind, entry.Id))
                                                .ToList();
            }
        }
        catch (Exception e) {
            Logger.Error($"Failed to decode dynamic hotbar data: {e.Message}");
            _slotsByTerritory = new Dictionary<uint, List<DynamicHotbarSlot>>();
        }
    }

    private static string Compress(string text) {
        byte[] bytes = Encoding.UTF8.GetBytes(text);

        using var output = new MemoryStream();

        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize)) {
            deflate.Write(bytes, 0, bytes.Length);
        }

        return Convert.ToBase64String(output.ToArray());
    }

    private static string Decompress(string text) {
        if (string.IsNullOrEmpty(text)) return "{}";

        byte[] bytes = Convert.FromBase64String(text);

        using var input  = new MemoryStream(bytes);
        using var output = new MemoryStream();

        using (var deflate = new DeflateStream(input, CompressionMode.Decompress)) {
            deflate.CopyTo(output);
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    /// <summary>
    /// A serializable representation of a single slot.
    /// </summary>
    public sealed class SlotEntry {
        public DynamicHotbarSlotKind Kind { get; set; }
        public uint                   Id   { get; set; }
    }
}
