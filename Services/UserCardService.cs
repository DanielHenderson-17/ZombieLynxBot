using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Serilog;

public class UserCardService
{
    public async Task SendUserCardAsync(SocketInteractionContext context, int ticketId)
    {
        await context.Interaction.DeferAsync(ephemeral: true);

        using var dbContext = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);

        var ticket = dbContext.Tickets.FirstOrDefault(t => t.Id == ticketId);
        if (ticket == null)
        {
            await context.Interaction.FollowupAsync("❌ Ticket not found.", ephemeral: true);
            return;
        }

        var member = dbContext.ZLGMembers.FirstOrDefault(m => m.DiscordId == ticket.DiscordUserId.ToString());
        if (member == null)
        {
            await context.Interaction.FollowupAsync("❌ Player info not found.", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithAuthor(UserNameFormatter.FormatNameUtils(member.DiscordName), member.DiscordImgUrl ?? "https://i.imgur.com/dnlokbX.png")
            .WithThumbnailUrl(member.DiscordImgUrl ?? "https://i.imgur.com/dnlokbX.png")
            .WithColor(Color.Blue)
            .WithCurrentTimestamp();

        embed.AddField("💰 Points", member.Points.ToString("N0"), false);

        if (!string.IsNullOrWhiteSpace(member.SteamName))
            embed.AddField("🧊 Steam", $"{member.SteamName}\n`{member.SteamId}`", false);

        if (!string.IsNullOrWhiteSpace(member.MinecraftUsername))
            embed.AddField("⛏️ Minecraft", $"{member.MinecraftUsername}\n`{member.MinecraftUuid}`", false);

        if (!string.IsNullOrWhiteSpace(member.EpicName))
            embed.AddField("🎮 Epic", $"{member.EpicName}\n`{member.EosId}`", false);

        if (!string.IsNullOrWhiteSpace(member.TimedPermissionGroups))
        {
            var group = member.TimedPermissionGroups.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(group))
            {
                string icon = group.ToLower() switch
                {
                    var g when g.Contains("vibranium") => "🟣",
                    var g when g.Contains("diamond") => "🔷",
                    var g when g.Contains("gold") => "🟡",
                    _ => "🔘"
                };

                embed.AddField("🛡️ Membership", $"{icon} {group}", true);
            }
        }

        await context.Interaction.FollowupAsync(embed: embed.Build());
    }
}
