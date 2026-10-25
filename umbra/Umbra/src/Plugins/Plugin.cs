using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Umbra.Plugins.Repository;

namespace Umbra.Plugins;

internal class Plugin : IDisposable
{
    public PluginEntry Entry      { get; private set; }
    public FileInfo    File       { get; private set; }
    public Assembly?   Assembly   { get; private set; }
    public bool        IsDisposed { get; private set; }

    private PluginLoadContext? _context;
    private FileSystemWatcher? _watcher;
    private bool               _restartScheduled;

    public Plugin(PluginEntry entry)
    {
        Entry = entry;
        File  = new(Entry.FilePath);

        if (Entry.Type == PluginEntry.PluginType.LocalFile) {
            EnableFileWatcher();
        }
    }

    public void Load()
    {
        if (!File.Exists) {
            Entry.LoadError = $"File not found: {File.FullName}";
            return;
        }

        try {
            _context = new(File.Directory!);
            Assembly = _context.LoadFromFile(File.FullName);

            Framework.Assemblies.Add(Assembly);
            UdtLoader.AddAssembly(Assembly);
        } catch (Exception e) {
            Entry.LoadError = e.Message;
            Dispose();
            Logger.Error($"Failed to load plugin: {File.FullName}: {e.Message}");
        }
    }

    public void Dispose()
    {
        if (Assembly != null) {
            Framework.Assemblies.Remove(Assembly);
            UdtLoader.RemoveAssembly(Assembly);
        }

        _context?.Unload();
        _watcher?.Dispose();
        Assembly   = null;
        IsDisposed = true;
    }

    private void EnableFileWatcher()
    {
        string? directory = File.DirectoryName;

        // A stale entry may point at a path that no longer exists (deleted
        // build output, unplugged drive, ...). Watching it would throw and
        // abort the entire startup, so simply skip the watcher instead.
        if (directory is null || !Directory.Exists(directory)) {
            Logger.Warning($"Not watching plugin file, its directory does not exist: {File.FullName}");

            return;
        }

        try {
            _watcher = new FileSystemWatcher(directory) {
                Filter               = File.Name,
                EnableRaisingEvents = true,
            };

            _watcher.Changed += (_, _) => RestartDelayed().ContinueWith(_ => { });
        } catch (Exception e) {
            // Insufficient permissions, path too long, network share, etc.
            _watcher?.Dispose();
            _watcher = null;

            Logger.Warning($"Failed to watch plugin file: {File.FullName}: {e.Message}");
        }
    }

    private async Task RestartDelayed()
    {
        if (_restartScheduled) return;

        Logger.Info($"Plugin file changed: {File.FullName}");

        _restartScheduled = true;
        _watcher?.Dispose();

        await Task.Delay(1000);
        await Framework.Restart();
    }
}
