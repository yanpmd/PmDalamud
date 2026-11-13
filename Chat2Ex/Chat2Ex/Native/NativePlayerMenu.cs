using Chat2Ex.Code;
using Chat2Ex.GameFunctions;
using Chat2Ex.Resources;
using Chat2Ex.Util;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.ImGuiNotification;
using KamiToolKit.ContextMenu;

namespace Chat2Ex.Native;

/// <summary>
///     Builds the player context menu (tell, party, friend request, ...) for a player link.
/// </summary>
public static class NativePlayerMenu
{
    /// <summary>
    ///     Adds the player related entries to the given menu.
    /// </summary>
    /// <param name="menu">The menu to add the entries to.</param>
    /// <param name="plugin">The plugin instance.</param>
    /// <param name="message">The message the player link belongs to.</param>
    /// <param name="player">The player the link points at.</param>
    /// <param name="setChatInput">Fills the chat input with the given text and focuses it.</param>
    public static void Build(ContextMenu menu, Plugin plugin, Message message, PlayerPayload player, Action<string> setChatInput)
    {
        var world = player.World;
        var isCrossWorld = world.Value.IsPublic;
        var worldName = world.Value.Name.ToString();
        var contentId = message.ContentId;

        // The player the link points at, shown as a disabled header entry.
        menu.AddItem(new ContextMenuItem
        {
            Name = isCrossWorld ? $"{player.PlayerName}@{worldName}" : player.PlayerName,
            OnClick = () => { },
            IsEnabled = false,
        });

        menu.AddItem(Language.Context_SendTell, () => Safe(() =>
        {
            var target = isCrossWorld ? $"{player.PlayerName}@{worldName}" : player.PlayerName;
            setChatInput($"/tell {target} ");
        }, "sending a tell"));

        var party = Plugin.PartyList;
        var leader = party.Length > 0 ? party[(int) party.PartyLeaderIndex]?.ContentId ?? 0 : 0;
        var isLeader = party.Length == 0 || Plugin.PlayerState.ContentId == leader;
        var member = party.FirstOrDefault(m => m.Name.ToString() == player.PlayerName && m.World.RowId == world.RowId);
        var isInParty = member is not null;

        if (isLeader && !isInParty)
        {
            var invite = new ContextMenuSubItem { Name = Language.Context_InviteToParty, OnClick = () => { } };

            invite.AddItem(Language.Context_InviteToParty_SameWorld, () => Safe(() => Party.InviteSameWorld(player.PlayerName, (ushort) world.RowId, contentId), "inviting a player"));

            if (contentId != 0)
                invite.AddItem(Language.Context_InviteToParty_DifferentWorld, () => Safe(() => Party.InviteOtherWorld(contentId, (ushort) world.RowId), "inviting a player"));

            menu.AddItem(invite);
        }

        if (isLeader && isInParty && member is not null)
        {
            menu.AddItem(Language.Context_Promote, () => Safe(() => Party.Promote(player.PlayerName, member.ContentId), "promoting a party member"));
            menu.AddItem(Language.Context_KickFromParty, () => Safe(() => Party.Kick(player.PlayerName, member.ContentId), "kicking a party member"));
        }

        var isFriend = GameFunctions.GameFunctions.GetFriends()
            .Any(friend => friend.NameString == player.PlayerName && friend.HomeWorld == world.RowId);

        if (!isFriend)
            menu.AddItem(Language.Context_SendFriendRequest, () => Safe(() => plugin.Functions.SendFriendRequest(player.PlayerName, (ushort) world.RowId), "sending a friend request"));

        if (GameFunctions.GameFunctions.IsMentor())
            menu.AddItem(Language.Context_InviteToNoviceNetwork, () => Safe(() => Context.InviteToNoviceNetwork(player.PlayerName, (ushort) world.RowId), "inviting to the novice network"));

        if (FindCharacter(plugin, player) is { } character)
            menu.AddItem(Language.Context_Target, () => Safe(() => Plugin.TargetManager.Target = character, "targeting a player"));

        if (contentId != 0)
            menu.AddItem(Language.Context_AdventurerPlate, () => Safe(() =>
            {
                if (!GameFunctions.GameFunctions.TryOpenAdventurerPlate(contentId))
                    WrapperUtil.AddNotification(Language.Context_AdventurerPlateError, NotificationType.Warning);
            }, "opening an adventurer plate"));

        var inputChannel = message.Code.Type.ToInputChannel();

        if (inputChannel is { } channel)
            menu.AddItem(Language.Context_ReplyInSelectedChatMode, () => Safe(() =>
            {
                plugin.Functions.Chat.SetChannelWithExtraChat(channel);
                setChatInput(string.Empty);
            }, "replying in the selected channel"));

        var block = new ContextMenuSubItem { Name = Language.Context_BlockFunctions, OnClick = () => { } };

        block.AddItem(Language.Context_AddToBlacklist, () => Safe(() => plugin.Functions.AddToBlacklist(player.PlayerName, (ushort) world.RowId), "adding a player to the blacklist"));

        if (message.AccountId != 0)
            block.AddItem(Language.Context_AddToMuteList, () => Safe(() => plugin.Functions.AddToMuteList(message.AccountId, contentId, player.PlayerName, (short) world.RowId), "adding a player to the mute list"));

        block.AddItem(Language.Context_AddToTermsFilter, () => Safe(() => plugin.Functions.AddToTermsList(message.ContentSource), "adding a message to the filter list"));

        menu.AddItem(block);
    }

    /// <summary>
    ///     Finds the character object of a player link, if the player is currently loaded.
    /// </summary>
    private static IPlayerCharacter? FindCharacter(Plugin plugin, PlayerPayload player)
    {
        foreach (var obj in Plugin.ObjectTable)
        {
            if (obj is not IPlayerCharacter character)
                continue;

            if (character.Name.TextValue != player.PlayerName)
                continue;

            if (player.World.Value.IsPublic && character.HomeWorld.RowId != player.World.RowId)
                continue;

            return character;
        }

        return null;
    }

    /// <summary>
    ///     Runs a menu entry, an exception would end up in the game's menu handling and terminate
    ///     the process.
    /// </summary>
    private static void Safe(Action callback, string context) => NativeCallback.Safe(callback, context);
}
