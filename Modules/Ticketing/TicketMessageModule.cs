using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Serilog;

public class TicketMessageModule
{
    private readonly TicketDbContext _dbContext;
    private readonly DiscordSocketClient _client;

    public TicketMessageModule(DiscordSocketClient client)
    {
        _client = client;
        _dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);

        // Register event handlers
        _client.MessageReceived += HandleMessageReceived;
        _client.MessageUpdated += HandleMessageUpdated;
        _client.MessageDeleted += HandleMessageDeleted;
    }

    private async Task HandleMessageReceived(SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage message || message.Author.IsBot || message.Channel is not SocketTextChannel channel)
            return;

        if (!channel.Name.StartsWith("ticket-"))
            return;

        if (!int.TryParse(channel.Name.Replace("ticket-", ""), out int ticketId))
            return;

        Log.Information($"📝 Logging new message in Ticket #{ticketId} from {message.Author.Username}");

        try
        {
            // Check if message already exists
            var existingMessage = _dbContext.Messages
                .FirstOrDefault(m => m.MessageGroupId == ticketId && m.DiscordMessageId == message.Id);

            if (existingMessage != null)
                return;

            // Extract all image URLs from attachments
            List<string> imgUrls = message.Attachments.Select(a => a.Url).ToList();

            // Debugging: Log extracted image URLs
            Log.Information($"🖼️ Extracted {imgUrls.Count} images: {string.Join(", ", imgUrls)}");

            // Get Discord Avatar URL
            string discordAvatarUrl = message.Author.GetAvatarUrl(ImageFormat.Png, 256) ??
                                      "https://cdn.discordapp.com/embed/avatars/0.png";

            // ✅ Replace mentions in message content before saving
            string formattedContent = ReplaceMentionsWithUsernames(message);

            // Find the user profile ID based on Discord ID
            var userProfile = _dbContext.UserProfiles.FirstOrDefault(u => u.ZLGMember.DiscordId == message.Author.Id.ToString());

            // If the user profile is not found, assign a default user profile (Admin or System User)
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

            // Debugging - Log message before saving
            Log.Information($"📝 Saving message: Content='{newMessage.Content}', Images={string.Join(", ", newMessage.ImgUrls)}");

            // Save to database
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

    private async Task HandleMessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
    {
        if (after.Author.IsBot || channel is not SocketTextChannel textChannel || !textChannel.Name.StartsWith("ticket-"))
            return;

        if (!int.TryParse(textChannel.Name.Replace("ticket-", ""), out int ticketId))
            return;

        Log.Information($"✏️ Updating message in Ticket #{ticketId} from {after.Author.Username}");

        // Find the message in the database
        var existingMessage = _dbContext.Messages.FirstOrDefault(m => m.DiscordMessageId == after.Id);
        if (existingMessage == null)
            return;

        // Update the content
        existingMessage.Content = after.Content;
        existingMessage.CreatedAt = after.EditedTimestamp?.UtcDateTime ?? existingMessage.CreatedAt;

        await _dbContext.SaveChangesAsync();
    }

    private async Task HandleMessageDeleted(Cacheable<IMessage, ulong> cacheable, Cacheable<IMessageChannel, ulong> channel)
    {
        var messageId = (int)cacheable.Id;
        var existingMessage = _dbContext.Messages.FirstOrDefault(m => m.DiscordMessageId == cacheable.Id);

        if (existingMessage == null)
            return;

        Log.Information($"❌ Deleting message from Ticket #{existingMessage.MessageGroupId}");

        // Remove the message from database
        _dbContext.Messages.Remove(existingMessage);
        await _dbContext.SaveChangesAsync();
    }
    private string ReplaceMentionsWithUsernames(SocketUserMessage message)
    {
        string content = message.Content;

        // ✅ Find all mentions in the message
        foreach (var mentionedUser in message.MentionedUsers)
        {
            string mentionTag = $"<@{mentionedUser.Id}>";
            string usernameTag = $"@{mentionedUser.Username}";

            // ✅ Replace `<@UserID>` with `@Username`
            content = content.Replace(mentionTag, usernameTag);
        }

        return content;
    }

}
