using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace ZombieLynxBot.Suggestions
{
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

            var suggesterId = Context.User.Id;
            var suggesterMention = Context.User.Mention;
            var suggesterName = Context.User.Username;
            var suggesterAvatar = Context.User.GetAvatarUrl();
            var maxWidth = 49;
            var separator = new string('─', maxWidth);
            var suggesterNameFormatted = char.ToUpper(suggesterName[0]) + suggesterName.Substring(1);
            var voteCloseTime = DateTimeOffset.UtcNow.AddDays(5).ToUnixTimeSeconds();


            var embed = new EmbedBuilder()
                .WithAuthor(suggesterNameFormatted, suggesterAvatar)
                .WithThumbnailUrl("https://i.imgur.com/dnlokbX.png")
                .WithDescription($"{separator}")
                .WithColor(Color.Green)
                .AddField("💬 **Suggestion:**", $"```{description}```", inline: false)
                .AddField("\u200B", $"**Vote closes in:** <t:{voteCloseTime}:R>", inline: true)
                .WithFooter($"React below to vote!", null)
                .WithCurrentTimestamp()
                .Build();



            var suggestionMessage = await channel.SendMessageAsync(embed: embed);

            // Store the suggester’s ID for later use when locking
            SuggestionMessageAuthors[suggestionMessage.Id] = suggesterId;

            // Add voting reactions
            await suggestionMessage.AddReactionAsync(new Emoji("⬆️"));
            await suggestionMessage.AddReactionAsync(new Emoji("⬇️"));

            await RespondAsync($"✅ Your suggestion was successfully submitted to {channel.Mention}.", ephemeral: true);
        }

        // ✅ Store message authors globally so we can use them when locking
        private static readonly Dictionary<ulong, ulong> SuggestionMessageAuthors = new();

        private string GetSuggestionChannelName(string gameKey)
        {
            return gameKey.ToLower() switch
            {
                "ase" => "✍︱ark-server-suggestions",
                "asa" => "✍︱asa-server-suggestions",
                "eco" => "✍︱eco-server-suggestions",
                "minecraft" => "✍︱minecraft-server-suggestions",
                "empyrion" => "✍︱empyrion-server-suggestions",
                "rust" => "✍︱rust-server-suggestions",
                "game" => "💬︱game-suggestions",
                _ => ""
            };
        }
        private static readonly HashSet<ulong> LockedMessages = new();

        public async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> messageCache, Cacheable<IMessageChannel, ulong> channelCache, SocketReaction reaction)
        {
            var guild = (reaction.Channel as SocketTextChannel)?.Guild;
            if (guild == null) return;

            var user = reaction.User.IsSpecified ? reaction.User.Value as SocketGuildUser : null;
            if (user == null || user.IsBot) return;

            var message = await messageCache.GetOrDownloadAsync();
            if (message == null) return;

            // ⛔ If the message is locked, remove all reactions
            if (LockedMessages.Contains(message.Id))
            {
                Console.WriteLine($"⛔ Message {message.Id} is locked. Removing reaction: {reaction.Emote.Name}");
                await message.RemoveReactionAsync(reaction.Emote, user);
                return;
            }
            // ✅ If an admin reacts with 🔒, lock the message
            if (reaction.Emote.Name == "🔒" && user.Roles.Any(role => role.Id == ulong.Parse(Program.Config.AdminRole)))
            {
                var upvote = new Emoji("⬆️");
                var downvote = new Emoji("⬇️");

                var upvoteCount = message.Reactions.ContainsKey(upvote) ? message.Reactions[upvote].ReactionCount - 1 : 0;
                var downvoteCount = message.Reactions.ContainsKey(downvote) ? message.Reactions[downvote].ReactionCount - 1 : 0;

                var totalVotes = upvoteCount + downvoteCount;

                LockedMessages.Add(message.Id);
                Console.WriteLine($"🔒 Message {message.Id} has been locked by {user.Username}");

                // ✅ Get the correct suggester's ID from our dictionary
                SuggestionMessageAuthors.TryGetValue(message.Id, out ulong suggesterId);
                string suggesterMention = suggesterId != 0 ? $"<@{suggesterId}>" : "Unknown User";

                // Case 1: Not enough votes
                if (totalVotes < 5)
                {
                    await message.ReplyAsync($"{suggesterMention} 🔒 There are not enough votes to implement this at this time. Please attempt to get more interest and try another vote later.");
                    return;
                }

                // Case 2: Thumbs up wins
                if (upvoteCount > downvoteCount)
                {
                    await message.ReplyAsync($"{suggesterMention} ✅ The vote has passed. It will be implemented when allowable.");
                }
                // Case 3: Thumbs down wins
                else
                {
                    await message.ReplyAsync($"{suggesterMention} ❌ The vote did not pass. We can take a look at this in the future once there is more interest.");
                }
            }
        }

    }
}
