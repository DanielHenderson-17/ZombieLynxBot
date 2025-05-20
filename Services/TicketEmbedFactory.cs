using Discord;
using Discord.WebSocket;

public static class TicketEmbedFactory
{
    public static Embed BuildTicketEmbed(IUser user, Ticket ticket)
    {
        var formattedUsername = UserNameFormatter.FormatNameUtils(user.Username);

        return new EmbedBuilder()
            .WithTitle($"🎫 Ticket #{ticket.Id} - {Capitalize(ticket.Subject)}")
            .WithAuthor(formattedUsername, user.GetAvatarUrl())
            .WithDescription("--------------------------------------\n")
            .WithThumbnailUrl("https://i.imgur.com/dnlokbX.png")
            .AddField("📂 **Category**", ticket.Category, inline: false)
            .AddField("🎮 **Game**", ticket.Game, inline: false)
            .AddField("🗺️ **Server**", ticket.Server, inline: false)
            .AddField("\u200B", "\u200B", inline: false)
            .AddField("📜 **Description**", $"```{Capitalize(ticket.Description)}```", inline: false)
            .WithColor(Color.Green)
            .WithFooter(footer =>
            {
                footer.Text = $"Ticket created by {formattedUsername}";
                footer.IconUrl = "https://i.imgur.com/dnlokbX.png";
            })
            .WithCurrentTimestamp()
            .Build();
    }


    public static ComponentBuilder BuildTicketButtons(int ticketId)
    {
        return new ComponentBuilder()
            .WithButton("Close Ticket", $"close_ticket_{ticketId}", ButtonStyle.Danger)
            .WithButton("📇 View Player Card", $"view_card_{ticketId}", ButtonStyle.Secondary)
            .WithButton("Reassign Owner", $"reassign_owner_{ticketId}", ButtonStyle.Primary);
    }

    private static string Capitalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        return char.ToUpper(input[0]) + input.Substring(1);
    }
}
