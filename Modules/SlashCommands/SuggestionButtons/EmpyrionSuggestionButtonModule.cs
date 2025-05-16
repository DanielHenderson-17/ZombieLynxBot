using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace ZombieLynxBot.SlashCommands
{
    public class EmpyrionSuggestionButtonModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("suggestion-button-empyrion", "Creates a persistent 'Make a Suggestion' button for Empyrion")]
        public async Task CreateSuggestionButton()
        {
            var user = (SocketGuildUser)Context.User;

            if (!user.Roles.Any(role => role.Id == ulong.Parse(Program.Config.AdminRole)))
            {
                await RespondAsync("❌ You do not have permission to execute this command.", ephemeral: true);
                return;
            }

            var builder = new ComponentBuilder()
                .WithButton("Make a Suggestion", customId: "suggestion-modal-empyrion", style: ButtonStyle.Success, emote: new Emoji("📝"));

            await Context.Channel.SendMessageAsync("Click below to make a suggestion:", components: builder.Build());

            await RespondAsync("✅ Suggestion button created successfully!", ephemeral: true);
        }
    }
}
