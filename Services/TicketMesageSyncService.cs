using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Serilog;

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
        Task.Run(() => CheckForReopenedTickets(_cancellationTokenSource.Token));
        Task.Run(() => CheckForClosedTickets(_cancellationTokenSource.Token));
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
                        Log.Information($"⚠️ Ticket channel '{channelName}' not found!");
                        continue;
                    }

                    // ✅ Format timestamp and username
                    string timestamp = msg.CreatedAt.ToString("HH:mm");
                    string username = CapitalizeFirstLetter(msg.DiscordUserName) ?? "Unknown User";

                    // ✅ Convert @Username mentions to <@UserID> format
                    string formattedContent = await ReplaceUserMentions(msg.Content, guild);

                    // ✅ Final formatted message (no embed)
                    string finalMessage = $"[{timestamp}] {username}: {formattedContent}";

                    // Send text message
                    await channel.SendMessageAsync(finalMessage);

                    // ✅ Send images separately (so they embed properly in Discord)
                    if (!string.IsNullOrEmpty(msg.ImgUrlsJson))
                    {
                        var imageUrls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(msg.ImgUrlsJson);
                        foreach (var imageUrl in imageUrls)
                        {
                            await channel.SendMessageAsync(imageUrl);
                        }
                    }

                    // ✅ Mark message as sent
                    msg.SentToDiscord = true;
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Information($"❌ Error syncing messages: {ex.Message}");
            }

            await Task.Delay(5000);
        }
    }

    private static string CapitalizeFirstLetter(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return char.ToUpper(input[0]) + input.Substring(1);
    }
    private string GetDiscordAvatarUrl(ulong discordUserId)
    {
        var user = _client.GetUser(discordUserId);
        if (user != null && user.GetAvatarUrl() != null)
        {
            return user.GetAvatarUrl(ImageFormat.Png, 256);
        }

        return "https://i.imgur.com/dnlokbX.png";
    }
    private async Task CheckForReopenedTickets(CancellationToken cancellationToken)
    {
        var channelManager = new TicketChannelManager(_client);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using (var dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider))
                {
                    Log.Information("🔍 Checking for reopened tickets...");

                    var reopenedTickets = _dbContext.Tickets
                        .Where(t => t.Status == "Open")
                        .ToList();

                    Log.Information($"🔍 Checking for reopened tickets... Found {reopenedTickets.Count}");

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

                        await channelManager.HandleTicketReopen(ticket.Id);
                        Log.Information($"✅ Finished processing Ticket #{ticket.Id}");
                    }


                    if (reopenedTickets.Any())
                    {
                        Log.Information($"✅ Found {reopenedTickets.Count} reopened tickets: " +
                            string.Join(", ", reopenedTickets.Select(t => t.Id)));
                    }
                    else
                    {
                        Log.Information("⚠️ No reopened tickets found.");
                    }

                }

            }
            catch (Exception ex)
            {
                Log.Information($"❌ Error checking reopened tickets: {ex.Message}");
            }

            await Task.Delay(10000); // Check every 10 seconds
        }
    }

    private async Task CheckForClosedTickets(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using (var dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider))
                {
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

                            // ✅ Send closure message to the channel
                            await channel.SendMessageAsync("✅ Ticket has been closed. The channel will be deleted in 10 seconds.");

                            // ✅ Wait for 10 seconds before deleting
                            await Task.Delay(10000, cancellationToken);

                            await channel.DeleteAsync();
                        }
                        else
                        {
                            Log.Information($"⚠️ Could not find Discord channel {channelId} for Ticket #{ticket.Id}.");
                        }

                        // ✅ Remove the DiscordChannelId from the ticket to mark it as processed
                        ticket.DiscordChannelId = null;
                        await dbContext.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Information($"❌ Error checking closed tickets: {ex.Message}");
            }

            await Task.Delay(10000); // Check every 10 seconds
        }
    }

    private async Task<string> ReplaceUserMentions(string content, SocketGuild guild)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        foreach (var user in guild.Users)
        {
            string usernameMention = $"@{user.Username}";
            string discordMention = $"<@{user.Id}>";

            // ✅ Replace @Username with <@UserID> for actual Discord pings
            if (content.Contains(usernameMention))
            {
                content = content.Replace(usernameMention, discordMention);
            }
        }

        return content;
    }
}
