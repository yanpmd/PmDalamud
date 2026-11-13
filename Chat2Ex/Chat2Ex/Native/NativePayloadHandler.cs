using Chat2Ex.Util;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Chat2ExPartyFinderPayload = Chat2Ex.Util.PartyFinderPayload;
using DalamudPartyFinderPayload = Dalamud.Game.Text.SeStringHandling.Payloads.PartyFinderPayload;

namespace Chat2Ex.Native;

/// <summary>
///     Native counterpart of the clickable parts of <c>Ui/Handler/PayloadHandler</c>.
/// </summary>
public static class NativePayloadHandler
{
    /// <summary>
    ///     Whether a left click on this payload has an action.
    /// </summary>
    public static bool CanHandle(Payload? payload) => payload switch
    {
        ItemPayload => true,
        MapLinkPayload => true,
        QuestPayload => true,
        Chat2ExPartyFinderPayload => true,
        DalamudPartyFinderPayload => true,
        AchievementPayload => true,
        RawPayload => true,
        UriPayload => true,
        PlayerPayload => true,
        DalamudLinkPayload => true,
        _ => false,
    };

    /// <summary>
    ///     Runs the left click action for the payload attached to the chunk.
    /// </summary>
    public static bool TryHandleLeftClick(Chunk chunk)
    {
        try
        {
            return HandleLeftClick(chunk);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Chat2Ex: error while handling a payload click");
            return false;
        }
    }

    private static bool HandleLeftClick(Chunk chunk)
    {
        switch (chunk.Link)
        {
            case ItemPayload item:
                GameFunctions.GameFunctions.OpenItemTooltip(item.RawItemId, item.Kind);
                return true;

            case MapLinkPayload map:
                Plugin.GameGui.OpenMapWithMapLink(map);
                return true;

            case QuestPayload quest:
                GameFunctions.GameFunctions.OpenQuestLog(quest.Quest);
                return true;

            case Chat2ExPartyFinderPayload partyFinder:
                GameFunctions.GameFunctions.OpenPartyFinder(partyFinder.Id);
                return true;

            case DalamudPartyFinderPayload partyFinder:
                // A notification only points at the party finder itself, every other link points at a
                // specific listing.
                if (partyFinder.LinkType == DalamudPartyFinderPayload.PartyFinderLinkType.PartyFinderNotification)
                    GameFunctions.GameFunctions.OpenPartyFinder();
                else
                    GameFunctions.GameFunctions.OpenPartyFinder(partyFinder.ListingId);

                return true;

            case AchievementPayload achievement:
                GameFunctions.GameFunctions.OpenAchievement(achievement.Id);
                return true;

            case RawPayload raw:
                if (Equals(raw, ChunkUtil.PeriodicRecruitmentLink))
                    GameFunctions.GameFunctions.OpenPartyFinder();

                return true;

            case UriPayload uri:
                WrapperUtil.TryOpenUri(uri.Uri);
                return true;

            case DalamudLinkPayload link:
                ClickLinkPayload(chunk, link);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    ///     Runs the handler another plugin registered for a Dalamud link.
    /// </summary>
    private static void ClickLinkPayload(Chunk chunk, DalamudLinkPayload link)
    {
        if (chunk.GetSeString() is not { } source)
            return;

        var start = source.Payloads.IndexOf(link);
        var end = source.Payloads.IndexOf(RawPayload.LinkTerminator, start == -1 ? 0 : start);

        if (start == -1 || end == -1)
            return;

        var payloads = source.Payloads.Skip(start).Take(end - start + 1).ToList();

        if (!Plugin.ChatGui.RegisteredLinkHandlers.TryGetValue((link.Plugin, link.CommandId), out var handler))
        {
            Plugin.Log.Warning($"Could not find the Dalamud link handler for '{link.Plugin}'");
            return;
        }

        try
        {
            // Running the handler instantly leads to a game freeze, so it has to be deferred.
            Plugin.Framework.RunOnTick(() => handler(link.CommandId, new SeString(payloads)));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Error executing the Dalamud link handler");
        }
    }
}
