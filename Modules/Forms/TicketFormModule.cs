// TicketFormModule.cs
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Serilog;

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

        Log.Information($"🔍 Debug: Category - {category}");
        Log.Information($"🔍 Debug: Selected Game - {game}");

        // Get servers from BotConfig
        var servers = Program.Config.GameServers.TryGetValue(game, out var serverList) ? serverList : new[] { "Other" };

        var selectMenu = new SelectMenuBuilder()
            .WithPlaceholder($"Select a Server for {displayGame}")
            .WithCustomId("select_ticket_server");

        foreach (var server in servers)
        {
            // Encode category and game into the value
            selectMenu.AddOption(server, $"{category}|{game}|{server.Replace("|", "~~")}");
            Log.Information($"✅ Debug: Added Server - {server}");
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
            Log.Information($"🔍 Debug: Received Interaction - select_ticket_server");
            Log.Information($"🔍 Debug: Selected Value - {selectedValue}");

            // Parse the encoded value
            var parts = selectedValue.Split('|');
            if (parts.Length < 3)
            {
                Log.Information($"❌ Error: Invalid value format - {selectedValue}");
                await RespondAsync("❌ An error occurred while processing your request. Please try again.", ephemeral: true);
                return;
            }

            string category = parts[0];
            string game = parts[1];
            string server = parts[2].Replace("~~", "|");

            // Convert SE → Ark:SE, SA → Ark:SA for display in the modal
            string displayGame = game switch
            {
                "SE" => "Ark:SE",
                "SA" => "Ark:SA",
                _ => game
            };

            Log.Information($"🔍 Debug: Parsed Category - {category}");
            Log.Information($"🔍 Debug: Parsed Game - {game}");
            Log.Information($"🔍 Debug: Parsed Server - {server}");

            var modal = new ModalBuilder()
                .WithTitle("Create a Ticket")
                .WithCustomId("ticket_submission")
                .AddTextInput("Subject", "subject", placeholder: "Enter a brief subject", minLength: 5, maxLength: 100, required: true)
                .AddTextInput("Category", "category", value: category, required: true)
                .AddTextInput("Game", "game", value: displayGame, required: true)
                .AddTextInput("Server", "server", value: server, required: true)
                .AddTextInput("Description", "description", TextInputStyle.Paragraph, "Describe your issue in detail", required: true);

            await RespondWithModalAsync(modal.Build());

            await Task.Delay(500);

            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Content = "✅ Thank you! Please fill out the form to complete your ticket.";
                msg.Components = new ComponentBuilder().Build();
            });


        }
        catch (Exception ex)
        {
            Log.Information($"❌ Exception in OpenTicketFormFinal: {ex}");
            await RespondAsync("❌ An unexpected error occurred. Please try again later.", ephemeral: true);
        }
    }
}


