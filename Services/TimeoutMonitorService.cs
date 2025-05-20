using System.Threading.Tasks;
using Discord.WebSocket;

public class TimeoutMonitorService
{
    private readonly DiscordSocketClient _client;
    private readonly TimeoutHandler _timeoutHandler;

    public TimeoutMonitorService(DiscordSocketClient client, BotConfig config)
    {
        _client = client;
        _timeoutHandler = new TimeoutHandler(config);

        _client.MessageReceived += OnMessageReceived;
    }

    private async Task OnMessageReceived(SocketMessage rawMessage)
    {
        await _timeoutHandler.Handle(rawMessage, _client);
    }
}
