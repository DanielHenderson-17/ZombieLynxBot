using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

public static class TicketEmbedUtils
{
    public static async Task<IUserMessage?> FindTicketEmbedMessageAsync(ISocketMessageChannel channel)
    {
        var messages = await channel.GetMessagesAsync(limit: 20).FlattenAsync();

        return messages
            .OfType<IUserMessage>()
            .FirstOrDefault(m =>
                m.Embeds.Any() &&
                m.Author.IsBot &&
                m.Embeds.First().Author != null &&
                !string.IsNullOrWhiteSpace(m.Embeds.First().Author?.Name));
    }
}
