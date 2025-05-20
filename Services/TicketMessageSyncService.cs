using System;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;

public class TicketMessageSyncService
{
    private readonly DiscordSocketClient _client;
    private readonly TicketDbContext _dbContext;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public TicketMessageSyncService(DiscordSocketClient client)
    {
        _client = client;
        _dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);

        var messageSyncHandler = new MessageSyncHandler(_client, _dbContext);
        var reopenedTicketHandler = new ReopenedTicketHandler(_client);
        var closedTicketHandler = new ClosedTicketHandler(_client);

        Task.Run(() => messageSyncHandler.SyncMessagesToDiscordAsync(_cancellationTokenSource.Token));
        Task.Run(() => reopenedTicketHandler.CheckForReopenedTickets(_cancellationTokenSource.Token));
        Task.Run(() => closedTicketHandler.CheckForClosedTickets(_cancellationTokenSource.Token));
    }

    private string GetDiscordAvatarUrl(ulong discordUserId)
    {
        var user = _client.GetUser(discordUserId);
        if (user != null && user.GetAvatarUrl() != null)
        {
            return user.GetAvatarUrl(Discord.ImageFormat.Png, 256);
        }

        return "https://i.imgur.com/dnlokbX.png";
    }
}
