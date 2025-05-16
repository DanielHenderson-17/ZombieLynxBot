using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

public class PingModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Replies with Pong! 🏓!")]
    public async Task PingAsync()
    {
        await RespondAsync("Pong! 🏓", ephemeral: true);
    }
}
