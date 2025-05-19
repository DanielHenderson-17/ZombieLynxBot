using System;
using Discord;
using Discord.WebSocket;

public static class TicketLogEmbedFactory
{
    public static Embed BuildClosureEmbed(SocketUser closedBy, IUser? ticketOwner, Ticket ticket, DateTime closedAtCST)
    {
        return new EmbedBuilder()
            .WithAuthor($"{ticketOwner?.Username}#{ticketOwner?.Discriminator}", ticketOwner?.GetAvatarUrl())
            .WithThumbnailUrl("https://i.imgur.com/dnlokbX.png")
            .WithColor(new Color(46, 204, 113))
            .AddField("Ticket Closed By", $"<@{closedBy.Id}>", true)
            .AddField("Ticket Name", $"Ticket-{ticket.Id}", true)
            .AddField("Panel Name", "@Help!", true)
            .AddField("Subject", ticket.Subject, true)
            .AddField("Category", ticket.Category, true)
            .AddField("Game", ticket.Game, true)
            .AddField("📜 **Description**", $"```{Capitalize(ticket.Description)}```", inline: false)
            .AddField("🔒 Closed At", $"{closedAtCST:yyyy-MM-dd hh:mm tt} CST", false)
            .WithImageUrl("https://imgur.com/a/iC7KmOw")
            .WithFooter("Closed Ticket Archive")
            .WithCurrentTimestamp()
            .Build();
    }

    private static string Capitalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        return char.ToUpper(input[0]) + input.Substring(1);
    }
}
