using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

public class TimeoutHandler
{
    private readonly BotConfig _config;

    public TimeoutHandler(BotConfig config)
    {
        _config = config;
    }

    public async Task Handle(SocketMessage rawMessage, DiscordSocketClient client)
    {
        if (rawMessage.Author.IsBot) return;
        if (rawMessage is not SocketUserMessage message) return;
        if (message.Channel is not SocketTextChannel textChannel) return;
        if (message.Author is not SocketGuildUser user) return;

        bool hasAdminRole = user.Roles.Any(role => role.Id.ToString() == _config.AdminRole);
        bool isListedAdmin = _config.Admins.Contains(user.Id.ToString());

        if (hasAdminRole || isListedAdmin) return;

        var mentionedRoleIds = message.MentionedRoles.Select(r => r.Id.ToString());
        var mentionedUserIds = message.MentionedUsers.Select(u => u.Id.ToString()).ToList();

        if (message.Reference != null && message.Reference.MessageId.IsSpecified)
        {
            var referencedMessage = await textChannel.GetMessageAsync(message.Reference.MessageId.Value);
            if (referencedMessage != null)
            {
                mentionedUserIds = mentionedUserIds
                    .Where(id => id != referencedMessage.Author.Id.ToString())
                    .ToList();
            }
        }

        bool mentionsAdminRole = mentionedRoleIds.Contains(_config.AdminRole);
        bool mentionsManualAdmin = _config.Admins.Intersect(mentionedUserIds).Any();

        if (mentionsAdminRole || mentionsManualAdmin)
        {
            await message.DeleteAsync();
            await user.SetTimeOutAsync(TimeSpan.FromHours(12));

            await textChannel.SendMessageAsync(
                $"<@{message.Author.Id}>, your message was removed for pinging admins. Please review the rules. You’ve been placed on a 12-hour timeout.",
                allowedMentions: AllowedMentions.None
            );

            var guild = user.Guild;
            var adminChannel = guild.GetTextChannel(ulong.Parse(_config.AdminChannelId));
            if (adminChannel != null)
            {
                var embed = EmbedBuilderUtils.BuildTimeoutEmbed(user);
                await adminChannel.SendMessageAsync(embed: embed);
            }
        }
    }
}
