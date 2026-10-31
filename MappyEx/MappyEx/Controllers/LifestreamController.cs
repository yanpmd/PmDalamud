using Dalamud.Plugin.Ipc;

namespace Mappy.Controllers;

/// <summary>
/// Optional integration with the Lifestream plugin.
/// Every entry point reports whether the plugin was reachable through <c>available</c>, so callers can decide between
/// "the request was rejected" and "Lifestream is not installed, fall back to the built in teleporter".
/// </summary>
public static class LifestreamController
{
    private static ICallGateSubscriber<uint, bool>? aethernetTeleportById;
    private static ICallGateSubscriber<uint, byte, bool>? teleport;
    private static ICallGateSubscriber<uint>? getActiveAetheryte;

    private static void EnsureInitialized()
    {
        aethernetTeleportById ??= Service.PluginInterface.GetIpcSubscriber<uint, bool>("Lifestream.AethernetTeleportById");
        teleport ??= Service.PluginInterface.GetIpcSubscriber<uint, byte, bool>("Lifestream.Teleport");
        getActiveAetheryte ??= Service.PluginInterface.GetIpcSubscriber<uint>("Lifestream.GetActiveAetheryte");
    }

    /// <summary>
    /// Aetheryte the player is currently standing next to, 0 when the player is not in range of any of them.
    /// This is the same condition Lifestream uses to accept an aethernet teleport.
    /// </summary>
    public static uint GetActiveAetheryte()
    {
        try
        {
            EnsureInitialized();
            return getActiveAetheryte!.InvokeFunc();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Move to an aethernet shard inside the current area.
    /// The id variant is preferred, it resolves the destination through Lifestream's name overrides instead of
    /// matching the raw PlaceName text, which does not always equal the entry shown in the aethernet menu.
    /// </summary>
    /// <param name="aetheryteRowId">Aetheryte sheet row id of the shard, not the master aetheryte</param>
    /// <param name="available">true when Lifestream is installed, independent of whether it accepted the request</param>
    public static bool TryAethernetTeleportById(uint aetheryteRowId, out bool available)
    {
        available = false;

        try
        {
            EnsureInitialized();

            var result = aethernetTeleportById!.InvokeFunc(aetheryteRowId);

            // Reaching this point proves the plugin is loaded, the exception path below means it is not.
            available = true;

            return result;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Regular teleport to an aetheryte, routed through Lifestream so it can handle server changes and queues.
    /// </summary>
    /// <param name="aetheryteId">Aetheryte sheet row id</param>
    /// <param name="available">true when Lifestream is installed, independent of whether it accepted the request</param>
    public static bool TryTeleport(uint aetheryteId, out bool available)
    {
        available = false;

        try
        {
            EnsureInitialized();

            var result = teleport!.InvokeFunc(aetheryteId, 0);

            available = true;

            return result;
        }
        catch
        {
            return false;
        }
    }
}
