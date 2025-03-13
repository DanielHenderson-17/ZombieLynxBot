using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace ZombieLynxBot.Forms
{
    public class SuggestionFormModule : InteractionModuleBase<SocketInteractionContext>
    {
        [ComponentInteraction("suggestion-modal-*")]
        public async Task HandleSuggestionButton(string gameKey)
        {
            var modal = new ModalBuilder()
                .WithTitle($"New Suggestion for {gameKey.ToUpper()}")
                .WithCustomId($"submit-suggestion-{gameKey}")
                .AddTextInput("Describe your suggestion", "suggestion-description", TextInputStyle.Paragraph, placeholder: "Describe your suggestion clearly...", required: true, maxLength: 1500)
                .Build();

            await RespondWithModalAsync(modal);
        }
    }
}