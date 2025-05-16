using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

public class TimeoutMonitorService
{
    private readonly DiscordSocketClient _client;
    private readonly BotConfig _config;

    public TimeoutMonitorService(DiscordSocketClient client, BotConfig config)
    {
        _client = client;
        _config = config;

        _client.MessageReceived += OnMessageReceived;
    }

private async Task OnMessageReceived(SocketMessage rawMessage)
{
    // Ignore messages from bots
    if (rawMessage.Author.IsBot) return;

    // Ensure the message is from a user in a guild text channel
    if (rawMessage is not SocketUserMessage message) return;
    if (message.Channel is not SocketTextChannel textChannel) return;

    // Cast the author to a guild user to access role information
    if (message.Author is not SocketGuildUser user) return;

    // Check if the author has the admin role
    bool hasAdminRole = user.Roles.Any(role => role.Id.ToString() == _config.AdminRole);

    // Check if the author is in the list of admin user IDs
    bool isListedAdmin = _config.Admins.Contains(user.Id.ToString());

    // If the author is an admin by role or by user ID, do not proceed with timeout actions
    if (hasAdminRole || isListedAdmin) return;

    // Retrieve mentioned roles and users in the message
    var mentionedRoleIds = message.MentionedRoles.Select(r => r.Id.ToString());
    var mentionedUserIds = message.MentionedUsers.Select(u => u.Id.ToString()).ToList();

    // If it's a reply, exclude the original message author from mentioned users
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

    // Check if the message mentions the admin role or any specific admin users
    bool mentionsAdminRole = mentionedRoleIds.Contains(_config.AdminRole);
    bool mentionsManualAdmin = _config.Admins.Intersect(mentionedUserIds).Any();

    // If the message mentions admins and the author is not an admin, take action
    if (mentionsAdminRole || mentionsManualAdmin)
    {
        // Delete the message
        await message.DeleteAsync();

        // Apply a 12-hour timeout to the user
        TimeSpan timeoutDuration = TimeSpan.FromHours(12);
        await user.SetTimeOutAsync(timeoutDuration);

        // Send a notification to the user in the channel
        await textChannel.SendMessageAsync(
            $"<@{message.Author.Id}>, your message was removed for pinging admins. Please review the rules. You’ve been placed on a 12-hour timeout.",
            allowedMentions: AllowedMentions.None
        );

        // Log the action in the admin channel
        var guild = user.Guild;
        var adminChannel = guild.GetTextChannel(ulong.Parse(_config.AdminChannelId));
        if (adminChannel != null)
        {
            var embed = new EmbedBuilder()
                .WithAuthor(user)
                .WithThumbnailUrl("https://i.imgur.com/dnlokbX.png")
                .WithColor(Color.Green)
                .AddField("Disciplinary Action", "Timeout", true)
                .AddField("Duration", "12 hours", true)
                .AddField("Reason", "Mentioned admin", true)
                .WithCurrentTimestamp()
                .Build();

            await adminChannel.SendMessageAsync(embed: embed);
        }
    }
}

}
