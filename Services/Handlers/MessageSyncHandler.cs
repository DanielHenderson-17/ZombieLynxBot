using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Serilog;

public class MessageSyncHandler
{
    private readonly DiscordSocketClient _client;
    private readonly TicketDbContext _dbContext;

    public MessageSyncHandler(DiscordSocketClient client, TicketDbContext dbContext)
    {
        _client = client;
        _dbContext = dbContext;
    }

    public async Task SyncMessagesToDiscordAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var unsentMessages = _dbContext.Messages
                    .Where(m => !m.SentToDiscord)
                    .OrderBy(m => m.CreatedAt)
                    .ToList();

                foreach (var msg in unsentMessages)
                {
                    var guild = _client.Guilds.FirstOrDefault();
                    if (guild == null) continue;

                    var channelName = $"ticket-{msg.MessageGroupId}";
                    var channel = guild.TextChannels.FirstOrDefault(c => c.Name == channelName);
                    if (channel == null)
                    {
                        Log.Information($"⚠️ Ticket channel '{channelName}' not found!");
                        continue;
                    }

                    string timestamp = msg.CreatedAt.ToString("HH:mm");
                    string username = DiscordFormatUtils.CapitalizeFirstLetter(msg.DiscordUserName) ?? "Unknown User";

                    string formattedContent = await ReplaceUserMentions(msg.Content, guild);
                    string finalMessage = $"[{timestamp}] {username}: {formattedContent}";

                    await channel.SendMessageAsync(finalMessage);

                    if (!string.IsNullOrEmpty(msg.ImgUrlsJson))
                    {
                        var imageUrls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(msg.ImgUrlsJson);
                        foreach (var imageUrl in imageUrls)
                        {
                            await channel.SendMessageAsync(imageUrl);
                        }
                    }

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

    private async Task<string> ReplaceUserMentions(string content, SocketGuild guild)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        foreach (var user in guild.Users)
        {
            string usernameMention = $"@{user.Username}";
            string discordMention = $"<@{user.Id}>";

            if (content.Contains(usernameMention))
            {
                content = content.Replace(usernameMention, discordMention);
            }
        }

        return content;
    }
}
