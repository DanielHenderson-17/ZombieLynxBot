using System;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Serilog;

public class TicketCloseModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly CloseTicketListener _closeTicketListener;
    private readonly UserCardService _userCardService;

    public TicketCloseModule(
        CloseTicketListener closeTicketListener,
        UserCardService userCardService)
    {
        _closeTicketListener = closeTicketListener;
        _userCardService = userCardService;
    }

    [ComponentInteraction("close_ticket_*")]
    public async Task HandleCloseTicket(string customId)
    {
        if (!TryParseTicketId(customId, "close_ticket_", out int ticketId))
        {
            await RespondAsync("❌ Invalid ticket ID.", ephemeral: true);
            return;
        }

        await _closeTicketListener.TryCloseTicketAsync(Context, ticketId);
    }

    [ComponentInteraction("reopen_ticket_*")]
    public async Task HandleReopenTicket(string customId)
    {
        await DeferAsync();

        if (!TryParseTicketId(customId, "reopen_ticket_", out int ticketId))
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

        if (!TryParseTicketId(customId, "transcript_", out int ticketId))
        {
            await FollowupAsync("❌ Invalid ticket ID.", ephemeral: true);
            return;
        }

        var transcriptStream = await TranscriptBuilder.BuildTranscriptAsync(ticketId);
        if (transcriptStream == null)
        {
            await FollowupAsync("❌ Could not generate transcript.", ephemeral: true);
            return;
        }

        await FollowupWithFileAsync(transcriptStream, $"ticket-{ticketId}-transcript.html", $"📜 Here's the transcript for Ticket #{ticketId}:");
    }

    [ComponentInteraction("view_card_*")]
    public async Task HandleViewCard(string customId)
    {
        if (!TryParseTicketId(customId, "view_card_", out int ticketId))
        {
            await RespondAsync("❌ Invalid ticket ID.", ephemeral: true);
            return;
        }

        await _userCardService.SendUserCardAsync(Context, ticketId);
    }

    private static bool TryParseTicketId(string customId, string prefix, out int ticketId)
    {
        string ticketIdString = customId.Replace(prefix, "");
        return int.TryParse(ticketIdString, out ticketId);
    }
}
