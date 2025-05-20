using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace ZombieLynxBot.Interactions
{
    public class TicketOwnerSelectModule : InteractionModuleBase<SocketInteractionContext>
    {
        [ComponentInteraction("select_new_owner_*")]
        public async Task HandleOwnerSelection(string ticketIdRaw, string[] selectedValues)
        {
            await DeferAsync(ephemeral: true); // Avoid timeout

            if (selectedValues.Length == 0)
            {
                await FollowupAsync("No user selected.", ephemeral: true);
                return;
            }

            if (!int.TryParse(ticketIdRaw, out int ticketId))
            {
                await FollowupAsync("Failed to parse ticket ID.", ephemeral: true);
                return;
            }

            ulong newOwnerId;
            if (!ulong.TryParse(selectedValues[0], out newOwnerId))
            {
                await FollowupAsync("Invalid user ID selected.", ephemeral: true);
                return;
            }

            var channel = Context.Channel as SocketTextChannel;
            if (channel == null)
            {
                await FollowupAsync("This must be run in a ticket channel.", ephemeral: true);
                return;
            }

            using var db = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);

            // Find ticket in DB
            var ticket = db.Tickets.FirstOrDefault(t => t.Id == ticketId);
            if (ticket == null)
            {
                await FollowupAsync("Ticket not found in database.", ephemeral: true);
                return;
            }

            // Find ZLGMember for new owner
            var zlgMember = db.ZLGMembers.FirstOrDefault(z => z.DiscordId == newOwnerId.ToString());
            if (zlgMember == null)
            {
                await FollowupAsync("Selected user is not a ZLGMember.", ephemeral: true);
                return;
            }

            // Update ticket fields
            ticket.DiscordUserId = newOwnerId;
            ticket.UserProfileId = zlgMember.UserProfileId;

            // Remove all previous UserTickets for this ticket
            var userTickets = db.UserTickets.Where(ut => ut.TicketId == ticket.Id).ToList();
            foreach (var ut in userTickets)
                db.UserTickets.Remove(ut);

            // Add new owner to UserTickets
            db.UserTickets.Add(new UserTicket
            {
                TicketId = ticket.Id,
                UserProfileId = zlgMember.UserProfileId,
                AssignedAt = DateTime.UtcNow
            });

            db.SaveChanges();

            // Adjust channel permissions: allow new owner, but don't remove previous users
            var guildUser = channel.Guild.GetUser(newOwnerId);
            if (guildUser != null)
            {
                await channel.AddPermissionOverwriteAsync(guildUser, new OverwritePermissions(
                    viewChannel: PermValue.Allow,
                    sendMessages: PermValue.Allow
                ));
            }

            await FollowupAsync($"✅ Ticket ownership reassigned to <@{newOwnerId}>!", ephemeral: false);
        }
    }
}
