using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

public class TicketChannelService
{
    private readonly DiscordSocketClient _client;

    public TicketChannelService(DiscordSocketClient client)
    {
        _client = client;
    }

    public async Task<SocketTextChannel?> CreateTicketChannel(SocketGuild guild, SocketUser user, int ticketId, ulong supportCategoryId, ulong supportRoleId)
    {
        string channelName = $"ticket-{ticketId}";

        var restChannel = await guild.CreateTextChannelAsync(channelName, props =>
        {
            props.CategoryId = supportCategoryId;
            props.PermissionOverwrites = new List<Overwrite>
            {
                new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)),
                new Overwrite(user.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow)),
                new Overwrite(supportRoleId, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow))
            };
        });

        // Refetch the channel from the socket cache
        var socketChannel = guild.GetTextChannel(restChannel.Id);
        return socketChannel;
    }
}
