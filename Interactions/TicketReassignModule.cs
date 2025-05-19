using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace ZombieLynxBot.Interactions
{
    public class TicketReassignModule : InteractionModuleBase<SocketInteractionContext>
    {
        [ComponentInteraction("reassign_owner_*")]
        public async Task HandleReassignOwnerButton(string ticketIdRaw)
        {
            await DeferAsync(ephemeral: true); // Avoid timeout

            int ticketId = int.Parse(ticketIdRaw);
            var channel = Context.Channel as SocketTextChannel;
            var guild = channel?.Guild;

            if (channel == null || guild == null)
            {
                await FollowupAsync("This must be run in a ticket channel.", ephemeral: true);
                return;
            }

            var userIds = new HashSet<ulong>();
            foreach (var overwrite in channel.PermissionOverwrites)
                if (overwrite.TargetType == PermissionTarget.User)
                    userIds.Add((ulong)overwrite.TargetId);

            foreach (var user in channel.Users)
                userIds.Add(user.Id);

            var usersInChannel = new List<IGuildUser>();
            foreach (var id in userIds)
            {
                var user = guild.GetUser(id);
                if (user != null)
                {
                    usersInChannel.Add(user);
                }
                else
                {
                    try
                    {
                        var restGuild = await Context.Client.Rest.GetGuildAsync(guild.Id);
                        var restUser = await restGuild.GetUserAsync(id);
                        if (restUser != null)
                            usersInChannel.Add(restUser);
                        else
                            Console.WriteLine($"[REST FAIL] Could not fetch user for ID: {id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Fetching user {id}: {ex.Message}");
                    }
                }
            }

            using var db = new TicketDbContext(Program.Config.TicketsDb.ConnectionString, Program.Config.TicketsDb.Provider);
            var eligibleDiscordIds = db.ZLGMembers.Select(z => z.DiscordId).ToHashSet();

            var selectMenu = new SelectMenuBuilder()
                .WithPlaceholder("Select the new ticket owner")
                .WithCustomId($"select_new_owner_{ticketId}");

            foreach (var user in usersInChannel.DistinctBy(u => u.Id))
            {
                if (!eligibleDiscordIds.Contains(user.Id.ToString()))
                    continue;

                var displayName = string.IsNullOrWhiteSpace(user.Nickname) ? user.Username : user.Nickname;
                displayName = UserNameFormatter.FormatNameUtils(displayName);

                selectMenu.AddOption(displayName, user.Id.ToString(), $"Assign ownership to {displayName}");
            }

            if (selectMenu.Options.Count == 0)
            {
                await FollowupAsync("No eligible users found in this ticket.", ephemeral: true);
                return;
            }

            var builder = new ComponentBuilder().WithSelectMenu(selectMenu);
            await FollowupAsync("Pick the new owner for this ticket:", components: builder.Build(), ephemeral: true);
        }

        [ComponentInteraction("select_new_owner_*")]
        public async Task HandleOwnerSelected(string ticketIdRaw, string[] selectedUserIds)
        {
            await DeferAsync(ephemeral: true);

            int ticketId = int.Parse(ticketIdRaw);
            ulong newOwnerId = ulong.Parse(selectedUserIds.First());

            IGuildUser guildUser = (Context.Guild as SocketGuild)?.GetUser(newOwnerId);

            if (guildUser == null)
            {
                var restGuild = await Context.Client.Rest.GetGuildAsync(Context.Guild.Id);
                guildUser = await restGuild.GetUserAsync(newOwnerId);

                if (guildUser == null)
                {
                    await FollowupAsync("❌ Could not find the selected user (even via REST).", ephemeral: true);
                    return;
                }
            }

            var ticketChannel = Context.Channel as SocketTextChannel;
            if (ticketChannel == null)
            {
                await FollowupAsync("❌ Invalid channel context.", ephemeral: true);
                return;
            }

            var originalMessage = await TicketEmbedUtils.FindTicketEmbedMessageAsync(ticketChannel);
            if (originalMessage == null)
            {
                await FollowupAsync("❌ Could not find the original ticket embed.", ephemeral: true);
                return;
            }

            var originalEmbed = originalMessage.Embeds.FirstOrDefault();
            if (originalEmbed == null)
            {
                await FollowupAsync("❌ Original embed is missing.", ephemeral: true);
                return;
            }
            var avatarUrl = guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl();
            Console.WriteLine($"Resolved avatar: {avatarUrl}");


            var updatedEmbed = new EmbedBuilder()
                .WithTitle(originalEmbed.Title)
                .WithDescription(originalEmbed.Description)
                .WithThumbnailUrl(originalEmbed.Thumbnail?.Url)
                .WithColor(originalEmbed.Color ?? Color.DarkGrey)
                .WithFooter($"{originalEmbed.Footer?.Text} (updated)", originalEmbed.Footer?.IconUrl)
                .WithTimestamp(originalEmbed.Timestamp ?? DateTimeOffset.UtcNow)
                .WithAuthor(guildUser.Username, avatarUrl);


            foreach (var field in originalEmbed.Fields)
            {
                updatedEmbed.AddField(field.Name, field.Value, field.Inline);
            }

            Console.WriteLine("Embed Update:");
            Console.WriteLine($"Author: {guildUser.Username}");
            Console.WriteLine($"Avatar: {guildUser.GetAvatarUrl()}");
            Console.WriteLine($"Title: {updatedEmbed.Title}");
            Console.WriteLine($"Fields: {updatedEmbed.Fields.Count}");

            await originalMessage.ModifyAsync(m => m.Embed = updatedEmbed.Build());


            await FollowupAsync($"✅ Ticket reassigned to {guildUser.Username}. Embed updated.", ephemeral: true);
        }
    }
}
