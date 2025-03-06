using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

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

        Console.WriteLine($"🎫 Creating ticket for {Context.User.Username}...");

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

        Console.WriteLine($"✅ Ticket {newTicket.Id} created in DB.");

        // ✅ Get the Guild & Config Settings
        var guild = (Context.Client as DiscordSocketClient)?.GetGuild(Context.Guild.Id);
        if (guild == null)
        {
            Console.WriteLine("❌ Error: Guild not found.");
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
            Console.WriteLine("❌ Error: Support category not found.");
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

        Console.WriteLine($"✅ Created channel {ticketChannel.Name} ({ticketChannel.Id})");

        // ✅ Update the Ticket in Database
        await _ticketHandler.UpdateTicketWithChannelId(newTicket.Id, ticketChannel.Id);


        // ✅ Send a message in the new channel
        var embed = new EmbedBuilder()
            .WithTitle($"Ticket #{newTicket.Id} - {newTicket.Subject}")
            .WithDescription($"Category: **{newTicket.Category}**\nGame: **{newTicket.Game}**\nServer: **{newTicket.Server}**\n\n📜 **Description:** {newTicket.Description}")
            .WithColor(Color.Orange)
            .WithFooter($"Ticket created by {Context.User.Username}")
            .WithCurrentTimestamp();

        // ✅ Create a button to close the ticket
        var closeButton = new ComponentBuilder()
            .WithButton("Close Ticket", $"close_ticket_{newTicket.Id}", ButtonStyle.Danger);

        // ✅ Send the embed with the button
        await ticketChannel.SendMessageAsync(embed: embed.Build(), components: closeButton.Build());

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

        Console.WriteLine($"🔍 Closing Ticket #{ticketId}");

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

        // ✅ Respond to the button press
        await RespondAsync("✅ Ticket has been closed. The channel will be deleted in 10 seconds.", ephemeral: true);

        // ✅ Wait 10 seconds before deleting the channel
        await Task.Delay(10000);
        if (Context.Channel is SocketTextChannel ticketChannel)
        {
            await ticketChannel.DeleteAsync();
        }
        else
        {
            Console.WriteLine("❌ Error: Tried to delete a non-text channel.");
        }
    }
}
