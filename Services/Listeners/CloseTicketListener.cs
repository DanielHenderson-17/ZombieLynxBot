using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Serilog;

public class CloseTicketListener
{
    private readonly TicketService _ticketService;
    private readonly DiscordSocketClient _client;

    public CloseTicketListener(TicketService ticketService, DiscordSocketClient client)
    {
        _ticketService = ticketService;
        _client = client;
    }

    public async Task<bool> TryCloseTicketAsync(SocketInteractionContext context, int ticketId)
    {
        var ticket = _ticketService.GetTicketById(ticketId);
        if (ticket == null)
        {
            await context.Interaction.RespondAsync("❌ Ticket not found.", ephemeral: true);
            return false;
        }

        Log.Information($"🔍 Closing Ticket #{ticketId}");

        bool updated = await _ticketService.CloseTicketAsync(ticketId);
        if (!updated)
        {
            await context.Interaction.RespondAsync("❌ Failed to close the ticket. Please contact an admin.", ephemeral: true);
            return false;
        }
        await context.Interaction.RespondAsync("✅ Ticket closed.", ephemeral: true);

        ulong transcriptChannelId = Convert.ToUInt64(Program.Config.TranscriptLogChannel);
        var transcriptChannel = _client.GetChannel(transcriptChannelId) as SocketTextChannel;
        var ticketOwner = await _client.GetUserAsync(ticket.DiscordUserId ?? 0);
        var centralTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"));

        if (transcriptChannel != null)
        {
            var embed = TicketLogEmbedFactory.BuildClosureEmbed(context.User, ticketOwner, ticket, centralTime);

            var components = new ComponentBuilder()
                .WithButton("📜 Transcript", $"transcript_{ticketId}", ButtonStyle.Primary)
                .WithButton("🔓 Reopen Ticket", $"reopen_ticket_{ticketId}", ButtonStyle.Success);

            await transcriptChannel.SendMessageAsync(embed: embed, components: components.Build());
        }

        return true;
    }
}
