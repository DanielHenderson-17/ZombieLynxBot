using System;
using System.Threading.Tasks;

public class TicketHandler
{
    private readonly TicketDbContext _dbContext;

    public TicketHandler()
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
            // 🔍 Look up ZLGMember using the Discord ID
            var member = _dbContext.ZLGMembers.FirstOrDefault(m => m.DiscordId == discordUserId.ToString());

            if (member == null)
            {
                Console.WriteLine($"❌ No ZLGMember found for Discord ID {discordUserId}. Creating a ticket WITHOUT user assignment.");

                // ✅ Create a ticket with NO UserProfileId
                var ticketWithoutUser = new Ticket
                {
                    Subject = subject,
                    Category = category,
                    Game = game,
                    Server = server,
                    Description = description,
                    Status = "Open",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    UserProfileId = null,
                    DiscordUserId = discordUserId
                };

                _dbContext.Tickets.Add(ticketWithoutUser);
                await _dbContext.SaveChangesAsync();

                Console.WriteLine($"✅ Ticket {ticketWithoutUser.Id} saved (NO user linked).");
                return ticketWithoutUser;
            }

            // ✅ If the user exists, proceed normally
            var newTicket = new Ticket
            {
                Subject = subject,
                Category = category,
                Game = game,
                Server = server,
                Description = description,
                Status = "Open",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UserProfileId = member.UserProfileId,
                DiscordUserId = discordUserId
            };

            _dbContext.Tickets.Add(newTicket);
            await _dbContext.SaveChangesAsync();

            // ✅ Assign the user to the ticket in `UserTickets`
            var userTicket = new UserTicket
            {
                TicketId = newTicket.Id,
                UserProfileId = member.UserProfileId,
                AssignedAt = DateTime.UtcNow
            };

            _dbContext.UserTickets.Add(userTicket);
            await _dbContext.SaveChangesAsync();

            Console.WriteLine($"✅ Ticket {newTicket.Id} successfully saved in DB and assigned to user.");
            return newTicket;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error saving ticket: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"🔍 Inner Exception: {ex.InnerException.Message}");
            }
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
                Console.WriteLine($"❌ Ticket with ID {ticketId} not found.");
                return;
            }

            ticket.DiscordChannelId = channelId;
            ticket.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            Console.WriteLine($"✅ Ticket {ticketId} updated with Discord Channel ID: {channelId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error updating ticket: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"🔍 Inner Exception: {ex.InnerException.Message}");
            }
        }
    }
    public async Task<bool> CloseTicketAsync(int ticketId)
    {
        var ticket = _dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null) return false;

        ticket.Status = "Closed";
        ticket.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return true;
    }
}
