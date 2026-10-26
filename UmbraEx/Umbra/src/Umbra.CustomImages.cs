using System.IO;

namespace Umbra;

/// <summary>
/// Loads and caches user-provided image files that are used as widget icons.
/// </summary>
internal static class CustomImages
{
    private static readonly Dictionary<string, (DateTime Modified, byte[] Bytes)> Cache = [];

    /// <summary>
    /// Returns the raw bytes of the image at the given path, or null if the
    /// file does not exist or could not be read. Results are cached until the
    /// file is modified.
    /// </summary>
    public static byte[]? GetBytes(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try {
            if (!File.Exists(path)) {
                Cache.Remove(path);

                return null;
            }

            DateTime modified = File.GetLastWriteTimeUtc(path);

            if (Cache.TryGetValue(path, out var cached) && cached.Modified == modified) {
                return cached.Bytes;
            }

            byte[] bytes = File.ReadAllBytes(path);

            Cache[path] = (modified, bytes);

            return bytes;
        } catch (Exception e) {
            Logger.Warning($"Could not load the custom image \"{path}\": {e.Message}");
            Cache.Remove(path);

            return null;
        }
    }
}
