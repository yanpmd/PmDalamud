using Chat2Ex.Http.MessageProtocol;
using Chat2Ex.Ui.Handler;
using Dalamud.Plugin.Services;

namespace Chat2Ex.Http;

public class ServerCore : IAsyncDisposable
{
    public static readonly HttpClient HttpClient = new();

    public readonly Plugin Plugin;
    public readonly SendHandler SendHandler;
    private readonly HostContext HostContext;

    public ServerCore(Plugin plugin)
    {
        Plugin = plugin;

        SendHandler = new SendHandler(plugin);
        HostContext = new HostContext(this);

        Plugin.Framework.Update += FrameworkUpdate;
    }

    public async ValueTask DisposeAsync()
    {
        HttpClient.Dispose();
        Plugin.Framework.Update -= FrameworkUpdate;
        await HostContext.DisposeAsync();
    }

    private Action? frameworkUpdateCore;

    /// <summary>
    ///     Framework.Update is a game callback; keep the delegate cached so the guard does not
    ///     allocate on every tick.
    /// </summary>
    private void FrameworkUpdate(IFramework _)
        => Chat2Ex.Native.NativeCallback.Safe(frameworkUpdateCore ??= FrameworkUpdateCore, "updating web server state");

    private void FrameworkUpdateCore()
    {
        // Web disabled: nothing to do, and Tabs.Index() used to allocate an iterator every frame.
        if (!HostContext.IsActive)
            return;

        var tabs = Plugin.Config.Tabs;
        for (var idx = 0; idx < tabs.Count; idx++)
        {
            var tab = tabs[idx];
            if (tab.Unread == tab.LastSendUnread)
                continue;

            tab.LastSendUnread = tab.Unread;
            foreach (var eventServer in HostContext.EventConnections)
                eventServer.OutboundQueue.Enqueue(new ChatTabUnreadStateEvent(new ChatTabUnreadState(idx, tab.Unread)));
        }
    }

    #region SSE Helper
    public async Task PrepareNewClient(SSEConnection sse)
    {
        // This takes long, so keep it outside the next frame
        var messages = await HostContext.Processing.GetAllMessages();

        // Using the bulk message event to clear everything on the client side that may still exist.
        // The try/catch around a RunOnTick call only covers queuing; the lambda itself needs a guard
        // or an exception thrown inside the framework tick terminates the process.
        await Plugin.Framework.RunOnTick(() => Chat2Ex.Native.NativeCallback.Safe(() =>
        {
            sse.OutboundQueue.Enqueue(new BulkMessagesEvent(messages));

            sse.OutboundQueue.Enqueue(new SwitchChannelEvent(HostContext.Processing.GetCurrentChannel()));
            sse.OutboundQueue.Enqueue(new ChannelListEvent(HostContext.Processing.GetValidChannels()));

            sse.OutboundQueue.Enqueue(new ChatTabSwitchedEvent(HostContext.Processing.GetCurrentTab()));
            sse.OutboundQueue.Enqueue(new ChatTabListEvent(HostContext.Processing.GetAllTabs()));
        }, "preparing a new web client"));
    }

    public void SendNewMessage(Message message)
    {
        if (!HostContext.IsActive)
            return;

        try
        {
            Plugin.Framework.RunOnTick(() => Chat2Ex.Native.NativeCallback.Safe(() =>
            {
                var bundledResponse = new NewMessageEvent(HostContext.Processing.ReadMessageContent(message));
                foreach (var eventServer in HostContext.EventConnections)
                    eventServer.OutboundQueue.Enqueue(bundledResponse);
            }, "sending a new message over SSE"));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Sending message over SSE failed.");
        }
    }

    public void SendBulkMessageList()
    {
        if (!HostContext.IsActive)
            return;

        try
        {
            Plugin.Framework.RunOnTick(() => Chat2Ex.Native.NativeCallback.Safe(() =>
            {
                foreach (var eventServer in HostContext.EventConnections)
                    eventServer.OutboundQueue.Enqueue(new BulkMessagesEvent(new Messages(HostContext.Processing.ReadMessageList().Result)));
            }, "sending the message list over SSE"));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Sending channel switch over SSE failed.");
        }
    }

    public void SendChannelSwitch(Chunk[] channelName)
    {
        if (!HostContext.IsActive)
            return;

        try
        {
            Plugin.Framework.RunOnTick(() => Chat2Ex.Native.NativeCallback.Safe(() =>
            {
                var bundledResponse = new SwitchChannelEvent(new SwitchChannel(HostContext.Processing.ReadChannelName(channelName)));
                foreach (var eventServer in HostContext.EventConnections)
                    eventServer.OutboundQueue.Enqueue(bundledResponse);
            }, "sending a channel switch over SSE"));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Sending channel switch over SSE failed.");
        }
    }

    public void SendChannelList()
    {
        if (!HostContext.IsActive)
            return;

        try
        {
            Plugin.Framework.RunOnTick(() => Chat2Ex.Native.NativeCallback.Safe(() =>
            {
                var bundledResponse = new ChannelListEvent(HostContext.Processing.GetValidChannels());
                foreach (var eventServer in HostContext.EventConnections)
                    eventServer.OutboundQueue.Enqueue(bundledResponse);
            }, "sending the channel list over SSE"));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Sending channel switch over SSE failed.");
        }
    }

    public void SendNewLogin()
    {
        if (!HostContext.IsActive)
            return;

        try
        {
            // async void lambdas cannot be guarded by an Action wrapper, so the try/catch has to be
            // inside; otherwise an exception from PrepareNewClient terminates the process.
            Plugin.Framework.RunOnTick(async () =>
            {
                try
                {
                    foreach (var eventServer in HostContext.EventConnections)
                        await HostContext.Core.PrepareNewClient(eventServer);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Preparing all clients after login failed.");
                }
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Preparing all clients after login failed.");
        }
    }
    #endregion

    public void InvalidateSessions()
    {
        if (!HostContext.IsActive)
            return;

        Plugin.Config.AuthStore.Clear();
        Plugin.SaveConfig();
    }

    public bool IsActive()
    {
        return HostContext is { IsActive: true, Host.IsListening: true };
    }

    public bool IsStopping()
    {
        return HostContext is { IsActive: false, IsStopping: true };
    }


    public bool Start()
    {
        return HostContext.Start();
    }

    public void Run()
    {
        HostContext.Run();
    }

    public async ValueTask<bool> Stop()
    {
        return await HostContext.Stop();
    }
}