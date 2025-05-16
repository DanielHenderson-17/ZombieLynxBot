using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace ZombieLynxBot.Forms
{
    public class SuggestionFormModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly TicketDbContext _db;

        public SuggestionFormModule(TicketDbContext db)
        {
            _db = db;
        }

        [ComponentInteraction("suggestion-modal-*")]
        public async Task HandleSuggestionButton(string gameKey)
        {
            var discordId = Context.User.Id.ToString();

            var member = await _db.ZLGMembers.FirstOrDefaultAsync(z => z.DiscordId == discordId);
            if (member == null)
            {
                await RespondAsync("🚫 You must register a ZLG account before making suggestions. Sign up here: https://zlg.gg/login", ephemeral: true);
                return;
            }

            var modal = new ModalBuilder()
                .WithTitle($"New Suggestion for {gameKey.ToUpper()}")
                .WithCustomId($"submit-suggestion-{gameKey}")
                .AddTextInput("Describe your suggestion", "suggestion-description", TextInputStyle.Paragraph, placeholder: "Describe your suggestion clearly...", required: true, maxLength: 1500)
                .Build();

            await RespondWithModalAsync(modal);
        }
    }
}
