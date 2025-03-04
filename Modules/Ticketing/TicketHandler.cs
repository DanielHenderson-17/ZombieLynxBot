using System;
using System.Threading.Tasks;

public class TicketHandler
{
    private readonly TicketDbContext _dbContext;

    public TicketHandler()
    {
        _dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);
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
                Console.WriteLine($"❌ No ZLGMember found for Discord ID {discordUserId}");
                return null; // If no profile, we can't create a ticket
            }

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
                UserProfileId = member.UserProfileId // ✅ Assign UserProfileId from ZLGMember
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
}
