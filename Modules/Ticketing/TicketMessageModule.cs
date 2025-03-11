using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

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

        Console.WriteLine($"📝 Logging new message in Ticket #{ticketId} from {message.Author.Username}");

        try
        {
            // Check if message already exists
            var existingMessage = _dbContext.Messages
                .FirstOrDefault(m => m.MessageGroupId == ticketId && m.DiscordMessageId == message.Id);

            if (existingMessage != null)
                return;

            // Extract image URL if present
            string? imgUrl = message.Attachments.FirstOrDefault()?.Url;

            // 🔍 Find UserProfileId using Discord ID from ZLGMembers
            var zlgMember = _dbContext.ZLGMembers.FirstOrDefault(m => m.DiscordId == message.Author.Id.ToString());
            int? userProfileId = zlgMember?.UserProfileId;

            Console.WriteLine($"🔍 Found user: {message.Author.Username}, DiscordId: {message.Author.Id}, UserProfileId: {(userProfileId.HasValue ? userProfileId.Value.ToString() : "NULL")}");

            // ✅ Create and save the message (DO NOT include UserProfileId if it's NULL)
            var newMessage = new Message
            {
                DiscordMessageId = message.Id,
                MessageGroupId = ticketId,
                DiscordUserId = message.Author.Id,
                DiscordUserName = message.Author.Username,
                Content = message.Content,
                CreatedAt = message.Timestamp.UtcDateTime,
                ImgUrl = imgUrl,
                SentToDiscord = true
            };

            if (userProfileId.HasValue)
            {
                newMessage.UserProfileId = userProfileId.Value; // Only set if not NULL
            }

            _dbContext.Messages.Add(newMessage);
            await _dbContext.SaveChangesAsync();
            Console.WriteLine($"✅ Message logged successfully for Ticket #{ticketId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error logging message: {ex.Message}");
        }
    }

    private async Task HandleMessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
    {
        if (after.Author.IsBot || channel is not SocketTextChannel textChannel || !textChannel.Name.StartsWith("ticket-"))
            return;

        if (!int.TryParse(textChannel.Name.Replace("ticket-", ""), out int ticketId))
            return;

        Console.WriteLine($"✏️ Updating message in Ticket #{ticketId} from {after.Author.Username}");

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

        Console.WriteLine($"❌ Deleting message from Ticket #{existingMessage.MessageGroupId}");

        // Remove the message from database
        _dbContext.Messages.Remove(existingMessage);
        await _dbContext.SaveChangesAsync();
    }
}
