using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace ZombieLynxBot.Suggestions
{
    // Define the modal inputs clearly in a separate class here
    public class SuggestionModal : IModal
    {
        public string Title => "New Suggestion";

        [InputLabel("Suggestion Title")]
        [ModalTextInput("suggestion-title", TextInputStyle.Short, "Enter a clear short title", maxLength: 100)]
        public string TitleInput { get; set; }

        [InputLabel("Suggestion Details")]
        [ModalTextInput("suggestion-description", TextInputStyle.Paragraph, "Describe your suggestion clearly...", maxLength: 1500)]
        public string DescriptionInput { get; set; }
    }

    public class SuggestionHandler : InteractionModuleBase<SocketInteractionContext>
    {
        [ModalInteraction("submit-suggestion-*")]
        public async Task HandleSuggestionSubmission(string gameKey, SuggestionModal modal)
        {
            var title = modal.TitleInput;
            var description = modal.DescriptionInput;

            if (!Program.Config.SuggestionsChannels.TryGetValue(GetSuggestionChannelName(gameKey), out string channelId))
            {
                await RespondAsync("⚠️ Suggestion channel not configured properly.", ephemeral: true);
                return;
            }

            var channel = Context.Guild.GetTextChannel(ulong.Parse(channelId));
            if (channel == null)
            {
                await RespondAsync("⚠️ Suggestion channel not found.", ephemeral: true);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle($"📌 {title}")
                .WithDescription(description)
                .WithFooter($"Suggested by {Context.User.Username}", Context.User.GetAvatarUrl())
                .WithColor(Color.Orange)
                .WithCurrentTimestamp()
                .Build();

            var suggestionMessage = await channel.SendMessageAsync(embed: embed);

            // Add voting reactions clearly
            await suggestionMessage.AddReactionAsync(new Emoji("👍"));
            await suggestionMessage.AddReactionAsync(new Emoji("👎"));

            await RespondAsync($"✅ Your suggestion was successfully submitted to {channel.Mention}.", ephemeral: true);
        }

        private string GetSuggestionChannelName(string gameKey)
        {
            return gameKey.ToLower() switch
            {
                "ase" => "✍︱ark-server-suggestions",
                "asa" => "✍︱asa-server-suggestions",
                "eco" => "✍︱eco-server-suggestions",
                "minecraft" => "✍︱minecraft-server-suggestions",
                "empyrion" => "✍︱empyrion-server-suggestions",
                "game" => "💬︱game-suggestions",
                _ => ""
            };
        }
    }
}
