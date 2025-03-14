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
                .WithColor(Color.Green)
                .AddField("💬 **Suggestion:**", $"```{description}```", inline: false)
                .AddField("\u200B", $"**Vote closes in:** <t:{voteCloseTime}:R>", inline: true)
                .AddField(separator, "\u200B", inline: false)
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
            if (message == null || message.Embeds.Count == 0) return; // Ensure message has an embed

            var embed = message.Embeds.FirstOrDefault();
            if (embed == null) return;

            // ⛔ If the message is already locked, remove all reactions
            if (LockedMessages.Contains(message.Id))
            {
                Console.WriteLine($"⛔ Message {message.Id} is already locked. Removing reaction: {reaction.Emote.Name}");
                await message.RemoveReactionAsync(reaction.Emote, user);
                return;
            }

            // ✅ Ensure the user is an admin before proceeding
            bool isAdmin = user.Roles.Any(role => role.Id == ulong.Parse(Program.Config.AdminRole));
            if (!isAdmin) return;

            if (reaction.Emote.Name == "🔒")
            {
                await LockSuggestionAsync(message);
                return;
            }

            if (reaction.Emote.Name == "🚫")
            {
                await VetoSuggestionAsync(message, user.Username);
                return;
            }
        }

        public async Task VetoSuggestionAsync(IUserMessage message, string adminUsername)
        {
            var lockEmoji = new Emoji("🔒");
            var vetoEmoji = new Emoji("🚫");

            LockedMessages.Add(message.Id);
            Console.WriteLine($"🚫 Suggestion {message.Id} was vetoed by {adminUsername}");

            // ✅ React with 🔒 before modifying the embed
            await message.AddReactionAsync(lockEmoji);

            // ✅ Get the correct suggester's ID from our dictionary
            SuggestionMessageAuthors.TryGetValue(message.Id, out ulong suggesterId);
            string suggesterMention = suggesterId != 0 ? $"<@{suggesterId}>" : "Unknown User";

            // Retrieve the existing embed
            var embed = message.Embeds.FirstOrDefault();
            if (embed == null) return;

            var embedBuilder = new EmbedBuilder()
                .WithAuthor(embed.Author?.Name ?? "Unknown", embed.Author?.IconUrl)
                .WithThumbnailUrl(embed.Thumbnail?.Url)
                .WithColor(Color.Red)
                .WithDescription(embed.Description)
                .WithFooter($"🚫 Vetoed by {adminUsername}")
                .WithCurrentTimestamp();

            // ✅ Convert existing embed fields, but exclude "Vote closes in:"
            foreach (var field in embed.Fields)
            {
                if (!field.Value.Contains("Vote closes in:"))
                {
                    embedBuilder.AddField(field.Name, field.Value, field.Inline);
                }
            }

            // ✅ Set the veto result message
            string resultMessage = $"{suggesterMention}, ❌ **The suggestion has been vetoed by an admin due to likely not aligning with ZLG goals.**";

            // ✅ Add the veto message as a new field in the embed
            embedBuilder.AddField("\u200B", resultMessage, inline: false);

            // ✅ Edit the original message with the updated embed
            await message.ModifyAsync(msg => msg.Embed = embedBuilder.Build());
        }

        public async Task LockSuggestionAsync(IUserMessage message)
        {
            var upvote = new Emoji("⬆️");
            var downvote = new Emoji("⬇️");
            var lockEmoji = new Emoji("🔒");

            var upvoteCount = message.Reactions.ContainsKey(upvote) ? message.Reactions[upvote].ReactionCount - 1 : 0;
            var downvoteCount = message.Reactions.ContainsKey(downvote) ? message.Reactions[downvote].ReactionCount - 1 : 0;
            var totalVotes = upvoteCount + downvoteCount;

            LockedMessages.Add(message.Id);
            Console.WriteLine($"🔒 Auto-locking expired suggestion: {message.Id}");
            await message.AddReactionAsync(lockEmoji);

            // ✅ Get the correct suggester's ID from our dictionary
            SuggestionMessageAuthors.TryGetValue(message.Id, out ulong suggesterId);
            string suggesterMention = suggesterId != 0 ? $"<@{suggesterId}>" : "Unknown User";

            // Retrieve the existing embed
            var embed = message.Embeds.FirstOrDefault();
            if (embed == null) return;

            var embedBuilder = new EmbedBuilder()
                .WithAuthor(embed.Author?.Name ?? "Unknown", embed.Author?.IconUrl)
                .WithThumbnailUrl(embed.Thumbnail?.Url)
                .WithColor(embed.Color ?? Color.Default)
                .WithDescription(embed.Description)
                .WithFooter("🚫 Vote is now closed")
                .WithCurrentTimestamp();

            // ✅ Convert existing embed fields, but exclude "Vote closes in:"
            foreach (var field in embed.Fields)
            {
                if (!field.Value.Contains("Vote closes in:"))
                {
                    embedBuilder.AddField(field.Name, field.Value, field.Inline);
                }
            }

            // ✅ Determine the vote outcome
            string resultMessage;
            if (totalVotes < 5)
            {
                resultMessage = $"{suggesterMention}, ❌ **Suggestions require 5 votes minimum.**";
            }
            else if (upvoteCount > downvoteCount)
            {
                resultMessage = $"{suggesterMention}, ✅ **The vote has passed!**";
            }
            else
            {
                resultMessage = $"{suggesterMention}, ❌ **The vote did not pass.**";
            }

            // ✅ Add the voting results as a new field in the embed
            embedBuilder.AddField("\u200B", resultMessage, inline: false);

            // ✅ Edit the original message with the updated embed
            await message.ModifyAsync(msg => msg.Embed = embedBuilder.Build());
        }
    }
}
