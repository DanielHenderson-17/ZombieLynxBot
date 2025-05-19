using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Serilog;

public class TicketChannelManager
{
    private readonly DiscordSocketClient _client;
    private readonly TicketDbContext _dbContext;

    public TicketChannelManager(DiscordSocketClient client)
    {
        _client = client;
        _dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);
    }

    public async Task HandleTicketReopen(int ticketId)
    {
        var guild = _client.Guilds.FirstOrDefault();
        if (guild == null)
        {
            Log.Information("❌ No guild found!");
            return;
        }

        string channelName = $"ticket-{ticketId}";
        var existingChannel = guild.TextChannels.FirstOrDefault(c => c.Name == channelName);

        // ✅ If channel already exists, do nothing
        if (existingChannel != null)
        {
            Log.Information($"✅ Channel {channelName} already exists.");
            return;
        }

        // Get the category ID
        ulong? categoryId = null;
        if (Program.Config.SupportCategory.TryGetValue("🔥 General 🔥", out string categoryIdStr) &&
            ulong.TryParse(categoryIdStr, out ulong parsedCategoryId))
        {
            categoryId = parsedCategoryId;
        }

        var categoryChannel = guild.CategoryChannels.FirstOrDefault(c => c.Id == categoryId);

        // Retrieve the ticket from the database
        var ticket = _dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
        {
            Log.Information($"❌ Ticket #{ticketId} not found in the database.");
            return;
        }
        // ✅ Set status back to Open
        ticket.Status = "Open";
        ticket.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var supportRoleId = Convert.ToUInt64(Program.Config.SupportRole["Help!"]);

        // ✅ Create the ticket channel
        var newRestChannel = await guild.CreateTextChannelAsync(channelName, options =>
        {
            options.CategoryId = categoryChannel?.Id;
            options.Topic = $"Ticket #{ticketId}";
            options.PermissionOverwrites = new System.Collections.Generic.List<Overwrite>
            {
            new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)),
            new Overwrite(ticket.DiscordUserId ?? 0, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow)),
            new Overwrite(supportRoleId, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow))
            };
        });

        var newChannel = guild.GetTextChannel(newRestChannel.Id);
        if (newChannel == null)
        {
            Log.Information($"⚠️ Failed to retrieve newly created channel {channelName}.");
            return;
        }

        Log.Information($"✅ Created new channel {newChannel.Name}.");
        ticket.DiscordChannelId = newChannel.Id;
        await _dbContext.SaveChangesAsync();

        // ✅ Send the ticket embed
        await SendTicketEmbed(ticketId, newChannel);

        // ✅ Generate the transcript file
        var transcriptFile = await GenerateTranscript(ticketId);
        if (transcriptFile == null)
        {
            await newChannel.SendMessageAsync("Ticket Opened from Website. No transcript available.");
            return;
        }

        // ✅ Send the transcript with a message
        await newChannel.SendFileAsync(transcriptFile, $"Ticket#{ticketId} Transcript");

        // ✅ Delete temp transcript file after sending
        File.Delete(transcriptFile);
    }

    private async Task SendTicketEmbed(int ticketId, SocketTextChannel channel)
    {
        var ticket = _dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
        {
            Log.Information($"❌ Ticket #{ticketId} not found in the database.");
            return;
        }

        // ✅ Fetch the Discord user from their ID (supports users not in cache)
        var user = ticket.DiscordUserId.HasValue
            ? await _client.Rest.GetUserAsync(ticket.DiscordUserId.Value)
            : null;

        var embed = new EmbedBuilder()
            .WithTitle($"🎫 Ticket #{ticket.Id} - {char.ToUpper(ticket.Subject[0])}{ticket.Subject.Substring(1)}")
            .WithAuthor(user?.Username ?? "Unknown", user?.GetAvatarUrl(ImageFormat.Png, 256) ?? "https://i.imgur.com/dnlokbX.png")
            .WithDescription("--------------------------------------\n")
            .WithThumbnailUrl("https://i.imgur.com/dnlokbX.png")
            .AddField("📂 **Category**", $"{ticket.Category}", inline: false)
            .AddField("🎮 **Game**", $"{ticket.Game}", inline: false)
            .AddField("🗺️ **Server**", $"{ticket.Server}", inline: false)
            .AddField("\u200B", "\u200B", inline: false)
            .AddField("📜 **Description**", $"```{char.ToUpper(ticket.Description[0])}{ticket.Description.Substring(1)}```", inline: false)
            .WithColor(Color.Green)
            .WithFooter(footer =>
            {
                footer.Text = $"Ticket reopened by Lynx Bot";
                footer.IconUrl = "https://i.imgur.com/dnlokbX.png";
            })
            .WithCurrentTimestamp();

        var components = new ComponentBuilder()
            .WithButton("Close Ticket", $"close_ticket_{ticket.Id}", ButtonStyle.Danger)
            .WithButton("📇 View Player Card", $"view_card_{ticket.Id}", ButtonStyle.Secondary);

        await channel.SendMessageAsync(embed: embed.Build(), components: components.Build());

    }

    private async Task LoadMessagesToChannel(int ticketId, SocketTextChannel channel)
    {
        var messages = _dbContext.Messages
            .Where(m => m.MessageGroupId == ticketId)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        if (!messages.Any())
        {
            Log.Information($"⚠️ No messages found for Ticket #{ticketId}.");
            return;
        }

        Log.Information($"📥 Preparing {messages.Count} messages for Ticket #{ticketId}.");

        var messageChunks = new List<string>();
        var currentBatch = new List<string>();
        int currentLength = 0;

        foreach (var msg in messages)
        {

            bool hasText = !string.IsNullOrWhiteSpace(msg.Content);
            bool isOnlyLink = hasText && Uri.IsWellFormedUriString(msg.Content.Trim(), UriKind.Absolute);

            if (!hasText)
            {
                continue;
            }

            // ✅ Handle only a hyperlink (make it clickable)
            if (isOnlyLink)
            {
                await channel.SendMessageAsync(msg.Content.Trim());
                continue;
            }

            string formattedMessage = $"[{msg.CreatedAt:HH:mm}] {CapitalizeFirstLetter(msg.DiscordUserName) ?? "Unknown"}: {msg.Content}";

            // ✅ Prevent exceeding Discord's 2000-character limit
            if (currentLength + formattedMessage.Length > 2000)
            {
                messageChunks.Add(string.Join("\n", currentBatch));
                currentBatch.Clear();
                currentLength = 0;
            }

            currentBatch.Add(formattedMessage);
            currentLength += formattedMessage.Length;

            // ✅ Send text first
            if (currentBatch.Count > 0)
            {
                messageChunks.Add(string.Join("\n", currentBatch));
                currentBatch.Clear();
            }

        }

        // Send any remaining text messages
        foreach (var chunk in messageChunks)
        {
            await channel.SendMessageAsync(chunk);
            await Task.Delay(500);
        }

        Log.Information($"✅ Sent {messageChunks.Count} message batches for Ticket #{ticketId}.");
    }

    private static string CapitalizeFirstLetter(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        return char.ToUpper(input[0]) + input.Substring(1);
    }

    private string GetDiscordAvatarUrl(ulong discordUserId)
    {
        var user = _client.GetUser(discordUserId);
        return user?.GetAvatarUrl(ImageFormat.Png, 256) ?? "https://i.imgur.com/dnlokbX.png";
    }
    private string FormatMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        // Ensure URLs are not wrapped in backticks
        return content.Replace("```", "").Trim();
    }

    private async Task<string> GenerateTranscript(int ticketId)
    {
        var ticket = _dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
        {
            Log.Information($"❌ Ticket #{ticketId} not found.");
            return null;
        }

        var messages = _dbContext.Messages
            .Where(m => m.MessageGroupId == ticketId)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        if (!messages.Any())
        {
            Log.Information($"⚠️ No messages found for Ticket #{ticketId}.");
            return null;
        }

        string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "transcript_template.html");
        if (!File.Exists(templatePath))
        {
            Log.Information($"❌ Transcript template not found at {templatePath}.");
            return null;
        }

        string htmlTemplate = await File.ReadAllTextAsync(templatePath);
        var messagesHtml = "";

        foreach (var msg in messages)
        {
            var timestamp = msg.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            var user = msg.DiscordUserName ?? "Unknown";
            var content = System.Net.WebUtility.HtmlEncode(msg.Content);

            messagesHtml += $@"
        <div class='message'>
        <div class='timestamp'>{timestamp}</div>
        <div class='username'>{user}</div>
        <div class='content'>{content}</div>";

            if (msg.ImgUrls.Any())
            {
                foreach (var imgUrl in msg.ImgUrls)
                {
                    messagesHtml += $"<img src='{imgUrl}' />";
                }
            }

            messagesHtml += "</div>";
        }

        var finalHtml = htmlTemplate
            .Replace("{TICKET_ID}", ticketId.ToString())
            .Replace("{MESSAGES}", messagesHtml);

        string transcriptFilePath = Path.Combine(Path.GetTempPath(), $"ticket-{ticketId}-transcript.html");
        await File.WriteAllTextAsync(transcriptFilePath, finalHtml);

        return transcriptFilePath;
    }


}
