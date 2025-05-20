using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

public class TicketMessageListener
{
    private readonly DiscordSocketClient _client;
    private readonly TicketMessageHandler _handler;

    public TicketMessageListener(DiscordSocketClient client)
    {
        _client = client;
        _handler = new TicketMessageHandler();

        _client.MessageReceived += OnMessageReceived;
        _client.MessageUpdated += OnMessageUpdated;
        _client.MessageDeleted += OnMessageDeleted;
    }

    private async Task OnMessageReceived(SocketMessage rawMessage)
    {
        if (rawMessage is SocketUserMessage userMessage)
        {
            await _handler.HandleReceived(userMessage);
        }
    }

    private async Task OnMessageUpdated(Cacheable<IMessage, ulong> _, SocketMessage after, ISocketMessageChannel channel)
    {
        await _handler.HandleUpdated(after, channel);
    }

    private async Task OnMessageDeleted(Cacheable<IMessage, ulong> cacheable, Cacheable<IMessageChannel, ulong> _)
    {
        await _handler.HandleDeleted(cacheable.Id);
    }
}
