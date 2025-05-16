using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace ZombieLynxBot.SlashCommands
{
    public class DeleteMessageCommand : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("delete", "Deletes a message by ID and posts a reason.")]
        public async Task DeleteMessage(string messageId, string reason)
        {
            var user = (SocketGuildUser)Context.User;

            // ✅ Permission check
            if (!user.Roles.Any(role => role.Id == ulong.Parse(Program.Config.AdminRole)))
            {
                await RespondAsync("❌ You do not have permission to execute this command.", ephemeral: true);
                return;
            }

            // ✅ Parse message ID
            if (!ulong.TryParse(messageId, out var parsedId))
            {
                await RespondAsync("❌ Invalid message ID format.", ephemeral: true);
                return;
            }

            var channel = Context.Channel as SocketTextChannel;
            if (channel == null)
            {
                await RespondAsync("❌ This command can only be used in a text channel.", ephemeral: true);
                return;
            }

            var message = await channel.GetMessageAsync(parsedId);

            if (message == null)
            {
                await RespondAsync("❌ Could not find the specified message.", ephemeral: true);
                return;
            }

            // ✅ Send the embed announcement
            var embed = new EmbedBuilder()
                .WithDescription($"🛑 A message was deleted for the following reason:\n> {reason}")
                .WithColor(Color.Red)
                .Build();

            await channel.SendMessageAsync(embed: embed);

            // ✅ Delete the message
            await message.DeleteAsync();

            // ✅ Acknowledge the command silently
            await RespondAsync("✅ Message deleted and reason posted.", ephemeral: true);
        }
    }
}
