// TicketFormModule.cs
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

public class TicketFormModule : InteractionModuleBase<SocketInteractionContext>
{
    // Step 1: Show Category Selection
    [ComponentInteraction("open_ticket_form")]
    public async Task ShowCategorySelection()
    {
        var selectMenu = new SelectMenuBuilder()
            .WithPlaceholder("Select a Ticket Category")
            .WithCustomId("select_ticket_category")
            .AddOption("Bug", "Bug")
            .AddOption("Shop Issue", "Shop Issue")
            .AddOption("Connection Issue", "Connection Issue")
            .AddOption("Other", "Other");

        var component = new ComponentBuilder()
            .WithSelectMenu(selectMenu)
            .Build();

        await RespondAsync("Please select a category:", components: component, ephemeral: true);
    }

    // Step 2: Show Game Selection after choosing a Category
    [ComponentInteraction("select_ticket_category")]
    public async Task ShowGameSelection(string selectedValue)
    {
        await DeferAsync(ephemeral: true);

        string category = selectedValue;

        var selectMenu = new SelectMenuBuilder()
            .WithPlaceholder("Select a Game")
            .WithCustomId("select_ticket_game")
            .AddOption("Discord Issue", "Discord Issue")
            .AddOption("Ark:SE", "SE")
            .AddOption("Ark:SA", "SA")
            .AddOption("Eco", "Eco")
            .AddOption("Minecraft", "Minecraft")
            .AddOption("Empyrion", "Empyrion")
            .AddOption("Palworld", "Palworld");

        var component = new ComponentBuilder()
            .WithSelectMenu(selectMenu)
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Content = "Please select a game:";
            msg.Components = component;
        });
    }

    // Step 3: Show Server Selection after choosing a Game
    [ComponentInteraction("select_ticket_game")]
    public async Task ShowServerSelection(string selectedValue)
    {
        await DeferAsync(ephemeral: true);

        // Convert SE → Ark:SE, SA → Ark:SA but keep original keys for server lookup
        string displayGame = selectedValue switch
        {
            "SE" => "Ark:SE",
            "SA" => "Ark:SA",
            _ => selectedValue
        };

        string game = selectedValue; // Keep original for server lookup

        string category = (Context.Interaction as SocketMessageComponent)?.Data.CustomId.Split(':').ElementAtOrDefault(1) ?? "Unknown";

        // Get servers from the BotConfig
        var servers = Program.Config.GameServers.TryGetValue(game, out var serverList) ? serverList : new[] { "Other" };

        Console.WriteLine($"🔍 Debug: Server List Count for {game} - {servers.Length}");
        Console.WriteLine($"🔍 Debug: Selected Game - {selectedValue} (Stored as {game})");
        Console.WriteLine($"🔍 Debug: Display Name - {displayGame}");
        Console.WriteLine($"🔍 Debug: Available Servers for {game}: {string.Join(", ", servers)}");

        var selectMenu = new SelectMenuBuilder()
            .WithPlaceholder($"Select a Server for {displayGame}")
            .WithCustomId($"select_ticket_server:{category}:{game}");

        foreach (var server in servers)
        {
            selectMenu.AddOption(server, server);
            Console.WriteLine($"✅ Debug: Added Server - {server}");
        }

        var component = new ComponentBuilder()
            .WithSelectMenu(selectMenu)
            .Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Content = $"Please select a server for {displayGame}:";
            msg.Components = component;
        });
    }

    // Step 4: Show Final Modal after choosing a Server
    [ComponentInteraction("select_ticket_server:*:*")]
    public async Task OpenTicketFormFinal(string selectedValue, string customId)
    {
        try
        {
            Console.WriteLine($"🔍 Debug: Received Interaction - select_ticket_server");
            Console.WriteLine($"🔍 Debug: CustomId - {customId}");
            Console.WriteLine($"🔍 Debug: Selected Server - {selectedValue}");

            var parts = customId.Split(':');
            if (parts.Length < 3)
            {
                Console.WriteLine($"❌ Error: Invalid customId format - {customId}");
                await RespondAsync("❌ An error occurred while processing your request. Please try again.", ephemeral: true);
                return;
            }

            string category = parts[1];
            string game = parts[2];

            Console.WriteLine($"🔍 Debug: Split Parts Length - {parts.Length}");
            Console.WriteLine($"🔍 Debug: Category - {category}");
            Console.WriteLine($"🔍 Debug: Game - {game}");

            var modal = new ModalBuilder()
                .WithTitle("Create a Ticket")
                .WithCustomId("ticket_submission")
                .AddTextInput("Subject", "subject", placeholder: "Enter a brief subject", minLength: 5, maxLength: 100, required: true)
                .AddTextInput("Category", "category", value: category, required: true) // Pre-filled
                .AddTextInput("Game", "game", value: game, required: true) // Pre-filled
                .AddTextInput("Server", "server", value: selectedValue, required: true) // Pre-filled
                .AddTextInput("Description", "description", TextInputStyle.Paragraph, "Describe your issue in detail", required: true);

            Console.WriteLine("🔍 Debug: Constructed Modal Data:");
            Console.WriteLine($"🔍 Subject: (User Input)");
            Console.WriteLine($"🔍 Category: {category}");
            Console.WriteLine($"🔍 Game: {game}");
            Console.WriteLine($"🔍 Server: {selectedValue}");
            Console.WriteLine("🔍 Awaiting user input in modal...");

            await RespondWithModalAsync(modal.Build());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception in OpenTicketFormFinal: {ex}");
            await RespondAsync("❌ An unexpected error occurred. Please try again later.", ephemeral: true);
        }
    }
}