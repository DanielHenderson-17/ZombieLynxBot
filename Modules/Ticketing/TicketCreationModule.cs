using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Serilog;

public class TicketModal : IModal
{
    public string Title => "Create a Ticket";

    [InputLabel("Subject")]
    [ModalTextInput("subject", TextInputStyle.Short, "Enter a brief subject", minLength: 5, maxLength: 100)]
    public string Subject { get; set; }

    [InputLabel("Category")]
    [ModalTextInput("category", TextInputStyle.Short)]
    public string Category { get; set; }

    [InputLabel("Game")]
    [ModalTextInput("game", TextInputStyle.Short)]
    public string Game { get; set; }

    [InputLabel("Server")]
    [ModalTextInput("server", TextInputStyle.Short)]
    public string Server { get; set; }

    [InputLabel("Description")]
    [ModalTextInput("description", TextInputStyle.Paragraph, "Describe your issue in detail")]
    public string Description { get; set; }
}

public class TicketCreationModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TicketHandler _ticketHandler;

    public TicketCreationModule()
    {
        _ticketHandler = new TicketHandler();
    }

    [ModalInteraction("ticket_submission")]
    public async Task HandleTicketSubmission(TicketModal modal)
    {
        await DeferAsync(); // Avoid interaction timeout

        Log.Information($"🎫 Creating ticket for {Context.User.Username}...");

        // ✅ Save the ticket in the database
        var newTicket = await _ticketHandler.CreateTicketAsync(
            modal.Subject,
            modal.Category,
            modal.Game,
            modal.Server,
            modal.Description,
            Context.User.Id,
            Context.User.Username
        );

        Log.Information($"✅ Ticket {newTicket.Id} created in DB.");

        // ✅ Get the Guild & Config Settings
        var guild = (Context.Client as DiscordSocketClient)?.GetGuild(Context.Guild.Id);
        if (guild == null)
        {
            Log.Information("❌ Error: Guild not found.");
            await FollowupAsync("An error occurred while creating your ticket. Please contact an admin.", ephemeral: true);
            return;
        }

        var supportCategoryId = Convert.ToUInt64(Program.Config.SupportCategory["🔥 General 🔥"]);
        var supportRoleId = Convert.ToUInt64(Program.Config.SupportRole["Help!"]);
        var helpRoleMention = $"<@&{supportRoleId}>";

        var ticketMessage = $"An admin will be with you to help with your request shortly.\n" +
                            $"Please tell us what your player name and tribe name are.\n" +
                            $"{helpRoleMention}";

        var categoryChannel = guild.GetCategoryChannel(supportCategoryId);
        if (categoryChannel == null)
        {
            Log.Information("❌ Error: Support category not found.");
            await FollowupAsync("An error occurred while creating your ticket. Please contact an admin.", ephemeral: true);
            return;
        }

        // ✅ Create the Ticket Channel
        string channelName = $"ticket-{newTicket.Id}";
        var ticketChannel = await guild.CreateTextChannelAsync(channelName, properties =>
        {
            properties.CategoryId = supportCategoryId;
            properties.PermissionOverwrites = new System.Collections.Generic.List<Overwrite>
            {
                // ❌ Deny @everyone from seeing the ticket
                new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)),

                // ✅ Allow the ticket creator to view and send messages
                new Overwrite(Context.User.Id, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow)),

                // ✅ Allow the Help! role to see and send messages
                new Overwrite(supportRoleId, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow))
            };
        });

        Log.Information($"✅ Created channel {ticketChannel.Name} ({ticketChannel.Id})");

        // ✅ Update the Ticket in Database
        await _ticketHandler.UpdateTicketWithChannelId(newTicket.Id, ticketChannel.Id);


        // ✅ Send a message in the new channel
        var embed = new EmbedBuilder()
         .WithTitle($"🎫 Ticket #{newTicket.Id} - {char.ToUpper(newTicket.Subject[0])}{newTicket.Subject.Substring(1)}")
         .WithAuthor(Context.User.Username, Context.User.GetAvatarUrl())
         .WithDescription("--------------------------------------\n")
         .WithThumbnailUrl("https://i.imgur.com/dnlokbX.png")
         .AddField("📂 **Category**", $"{newTicket.Category}", inline: false)
         .AddField("🎮 **Game**", $"{newTicket.Game}", inline: false)
         .AddField("🗺️ **Server**", $"{newTicket.Server}", inline: false)
         .AddField("\u200B", "\u200B", inline: false)
         .AddField("📜 **Description**", $"```{char.ToUpper(newTicket.Description[0])}{newTicket.Description.Substring(1)}```", inline: false)
         .WithColor(Color.Green)
         .WithFooter(footer =>
            {
                footer.Text = $"Ticket created by {Context.User.Username}";
                footer.IconUrl = "https://i.imgur.com/dnlokbX.png";
            })
         .WithCurrentTimestamp();

        // ✅ Buttons: Close + View Card
        var buttons = new ComponentBuilder()
            .WithButton("Close Ticket", $"close_ticket_{newTicket.Id}", ButtonStyle.Danger)
            .WithButton("📇 View Player Card", $"view_card_{newTicket.Id}", ButtonStyle.Secondary);

        // ✅ Send the embed with buttons
        await ticketChannel.SendMessageAsync(embed: embed.Build(), components: buttons.Build());


        // ✅ Send the initial ticket message after the embed
        await ticketChannel.SendMessageAsync(ticketMessage);


    }
}

public class TicketCloseModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TicketHandler _ticketHandler;

    public TicketCloseModule()
    {
        _ticketHandler = new TicketHandler();
    }

    [ComponentInteraction("close_ticket_*")]
    public async Task HandleCloseTicket(string customId)
    {

        // ✅ Extract the Ticket ID from the button's custom ID
        string ticketIdString = customId.Replace("close_ticket_", "");
        if (!int.TryParse(ticketIdString, out int ticketId))
        {
            await RespondAsync("❌ Invalid ticket ID.", ephemeral: true);
            return;
        }

        var ticket = _ticketHandler.GetTicketById(ticketId);
        if (ticket == null)
        {
            await RespondAsync("❌ Ticket not found.", ephemeral: true);
            return;
        }

        Log.Information($"🔍 Closing Ticket #{ticketId}");

        // ✅ Update the ticket in the database
        bool updated = await _ticketHandler.CloseTicketAsync(ticketId);
        if (!updated)
        {
            await RespondAsync("❌ Failed to close the ticket. Please contact an admin.", ephemeral: true);
            return;
        }

        // ✅ Send a closing message in the ticket channel
        var closeMessage = "Hopefully we helped you out today. If you have any further issues in the future, please submit a new ticket.";
        await Context.Channel.SendMessageAsync(closeMessage);

        // ✅ Send a closing embed in the transcript log channel
        ulong transcriptChannelId = Convert.ToUInt64(Program.Config.TranscriptLogChannel);
        var transcriptChannel = Context.Client.GetChannel(transcriptChannelId) as SocketTextChannel;
        var ticketOwner = await Context.Client.GetUserAsync(ticket.DiscordUserId ?? 0);
        var centralTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"));

        if (transcriptChannel != null)
        {
            var embed = new EmbedBuilder()
                .WithAuthor($"{ticketOwner?.Username}#{ticketOwner?.Discriminator}", ticketOwner?.GetAvatarUrl())
                .WithThumbnailUrl("https://i.imgur.com/dnlokbX.png")
                .WithColor(new Color(46, 204, 113))
                .AddField("Ticket Closed By", $"<@{Context.User.Id}>", true)
                .AddField("Ticket Name", $"Ticket-{ticketId}", true)
                .AddField("Panel Name", "@Help!", true)
                .AddField("Subject", ticket.Subject, true)
                .AddField("Category", ticket.Category, true)
                .AddField("Game", ticket.Game, true)
                .AddField("📜 **Description**", $"```{char.ToUpper(ticket.Description[0])}{ticket.Description.Substring(1)}```", inline: false)
                .AddField("🔒 Closed At", $"{centralTime:yyyy-MM-dd hh:mm tt} CST", false)
                .WithImageUrl("https://imgur.com/a/iC7KmOw")
                .WithFooter("Closed Ticket Archive")
                .WithCurrentTimestamp();

            var components = new ComponentBuilder()
                .WithButton("📜 Transcript", $"transcript_{ticketId}", ButtonStyle.Primary)
                .WithButton("🔓 Reopen Ticket", $"reopen_ticket_{ticketId}", ButtonStyle.Success);


            await transcriptChannel.SendMessageAsync(embed: embed.Build(), components: components.Build());
        }

        // ✅ Wait 10 seconds before deleting the channel
        await Task.Delay(10000);
        if (Context.Channel is SocketTextChannel ticketChannel)
        {
            await ticketChannel.DeleteAsync();
        }
        else
        {
            Log.Information("❌ Error: Tried to delete a non-text channel.");
        }
    }

    [ComponentInteraction("reopen_ticket_*")]
    public async Task HandleReopenTicket(string customId)
    {
        await DeferAsync();

        string ticketIdString = customId.Replace("reopen_ticket_", "");
        if (!int.TryParse(ticketIdString, out int ticketId))
        {
            await FollowupAsync("❌ Invalid ticket ID.", ephemeral: true);
            return;
        }

        var ticketChannelManager = new TicketChannelManager(Context.Client as DiscordSocketClient);
        await ticketChannelManager.HandleTicketReopen(ticketId);

        if (Context.Interaction is SocketMessageComponent component)
        {
            await component.Message.DeleteAsync();
        }

        await FollowupAsync($"🔓 Ticket #{ticketId} has been reopened!", ephemeral: true);
    }

    [ComponentInteraction("transcript_*")]
    public async Task HandleTranscriptButton(string customId)
    {
        await DeferAsync(ephemeral: true);

        string ticketIdString = customId.Replace("transcript_", "");
        if (!int.TryParse(ticketIdString, out int ticketId))
        {
            await FollowupAsync("❌ Invalid ticket ID.", ephemeral: true);
            return;
        }

        using var dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);

        var ticket = dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
        {
            await FollowupAsync("❌ Ticket not found in the database.", ephemeral: true);
            return;
        }

        var messages = dbContext.Messages
            .Where(m => m.MessageGroupId == ticketId)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        // Load the HTML template from disk
        string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "transcript_template.html");
        if (!File.Exists(templatePath))
        {
            await FollowupAsync($"❌ Transcript template not found at {templatePath}.", ephemeral: true);
            return;
        }


        string htmlTemplate = await File.ReadAllTextAsync(templatePath);

        // Build messages HTML dynamically
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

        // Insert into template
        var finalHtml = htmlTemplate
            .Replace("{TICKET_ID}", ticketId.ToString())
            .Replace("{MESSAGES}", messagesHtml);

        // Convert to stream and send as attachment
        var fileBytes = System.Text.Encoding.UTF8.GetBytes(finalHtml);
        using var stream = new MemoryStream(fileBytes);

        await FollowupWithFileAsync(stream, $"ticket-{ticketId}-transcript.html", $"📜 Here's the transcript for Ticket #{ticketId}:");
    }

    [ComponentInteraction("view_card_*")]
    public async Task HandleViewCard(string customId)
    {
        await DeferAsync(ephemeral: true);

        string ticketIdString = customId.Replace("view_card_", "");
        if (!int.TryParse(ticketIdString, out int ticketId))
        {
            await FollowupAsync("❌ Invalid ticket ID.", ephemeral: true);
            return;
        }

        using var dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);
        var ticket = dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
        {
            await FollowupAsync("❌ Ticket not found.", ephemeral: true);
            return;
        }

        var member = dbContext.ZLGMembers.FirstOrDefault(m => m.DiscordId == ticket.DiscordUserId.ToString());
        if (member == null)
        {
            await FollowupAsync("❌ Player info not found.", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithAuthor(FormatName(member.DiscordName), member.DiscordImgUrl ?? "https://i.imgur.com/dnlokbX.png")
            .WithThumbnailUrl(member.DiscordImgUrl ?? "https://i.imgur.com/dnlokbX.png")
            .WithColor(Color.Blue)
            .WithCurrentTimestamp();
        embed.AddField("💰 Points", member.Points.ToString("N0"), false);

        // Steam info
        if (!string.IsNullOrWhiteSpace(member.SteamName))
        {
            embed.AddField("🧊 Steam", $"{member.SteamName}\n`{member.SteamId}`", false);
        }

        // Minecraft info
        if (!string.IsNullOrWhiteSpace(member.MinecraftUsername))
        {
            embed.AddField("⛏️ Minecraft", $"{member.MinecraftUsername}\n`{member.MinecraftUuid}`", false);
        }

        // Epic info
        if (!string.IsNullOrWhiteSpace(member.EpicName))
        {
            embed.AddField("🎮 Epic", $"{member.EpicName}\n`{member.EosId}`", false);
        }



        // TimedPermissionGroups (show top one only if multiple)
        if (!string.IsNullOrWhiteSpace(member.TimedPermissionGroups))
        {
            var group = member.TimedPermissionGroups.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(group))
            {
                string icon = group.ToLower().Contains("vibranium") ? "🟣" :
                              group.ToLower().Contains("diamond") ? "🔷" :
                              group.ToLower().Contains("gold") ? "🟡" :
                              "🔘";

                embed.AddField("🛡️ Membership", $"{icon} {group}", true);
            }
        }

        await FollowupAsync(embed: embed.Build());
    }

    private static string FormatName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Unknown";
        var name = raw.Split('#')[0];
        return char.ToUpper(name[0]) + name.Substring(1).ToLower();
    }

}
