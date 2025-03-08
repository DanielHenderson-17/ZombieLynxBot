using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

public class TicketMessageSyncService
{
    private readonly DiscordSocketClient _client;
    private readonly TicketDbContext _dbContext;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public TicketMessageSyncService(DiscordSocketClient client)
    {
        _client = client;
        _dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);

        Task.Run(() => SyncMessagesToDiscordAsync(_cancellationTokenSource.Token));
    }

    private async Task SyncMessagesToDiscordAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 🔍 Fetch messages that haven't been sent to Discord yet
                var unsentMessages = _dbContext.Messages
                    .Where(m => !m.SentToDiscord)
                    .OrderBy(m => m.CreatedAt)
                    .ToList();

                foreach (var msg in unsentMessages)
                {
                    // Find the corresponding ticket channel
                    var guild = _client.Guilds.FirstOrDefault();
                    if (guild == null) continue;

                    var channelName = $"ticket-{msg.MessageGroupId}";
                    var channel = guild.TextChannels.FirstOrDefault(c => c.Name == channelName);
                    if (channel == null)
                    {
                        Console.WriteLine($"⚠️ Ticket channel '{channelName}' not found!");
                        continue;
                    }

                    // Format the message
                    var embed = new EmbedBuilder()
                        .WithAuthor(msg.DiscordUserName ?? "Unknown User", "https://i.imgur.com/dnlokbX.png")
                        .WithDescription($"{msg.Content}")
                        .WithColor(Color.Blue)
                        .Build();

                    // Send message to Discord
                    await channel.SendMessageAsync(embed: embed);

                    // ✅ Mark message as sent
                    msg.SentToDiscord = true;
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error syncing messages: {ex.Message}");
            }

            await Task.Delay(5000); // Check every 5 seconds
        }
    }
}
