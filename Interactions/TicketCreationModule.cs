using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Serilog;

public class TicketCreationModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly TicketService _ticketService;

    public TicketCreationModule()
    {
        _ticketService = new TicketService();
    }

    [ModalInteraction("ticket_submission")]
    public async Task HandleTicketSubmission(TicketModal modal)
    {
        await DeferAsync(); // Avoid interaction timeout

        Log.Information($"🎫 Creating ticket for {Context.User.Username}...");

        // ✅ Save the ticket in the database
        var newTicket = await _ticketService.CreateTicketAsync(
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
        await _ticketService.UpdateTicketWithChannelId(newTicket.Id, ticketChannel.Id);


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
