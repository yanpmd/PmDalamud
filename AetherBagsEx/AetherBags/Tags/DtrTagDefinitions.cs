using System.Collections.Generic;
using System.Linq;

namespace AetherBags.Tags;

public static class DtrTagDefinitions
{
    /// <summary>Canonical key of the placeholder entry shown by default in the dropdown.</summary>
    public const string DefaultTagKey = "Insert";

    /// <summary>Canonical tag keys paired with their template text.</summary>
    public static readonly (string Key, string Template)[] Tags =
    [
        ("Insert", string.Empty),
        ("MainUsed", "[used]"),
        ("MainTotal", "[total]"),
        ("MainFree", "[free]"),
        ("MainPercent", "[percent:0]%"),
        ("MainItems", "[items]"),
        ("MainQuantity", "[quantity]"),
        ("MainCategories", "[categories]"),
        ("SaddleUsed", "[saddle.used]"),
        ("SaddleTotal", "[saddle.total]"),
        ("SaddleFree", "[saddle.free]"),
        ("SaddlePercent", "[saddle.percent:0]%"),
    ];

    public static IReadOnlyDictionary<string, string> Templates { get; } =
        Tags.ToDictionary(t => t.Key, t => t.Template);

    public static List<string> GetKeys() => Tags.Select(t => t.Key).ToList();
}
