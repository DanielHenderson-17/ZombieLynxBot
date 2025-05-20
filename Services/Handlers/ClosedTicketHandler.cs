using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using Serilog;

public class ClosedTicketHandler
{
    private readonly DiscordSocketClient _client;

    public ClosedTicketHandler(DiscordSocketClient client)
    {
        _client = client;
    }

    public async Task CheckForClosedTickets(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);

                Log.Information("🔍 Checking for closed tickets...");

                var closedTickets = dbContext.Tickets
                    .Where(t => t.Status == "Closed" && t.DiscordChannelId != null)
                    .ToList();

                Log.Information($"🔍 Found {closedTickets.Count} closed tickets.");

                foreach (var ticket in closedTickets)
                {
                    ulong channelId = (ulong)ticket.DiscordChannelId;
                    var channel = _client.GetChannel(channelId) as SocketTextChannel;

                    if (channel != null)
                    {
                        Log.Information($"🔴 Closing Discord channel for Ticket #{ticket.Id}.");

                        await channel.SendMessageAsync("✅ Ticket has been closed. The channel will be deleted in 10 seconds.");
                        await Task.Delay(10000, cancellationToken);
                        await channel.DeleteAsync();
                    }
                    else
                    {
                        Log.Information($"⚠️ Could not find Discord channel {channelId} for Ticket #{ticket.Id}.");
                    }

                    ticket.DiscordChannelId = null;
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Information($"❌ Error checking closed tickets: {ex.Message}");
            }

            await Task.Delay(10000);
        }
    }
}
