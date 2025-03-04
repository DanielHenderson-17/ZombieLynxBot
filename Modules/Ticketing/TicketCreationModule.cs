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

        // Save the ticket in the database
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

        // Next: Create the Discord channel
    }
}
