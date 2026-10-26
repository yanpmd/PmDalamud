using System.Linq;
using Dalamud.Plugin.Ipc;

namespace QoLBar;

public static class IPC
{
    public const int IPCVersion = 1;
    public static readonly ICallGateProvider<object> InitializedProvider = DalamudApi.PluginInterface.GetIpcProvider<object>("QoLBarCN.Initialized");
    public static readonly ICallGateProvider<object> DisposedProvider = DalamudApi.PluginInterface.GetIpcProvider<object>("QoLBarCN.Disposed");
    public static readonly ICallGateProvider<string> GetVersionProvider = DalamudApi.PluginInterface.GetIpcProvider<string>("QoLBarCN.GetVersion");
    public static readonly ICallGateProvider<int> GetIPCVersionProvider = DalamudApi.PluginInterface.GetIpcProvider<int>("QoLBarCN.GetIPCVersion");
    public static readonly ICallGateProvider<string, object> ImportBarProvider = DalamudApi.PluginInterface.GetIpcProvider<string, object>("QoLBarCN.ImportBar");
    public static readonly ICallGateProvider<string[]> GetConditionSetsProvider = DalamudApi.PluginInterface.GetIpcProvider<string[]>("QoLBarCN.GetConditionSets");
    public static readonly ICallGateProvider<int, bool> CheckConditionSetProvider = DalamudApi.PluginInterface.GetIpcProvider<int, bool>("QoLBarCN.CheckConditionSet");
    public static readonly ICallGateProvider<int, int, object> MovedConditionSetProvider = DalamudApi.PluginInterface.GetIpcProvider<int, int, object>("QoLBarCN.MovedConditionSet");
    public static readonly ICallGateProvider<int, object> RemovedConditionSetProvider = DalamudApi.PluginInterface.GetIpcProvider<int, object>("QoLBarCN.RemovedConditionSet");

    public static void Initialize()
    {
        GetVersionProvider.RegisterFunc(() => QoLBar.Config.PluginVersion);
        GetIPCVersionProvider.RegisterFunc(() => IPCVersion);
        ImportBarProvider.RegisterAction(import => QoLBar.Plugin.ui.ImportBar(import));
        GetConditionSetsProvider.RegisterFunc(() => QoLBar.Config.CndSetCfgs.Select(s => s.Name).ToArray());
        CheckConditionSetProvider.RegisterFunc(i => i >= 0 && i < QoLBar.Config.CndSetCfgs.Count && ConditionManager.CheckConditionSet(i));
    }

    public static void Dispose()
    {
        GetVersionProvider.UnregisterFunc();
        GetIPCVersionProvider.UnregisterFunc();
        ImportBarProvider.UnregisterAction();
        GetConditionSetsProvider.UnregisterFunc();
        CheckConditionSetProvider.UnregisterFunc();
    }
}
