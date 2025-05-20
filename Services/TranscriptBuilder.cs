using System.Text;

public static class TranscriptBuilder
{
    public static async Task<MemoryStream?> BuildTranscriptAsync(int ticketId)
    {
        using var dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);

        var ticket = dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null) return null;

        var messages = dbContext.Messages
            .Where(m => m.MessageGroupId == ticketId)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        string templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "transcript_template.html");
        if (!File.Exists(templatePath)) return null;

        string htmlTemplate = await File.ReadAllTextAsync(templatePath);
        var messagesHtml = new StringBuilder();

        foreach (var msg in messages)
        {
            var timestamp = msg.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            var user = msg.DiscordUserName ?? "Unknown";
            var avatarUrl = msg.DiscordImgUrl ?? "https://cdn.discordapp.com/embed/avatars/0.png";
            var content = System.Net.WebUtility.HtmlEncode(msg.Content);

            // Debug output
            Console.WriteLine($"user={user} | avatarUrl={avatarUrl}");

            messagesHtml.Append($@"
            <div class='message'>
              <img src='{avatarUrl}' alt='avatar' width='40' height='40' style='border-radius: 50%; display: inline-block;' />
              <div class='username'>{user}</div>
              <div class='timestamp'>{timestamp}</div>
              <div class='content'>{content}</div>
            </div>");
        }

        var finalHtml = htmlTemplate
            .Replace("{TICKET_ID}", ticketId.ToString())
            .Replace("{MESSAGES}", messagesHtml.ToString());

        return new MemoryStream(Encoding.UTF8.GetBytes(finalHtml));
    }
}
