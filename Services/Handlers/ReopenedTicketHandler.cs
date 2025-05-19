using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using Serilog;

public class ReopenedTicketHandler
{
    private readonly DiscordSocketClient _client;

    public ReopenedTicketHandler(DiscordSocketClient client)
    {
        _client = client;
    }

    public async Task CheckForReopenedTickets(CancellationToken cancellationToken)
    {
        var ticketReopenService = new TicketReopenService(_client);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);

                Log.Information("🔍 Checking for reopened tickets...");

                var reopenedTickets = dbContext.Tickets
                    .Where(t => t.Status == "Open")
                    .ToList();

                Log.Information($"🔍 Found {reopenedTickets.Count} reopened tickets");

                foreach (var ticket in reopenedTickets)
                {
                    Log.Information($"🔄 Processing reopening for Ticket #{ticket.Id}");
                    var guild = _client.Guilds.FirstOrDefault();
                    if (guild != null)
                    {
                        string expectedChannelName = $"ticket-{ticket.Id}";
                        var existingChannel = guild.TextChannels.FirstOrDefault(c => c.Name == expectedChannelName);
                        if (existingChannel != null)
                        {
                            Log.Information($"⛔ Skipping Ticket #{ticket.Id} — channel '{expectedChannelName}' already exists.");
                            continue;
                        }
                    }

                    await ticketReopenService.HandleTicketReopen(ticket.Id);
                    Log.Information($"✅ Finished processing Ticket #{ticket.Id}");
                }

                if (!reopenedTickets.Any())
                {
                    Log.Information("⚠️ No reopened tickets found.");
                }

            }
            catch (Exception ex)
            {
                Log.Information($"❌ Error checking reopened tickets: {ex.Message}");
            }

            await Task.Delay(10000);
        }
    }
}
