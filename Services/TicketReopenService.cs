using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Serilog;

public class TicketReopenService
{
    private readonly DiscordSocketClient _client;
    private readonly TicketDbContext _dbContext;

    public TicketReopenService(DiscordSocketClient client)
    {
        _client = client;
        _dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);
    }

    public async Task HandleTicketReopen(int ticketId)
    {
        var guild = _client.Guilds.FirstOrDefault();
        if (guild == null)
        {
            Log.Information("❌ No guild found!");
            return;
        }

        string channelName = $"ticket-{ticketId}";
        var existingChannel = guild.TextChannels.FirstOrDefault(c => c.Name == channelName);
        if (existingChannel != null)
        {
            Log.Information($"✅ Channel {channelName} already exists.");
            return;
        }

        // Get the category ID
        ulong? categoryId = null;
        if (Program.Config.SupportCategory.TryGetValue("🔥 General 🔥", out string categoryIdStr) &&
            ulong.TryParse(categoryIdStr, out ulong parsedCategoryId))
        {
            categoryId = parsedCategoryId;
        }
        var categoryChannel = guild.CategoryChannels.FirstOrDefault(c => c.Id == categoryId);

        // Retrieve ticket from DB
        var ticket = _dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
        {
            Log.Information($"❌ Ticket #{ticketId} not found in the database.");
            return;
        }

        ticket.Status = "Open";
        ticket.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var supportRoleId = Convert.ToUInt64(Program.Config.SupportRole["Help!"]);

        // Create the ticket channel
        var newRestChannel = await guild.CreateTextChannelAsync(channelName, options =>
        {
            options.CategoryId = categoryChannel?.Id;
            options.Topic = $"Ticket #{ticketId}";
            options.PermissionOverwrites = new System.Collections.Generic.List<Overwrite>
            {
                new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Deny)),
                new Overwrite(ticket.DiscordUserId ?? 0, PermissionTarget.User, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow)),
                new Overwrite(supportRoleId, PermissionTarget.Role, new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow))
            };
        });

        var newChannel = guild.GetTextChannel(newRestChannel.Id);
        if (newChannel == null)
        {
            Log.Information($"⚠️ Failed to retrieve newly created channel {channelName}.");
            return;
        }

        Log.Information($"✅ Created new channel {newChannel.Name}.");
        ticket.DiscordChannelId = newChannel.Id;
        await _dbContext.SaveChangesAsync();

        // Build and send the ticket embed
        var user = ticket.DiscordUserId.HasValue
            ? await _client.Rest.GetUserAsync(ticket.DiscordUserId.Value)
            : null;

        var embed = TicketEmbedFactory.BuildTicketEmbed(user, ticket);
        var buttons = TicketEmbedFactory.BuildTicketButtons(ticket.Id);

        await newChannel.SendMessageAsync(embed: embed, components: buttons.Build());

        // Generate and send the transcript
        var transcriptStream = await TranscriptBuilder.BuildTranscriptAsync(ticketId);
        if (transcriptStream == null)
        {
            await newChannel.SendMessageAsync("Ticket Opened from Website. No transcript available.");
            return;
        }

        transcriptStream.Position = 0;
        await newChannel.SendFileAsync(transcriptStream, $"Ticket#{ticketId}-Transcript.html");
        transcriptStream.Dispose();
    }
}
