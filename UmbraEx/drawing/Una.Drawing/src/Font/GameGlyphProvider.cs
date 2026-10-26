using System.Net.Http;
using System.Threading.Tasks;

namespace Una.Drawing.Font;

internal static class GameGlyphProvider
{
    private static string GlyphsFontUrl { get; } =
        "https://lds-img.finalfantasyxiv.com/pc/global/fonts/FFXIV_Lodestone_SSF.ttf";

    internal static async Task DownloadGameGlyphs()
    {
        if (GlyphsFile.Exists) return;

        DalamudServices.PluginLog.Info("Downloading game glyphs font. This is a one-time operation.");

        using var client   = new HttpClient();
        using var response = await client.GetAsync(GlyphsFontUrl);

        if (response.IsSuccessStatusCode == false) {
            DalamudServices.PluginLog.Error($"Failed to download game glyphs font from URL: {GlyphsFontUrl}");
            return;
        }

        await using var stream     = await response.Content.ReadAsStreamAsync();
        await using var fileStream = GlyphsFile.OpenWrite();
        await stream.CopyToAsync(fileStream);

        DalamudServices.PluginLog.Info("Game glyphs font downloaded successfully.");
    }

    public static FileInfo GlyphsFile =>
        new(Path.Combine(DalamudServices.PluginInterface.ConfigDirectory.FullName, "Glyphs.ttf"));
}
