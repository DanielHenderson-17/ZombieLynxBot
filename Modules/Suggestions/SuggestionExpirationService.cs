using Discord;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace ZombieLynxBot.Suggestions
{
    public class SuggestionExpirationService
    {
        private readonly DiscordSocketClient _client;
        private readonly SuggestionHandler _suggestionHandler;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

        public SuggestionExpirationService(DiscordSocketClient client, SuggestionHandler suggestionHandler)
        {
            _client = client;
            _suggestionHandler = suggestionHandler;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Log.Information("🔍 Checking for expired suggestions...");
                await CheckExpiredSuggestions();
                await Task.Delay(_checkInterval, cancellationToken);
            }
        }

        private async Task CheckExpiredSuggestions()
        {
            foreach (var guild in _client.Guilds)
            {
                foreach (var channel in guild.TextChannels)
                {
                    var messages = await channel.GetMessagesAsync(50).FlattenAsync();
                    foreach (var message in messages.OfType<IUserMessage>())
                    {
                        if (message.Embeds.Count == 0) continue;
                        var embed = message.Embeds.First();

                        var voteCloseField = embed.Fields.FirstOrDefault(f => f.Value.Contains("Vote closes in:"));
                        if (voteCloseField.Equals(default(EmbedField))) continue;

                        // Extract the timestamp from <t:XXXXXXXXXX:R>
                        var timestampText = voteCloseField.Value;
                        var unixTime = ExtractUnixTimestamp(timestampText);
                        if (unixTime == null) continue;

                        var voteCloseTime = DateTimeOffset.FromUnixTimeSeconds(unixTime.Value);
                        if (voteCloseTime <= DateTimeOffset.UtcNow)
                        {
                            Log.Information($"⏳ Locking expired suggestion: {message.Id}");
                            await _suggestionHandler.LockSuggestionAsync(message);
                        }
                    }
                }
            }
        }

        private long? ExtractUnixTimestamp(string text)
        {
            var start = text.IndexOf("<t:") + 3;
            var end = text.IndexOf(":R>");
            if (start == -1 || end == -1) return null;
            var timestampString = text.Substring(start, end - start);
            return long.TryParse(timestampString, out long timestamp) ? timestamp : (long?)null;
        }
    }
}
