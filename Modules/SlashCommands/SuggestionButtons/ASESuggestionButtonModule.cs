using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace ZombieLynxBot.SlashCommands
{
    public class ASESuggestionButtonModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("suggestion-button-ase", "Creates a persistent 'Make a Suggestion' button for Ark SE")]
        public async Task CreateSuggestionButton()
        {
            var user = (SocketGuildUser)Context.User;

            // No injected dependencies—just accessing your existing static Config
            if (!user.Roles.Any(role => role.Id == ulong.Parse(Program.Config.AdminRole)))
            {
                await RespondAsync("❌ You do not have permission to execute this command.", ephemeral: true);
                return;
            }

            var builder = new ComponentBuilder()
                .WithButton("Make a Suggestion", customId: "suggestion-modal-ase", style: ButtonStyle.Success, emote: new Emoji("📝"));

            await Context.Channel.SendMessageAsync("Click below to make a suggestion:", components: builder.Build());

            await RespondAsync("✅ Suggestion button created successfully!", ephemeral: true);
        }
    }
}
