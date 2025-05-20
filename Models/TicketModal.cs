using Discord;
using Discord.Interactions;

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
