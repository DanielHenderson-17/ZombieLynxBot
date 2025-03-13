using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

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
            Console.WriteLine("❌ No guild found!");
            return;
        }

        string channelName = $"ticket-{ticketId}";
        var existingChannel = guild.TextChannels.FirstOrDefault(c => c.Name == channelName);

        // ✅ If channel already exists, do nothing
        if (existingChannel != null)
        {
            Console.WriteLine($"✅ Channel {channelName} already exists.");
            return;
        }

        // 🆕 Ensure the category ID is retrieved correctly
        ulong? categoryId = null;

        if (Program.Config.SupportCategory.TryGetValue("🔥 General 🔥", out string categoryIdStr) &&
            ulong.TryParse(categoryIdStr, out ulong parsedCategoryId))
        {
            categoryId = parsedCategoryId;
            Console.WriteLine($"ℹ️ Retrieved Category ID: {categoryId}");
        }
        else
        {
            Console.WriteLine("⚠️ No valid category ID found in config.");
        }


        // 🛠 Verify the bot can find the category in Discord
        var categoryChannel = guild.CategoryChannels.FirstOrDefault(c => c.Id == categoryId);

        if (categoryChannel == null)
        {
            Console.WriteLine("⚠️ Category for tickets not found! Channel will be created without a category.");
        }
        else
        {
            Console.WriteLine($"✅ Found ticket category: {categoryChannel.Name}");
        }

        // Retrieve the ticket from the database
        var ticket = _dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
        {
            Console.WriteLine($"❌ Ticket #{ticketId} not found in the database.");
            return;
        }

        // Retrieve the necessary role IDs from config
        var supportRoleId = Convert.ToUInt64(Program.Config.SupportRole["Help!"]);

        // 🆕 Create the ticket channel under the found category with correct permissions
        var newRestChannel = await guild.CreateTextChannelAsync(channelName, options =>
        {
            options.CategoryId = categoryChannel?.Id;
            options.Topic = $"Ticket #{ticketId}";
            options.PermissionOverwrites = new System.Collections.Generic.List<Overwrite>
            {
        // ❌ Deny @everyone from seeing the ticket
        new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)),

        // ✅ Allow the ticket creator to view and send messages
        new Overwrite(ticket.DiscordUserId ?? 0, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow)),

        // ✅ Allow the Help! role to see and send messages
        new Overwrite(supportRoleId, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow))
            };
        });


        // ✅ Convert RestTextChannel to SocketTextChannel
        var newChannel = guild.GetTextChannel(newRestChannel.Id);

        if (newChannel == null)
        {
            Console.WriteLine($"⚠️ Failed to retrieve newly created channel {channelName}.");
            return;
        }

        Console.WriteLine($"✅ Created new channel {newChannel.Name} in category {categoryChannel?.Name ?? "None"}.");

        // 📩 Send the ticket embed with close button
        await SendTicketEmbed(ticketId, newChannel);

        // 🔄 Load messages for this ticket
        await LoadMessagesToChannel(ticketId, newChannel);

    }

    private async Task SendTicketEmbed(int ticketId, SocketTextChannel channel)
    {
        var ticket = _dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
        {
            Console.WriteLine($"❌ Ticket #{ticketId} not found in the database.");
            return;
        }

        var user = ticket.DiscordUserId.HasValue ? _client.GetUser(ticket.DiscordUserId.Value) : null;
        string userAvatar = user?.GetAvatarUrl(ImageFormat.Png, 256) ?? "https://i.imgur.com/dnlokbX.png";

        var embed = new EmbedBuilder()
            .WithTitle($"🎫 Ticket #{ticket.Id} - {char.ToUpper(ticket.Subject[0])}{ticket.Subject.Substring(1)}")
            .WithDescription("--------------------------------------\n")
            .WithThumbnailUrl(userAvatar)
            .AddField("📂 **Category**", $"{ticket.Category}", inline: false)
            .AddField("🎮 **Game**", $"{ticket.Game}", inline: false)
            .AddField("🗺️ **Server**", $"{ticket.Server}", inline: false)
            .AddField("\u200B", "\u200B", inline: false)
            .AddField("📜 **Description**", $"```{char.ToUpper(ticket.Description[0])}{ticket.Description.Substring(1)}```", inline: false)
            .WithColor(Color.Green)
            .WithFooter(footer =>
            {
                footer.Text = $"Ticket reopened by {user?.Username ?? "Lynx Bot"}";
                footer.IconUrl = userAvatar;
            })
            .WithCurrentTimestamp();

        var closeButton = new ComponentBuilder()
            .WithButton("Close Ticket", $"close_ticket_{ticket.Id}", ButtonStyle.Danger);

        await channel.SendMessageAsync(embed: embed.Build(), components: closeButton.Build());
    }

    private async Task LoadMessagesToChannel(int ticketId, SocketTextChannel channel)
    {
        var messages = _dbContext.Messages
            .Where(m => m.MessageGroupId == ticketId)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        if (!messages.Any())
        {
            Console.WriteLine($"⚠️ No messages found for Ticket #{ticketId}.");
            return;
        }

        Console.WriteLine($"📥 Preparing {messages.Count} messages for Ticket #{ticketId}.");

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

        Console.WriteLine($"✅ Sent {messageChunks.Count} message batches for Ticket #{ticketId}.");
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

}
