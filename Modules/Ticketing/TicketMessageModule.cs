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

        // Ensure the message is inside a ticket channel
        if (!channel.Name.StartsWith("ticket-"))
            return;

        // Extract ticket ID from channel name
        if (!int.TryParse(channel.Name.Replace("ticket-", ""), out int ticketId))
            return;

        Console.WriteLine($"📝 Logging new message in Ticket #{ticketId} from {message.Author.Username}");

        // Check for existing message in database
        var existingMessage = _dbContext.Messages
            .FirstOrDefault(m => m.MessageGroupId == ticketId && m.DiscordMessageId == message.Id);

        if (existingMessage != null)
            return;

        // Extract image URL if any
        string? imgUrl = message.Attachments.FirstOrDefault()?.Url;

        // 🔍 Try to find UserProfileId using Discord ID from ZLGMembers
        var zlgMember = _dbContext.ZLGMembers.FirstOrDefault(m => m.DiscordId == message.Author.Id.ToString());
        int? userProfileId = zlgMember?.UserProfileId; // Null if not found

        // ✅ Create and save the message
        var newMessage = new Message
        {
            DiscordMessageId = message.Id,
            MessageGroupId = ticketId,
            UserProfileId = zlgMember?.UserProfileId ?? null,
            DiscordUserId = message.Author.Id,
            DiscordUserName = message.Author.Username,
            Content = message.Content,
            CreatedAt = message.Timestamp.UtcDateTime,
            ImgUrl = imgUrl,
            SentToDiscord = true // ✅ Mark message as already sent so bot doesn't resend
        };

        _dbContext.Messages.Add(newMessage);
        await _dbContext.SaveChangesAsync();
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
