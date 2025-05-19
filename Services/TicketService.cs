using System;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

public class TicketService
{
    private readonly TicketDbContext _dbContext;

    public TicketService()
    {
        _dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);
    }

    public Ticket? GetTicketById(int ticketId)
    {
        return _dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
    }

    public async Task<Ticket> CreateTicketAsync(
        string subject,
        string category,
        string game,
        string server,
        string description,
        ulong discordUserId,
        string discordUserName)
    {
        try
        {
            var member = _dbContext.ZLGMembers.FirstOrDefault(m => m.DiscordId == discordUserId.ToString());

            var ticket = new Ticket
            {
                Subject = subject,
                Category = category,
                Game = game,
                Server = server,
                Description = description,
                Status = "Open",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UserProfileId = member?.UserProfileId,
                DiscordUserId = discordUserId
            };

            _dbContext.Tickets.Add(ticket);
            await _dbContext.SaveChangesAsync();

            if (member != null)
            {
                var userTicket = new UserTicket
                {
                    TicketId = ticket.Id,
                    UserProfileId = member.UserProfileId,
                    AssignedAt = DateTime.UtcNow
                };

                _dbContext.UserTickets.Add(userTicket);
                await _dbContext.SaveChangesAsync();

                Log.Information($"✅ Ticket {ticket.Id} created and linked to user.");
            }
            else
            {
                Log.Information($"✅ Ticket {ticket.Id} created without user link (no matching ZLGMember).");
            }

            return ticket;
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Error creating ticket: {ex.Message}");
            if (ex.InnerException != null)
                Log.Information($"🔍 Inner Exception: {ex.InnerException.Message}");
            throw;
        }
    }

    public async Task UpdateTicketWithChannelId(int ticketId, ulong channelId)
    {
        try
        {
            var ticket = _dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
            if (ticket == null)
            {
                Log.Information($"❌ Ticket ID {ticketId} not found.");
                return;
            }

            ticket.DiscordChannelId = channelId;
            ticket.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            Log.Information($"✅ Ticket {ticketId} updated with channel ID {channelId}.");
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Error updating ticket: {ex.Message}");
            if (ex.InnerException != null)
                Log.Information($"🔍 Inner Exception: {ex.InnerException.Message}");
        }
    }

    public async Task<bool> CloseTicketAsync(int ticketId)
    {
        var ticket = _dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
            return false;

        ticket.Status = "Closed";
        ticket.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        Log.Information($"✅ Ticket {ticketId} marked as closed.");
        return true;
    }

    public async Task<bool> MarkTicketAsClosedAsync(int ticketId)
    {
        var ticket = _dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
            return false;

        ticket.Status = "Closed";
        ticket.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        Log.Information($"✅ [Sync] Ticket {ticketId} marked as closed in DB.");
        return true;
    }

}
