using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Serilog;

public class TicketMessageHandler
{
    private readonly TicketDbContext _dbContext;

    public TicketMessageHandler()
    {
        _dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);
    }

    public async Task HandleReceived(SocketUserMessage message)
    {
        if (message.Author.IsBot || message.Channel is not SocketTextChannel channel)
            return;

        if (!channel.Name.StartsWith("ticket-"))
            return;

        if (!int.TryParse(channel.Name.Replace("ticket-", ""), out int ticketId))
            return;

        Log.Information($"📝 Logging new message in Ticket #{ticketId} from {message.Author.Username}");

        try
        {
            var existingMessage = _dbContext.Messages
                .FirstOrDefault(m => m.MessageGroupId == ticketId && m.DiscordMessageId == message.Id);

            if (existingMessage != null)
                return;

            List<string> imgUrls = message.Attachments.Select(a => a.Url).ToList();

            Log.Information($"🖼️ Extracted {imgUrls.Count} images: {string.Join(", ", imgUrls)}");

            string discordAvatarUrl = message.Author.GetAvatarUrl(ImageFormat.Png, 256) ??
                                      "https://cdn.discordapp.com/embed/avatars/0.png";

            string formattedContent = ReplaceMentionsWithUsernames(message);

            var userProfile = _dbContext.UserProfiles.FirstOrDefault(u => u.ZLGMember.DiscordId == message.Author.Id.ToString());
            int? userProfileId = userProfile?.Id ?? 1;

            var newMessage = new Message
            {
                DiscordMessageId = message.Id,
                MessageGroupId = ticketId,
                UserProfileId = userProfileId,
                DiscordUserId = message.Author.Id,
                DiscordUserName = message.Author.Username,
                DiscordImgUrl = discordAvatarUrl,
                Content = formattedContent,
                CreatedAt = message.Timestamp.UtcDateTime,
                ImgUrlsJson = System.Text.Json.JsonSerializer.Serialize(imgUrls),
                SentToDiscord = true
            };

            Log.Information($"📝 Saving message: Content='{newMessage.Content}', Images={string.Join(", ", newMessage.ImgUrls)}");

            _dbContext.Messages.Add(newMessage);
            await _dbContext.SaveChangesAsync();

            Log.Information($"✅ Message logged successfully for Ticket #{ticketId}");
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Error logging message: {ex.Message}");
            if (ex.InnerException != null)
            {
                Log.Information($"🔍 Inner Exception: {ex.InnerException.Message}");
            }
        }
    }

    public async Task HandleUpdated(SocketMessage after, ISocketMessageChannel channel)
    {
        if (after.Author.IsBot || channel is not SocketTextChannel textChannel || !textChannel.Name.StartsWith("ticket-"))
            return;

        if (!int.TryParse(textChannel.Name.Replace("ticket-", ""), out int ticketId))
            return;

        Log.Information($"✏️ Updating message in Ticket #{ticketId} from {after.Author.Username}");

        var existingMessage = _dbContext.Messages.FirstOrDefault(m => m.DiscordMessageId == after.Id);
        if (existingMessage == null)
            return;

        existingMessage.Content = after.Content;
        existingMessage.CreatedAt = after.EditedTimestamp?.UtcDateTime ?? existingMessage.CreatedAt;

        await _dbContext.SaveChangesAsync();
    }

    public async Task HandleDeleted(ulong messageId)
    {
        var existingMessage = _dbContext.Messages.FirstOrDefault(m => m.DiscordMessageId == messageId);
        if (existingMessage == null)
            return;

        Log.Information($"❌ Deleting message from Ticket #{existingMessage.MessageGroupId}");

        _dbContext.Messages.Remove(existingMessage);
        await _dbContext.SaveChangesAsync();
    }

    private string ReplaceMentionsWithUsernames(SocketUserMessage message)
    {
        string content = message.Content;

        foreach (var mentionedUser in message.MentionedUsers)
        {
            string mentionTag = $"<@{mentionedUser.Id}>";
            string usernameTag = $"@{mentionedUser.Username}";
            content = content.Replace(mentionTag, usernameTag);
        }

        return content;
    }
}
