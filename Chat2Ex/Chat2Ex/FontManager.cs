using Chat2Ex.Http;

namespace Chat2Ex;

/// <summary>
///     Provides the Lodestone font used by the web interface. The in-game UI is native and always
///     uses the game's own fonts, so no ImGui font building happens anymore.
/// </summary>
public class FontManager
{
    public readonly byte[] GameSymFont;

    public FontManager()
    {
        var filePath = Path.Combine(Plugin.Interface.ConfigDirectory.FullName, "FFXIV_Lodestone_SSF.ttf");
        if (File.Exists(filePath))
        {
            GameSymFont = File.ReadAllBytes(filePath);
        }
        else
        {
            GameSymFont = ServerCore.HttpClient.GetAsync("https://img.finalfantasyxiv.com/lds/pc/global/fonts/FFXIV_Lodestone_SSF.ttf")
                .Result
                .Content
                .ReadAsByteArrayAsync()
                .Result;

            Dalamud.Utility.FilesystemUtil.WriteAllBytesSafe(filePath, GameSymFont);
        }
    }
}
