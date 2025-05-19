using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

public class TicketSetupModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ticket-create", "Sets up the ticket creation button in this channel.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task CreateTicketButton()
    {
        // Create an embed with the ticket rules/bullet points
        var embed = new EmbedBuilder()
            .WithTitle("🎫 Zombie Lynx Gaming Ticket Guidelines")
            .WithDescription("Before creating a ticket, please review the following rules:\n" +
                             "• Tickets made on behalf of others will be closed immediately without response.\n" +
                             "• Duplicate tickets from the same tribe will be closed immediately without response.\n" +
                             "• Tickets not being prioritized over gameplay will be closed due to inactivity.\n\n" +
                             "**You must have a registered account with ZLG to create a ticket.**\n" +
                             "If you don’t have one yet, visit [zlg.gg](https://zlg.gg/login) to register.\n\n" +
                             "**Click the button below to create a ticket:**")
            .WithColor(Color.Green)
            .WithFooter("Failure to follow these guidelines may result in ticket closure.")
            .Build();

        // Create the button
        var button = new ButtonBuilder()
            .WithLabel("✉️ Create Ticket")
            .WithStyle(ButtonStyle.Primary)
            .WithCustomId("open_ticket_form");

        var component = new ComponentBuilder()
            .WithButton(button)
            .Build();

        // Send the message in the current channel
        await Context.Channel.SendMessageAsync(embed: embed, components: component);

        // Acknowledge the command without showing it in chat
        await RespondAsync("✅ Ticket button has been posted!", ephemeral: true);
    }
}
