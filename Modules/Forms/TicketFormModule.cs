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
            .AddOption("Discord Issue", $"Discord Issue|{category}")
            .AddOption("Ark:SE", $"SE|{category}")
            .AddOption("Ark:SA", $"SA|{category}")
            .AddOption("Eco", $"Eco|{category}")
            .AddOption("Minecraft", $"Minecraft|{category}")
            .AddOption("Empyrion", $"Empyrion|{category}")
            .AddOption("Palworld", $"Palworld|{category}");

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

        // Parse the selected value to get game and category
        var parts = selectedValue.Split('|');
        string game = parts[0];
        string category = parts.Length > 1 ? parts[1] : "Unknown";

        // Convert SE → Ark:SE, SA → Ark:SA but keep original keys for server lookup
        string displayGame = game switch
        {
            "SE" => "Ark:SE",
            "SA" => "Ark:SA",
            _ => game
        };

        Console.WriteLine($"🔍 Debug: Category - {category}");
        Console.WriteLine($"🔍 Debug: Selected Game - {game}");

        // Get servers from BotConfig
        var servers = Program.Config.GameServers.TryGetValue(game, out var serverList) ? serverList : new[] { "Other" };

        var selectMenu = new SelectMenuBuilder()
            .WithPlaceholder($"Select a Server for {displayGame}")
            .WithCustomId("select_ticket_server");

        foreach (var server in servers)
        {
            // Encode category and game into the value
            selectMenu.AddOption(server, $"{category}|{game}|{server}");
            Console.WriteLine($"✅ Debug: Added Server - {server}");
        }

        var component = new ComponentBuilder().WithSelectMenu(selectMenu).Build();

        await ModifyOriginalResponseAsync(msg =>
        {
            msg.Content = $"Please select a server for {displayGame}:";
            msg.Components = component;
        });
    }

    // Step 4: Show Final Modal after choosing a Server
    [ComponentInteraction("select_ticket_server")]
    public async Task OpenTicketFormFinal(string selectedValue)
    {
        try
        {
            Console.WriteLine($"🔍 Debug: Received Interaction - select_ticket_server");
            Console.WriteLine($"🔍 Debug: Selected Value - {selectedValue}");

            // Parse the encoded value
            var parts = selectedValue.Split('|');
            if (parts.Length < 3)
            {
                Console.WriteLine($"❌ Error: Invalid value format - {selectedValue}");
                await RespondAsync("❌ An error occurred while processing your request. Please try again.", ephemeral: true);
                return;
            }

            string category = parts[0];
            string game = parts[1];
            string server = parts[2];

            // Convert SE → Ark:SE, SA → Ark:SA for display in the modal
            string displayGame = game switch
            {
                "SE" => "Ark:SE",
                "SA" => "Ark:SA",
                _ => game
            };

            Console.WriteLine($"🔍 Debug: Parsed Category - {category}");
            Console.WriteLine($"🔍 Debug: Parsed Game - {game}");
            Console.WriteLine($"🔍 Debug: Parsed Server - {server}");

            var modal = new ModalBuilder()
                .WithTitle("Create a Ticket")
                .WithCustomId("ticket_submission")
                .AddTextInput("Subject", "subject", placeholder: "Enter a brief subject", minLength: 5, maxLength: 100, required: true)
                .AddTextInput("Category", "category", value: category, required: true) // Pre-filled
                .AddTextInput("Game", "game", value: displayGame, required: true) // Use displayGame instead of game
                .AddTextInput("Server", "server", value: server, required: true) // Pre-filled
                .AddTextInput("Description", "description", TextInputStyle.Paragraph, "Describe your issue in detail", required: true);

            await RespondWithModalAsync(modal.Build());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Exception in OpenTicketFormFinal: {ex}");
            await RespondAsync("❌ An unexpected error occurred. Please try again later.", ephemeral: true);
        }
    }
}