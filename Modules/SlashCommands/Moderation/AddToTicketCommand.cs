using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace ZombieLynxBot.SlashCommands
{
    public class AddToTicketCommand : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("addtoticket", "Adds a user to this ticket.")]
        public async Task AddToTicket(SocketGuildUser userToAdd)
        {
            var caller = (SocketGuildUser)Context.User;

            // ✅ Permission check: admin role or admin user ID
            var isAdmin = caller.Roles.Any(role => role.Id == ulong.Parse(Program.Config.AdminRole))
                || Program.Config.Admins.Contains(caller.Id.ToString());

            if (!isAdmin)
            {
                await RespondAsync("❌ You do not have permission to execute this command.", ephemeral: true);
                return;
            }

            // ✅ Ticket channel check
            var channel = Context.Channel as SocketTextChannel;
            if (channel == null || !channel.Name.StartsWith("ticket-"))
            {
                await RespondAsync("❌ This command can only be used inside a ticket channel.", ephemeral: true);
                return;
            }

            // ✅ Try to add permission overwrite for the selected user
            var overwrite = channel.GetPermissionOverwrite(userToAdd);

            if (overwrite != null)
            {
                await RespondAsync("❌ This user already has access to the ticket.", ephemeral: true);
                return;
            }

            var perms = new OverwritePermissions(
                viewChannel: PermValue.Allow,
                sendMessages: PermValue.Allow,
                readMessageHistory: PermValue.Allow
            );

            await channel.AddPermissionOverwriteAsync(userToAdd, perms);

            await RespondAsync($"✅ {userToAdd.Mention} has been added to this ticket!", ephemeral: false);

        }
    }
}
