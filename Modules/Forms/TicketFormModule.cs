using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;

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

        // ✅ Send a new message instead of modifying the button message
        await RespondAsync("Please select a category:", components: component, ephemeral: true);
    }

    // Step 2: Show Game Selection after choosing a Category
    [ComponentInteraction("select_ticket_category")]
    public async Task ShowGameSelection(string selectedValue)
    {
        await DeferAsync(ephemeral: true);

        string category = selectedValue; // ✅ Store only the category

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

        Console.WriteLine($"🔍 Debug: Selected Game - {selectedValue}");

        string game = selectedValue; // ✅ Use the actual selected game
        string category = "Unknown"; // ✅ Category is not stored in this step

        var servers = game switch
        {
            "Discord Issue" => new[] { "Zombie Lynx Gaming Discord" },
            "SE" => new[]
            {
            "ZombieLynx-TheIsland-3X-PVPClusterORP",
            "ZombieLynx-Extinction-3X-PVPClusterORP",
            "ZombieLynx-Aberration-3X-PVPClusterORP",
            "ZombieLynx-Gen2-3X-PVPClusterORP",
            "ZombieLynx-Fjordur-3X-PVPClusterORP",
            "ZombieLynx-CrystalIsles-3X-PVPClusterORP",
            "ZombieLynx-ScorchedEarth-3X-PVPClusterORP",
            "ZombieLynx-LostIsland-3X-PVPClusterORP",
            "ZombieLynx-Gen1-3X-PVPClusterORP",
            "ZombieLynx-ThCenter-3X-PVPClusterORP",
            "ZombieLynx-Ragnarok-3X-PVPClusterORP",
            "ZombieLynx-Valguero-3X-PVPClusterORP"
        },
            "SA" => new[]
            {
            "ZombieLynx-TheIsland-3X-PVP",
            "ZombieLynx-ScorchedEarth-3X-PVPORP",
            "ZombieLynx-TheCenter-3X-PVP",
            "ZombieLynx-Aberration-3X-PVPORP"
        },
            "Eco" => new[] { "Zombie Lynx Gaming | Medium Collab | Beginner Friendly" },
            "Minecraft" => new[] { "Zombie Lynx Gaming Minecraft" },
            "Empyrion" => new[] { "Zombie Lynx Reforged Eden PVP" },
            "Palworld" => new[] { "Zombie Lynx Gaming Palworld 3X" },
            _ => new[] { "Other" }
        };

        Console.WriteLine($"🔍 Debug: Server List Count for {game} - {servers.Length}");

        var selectMenu = new SelectMenuBuilder()
            .WithPlaceholder($"Select a Server for {game}")
            .WithCustomId("select_ticket_server"); // ✅ Fixed CustomId

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
            msg.Content = $"Please select a server for {game}:";
            msg.Components = component;
        });
    }

    // Step 4: Show Final Modal after choosing a Server
    [ComponentInteraction("select_ticket_server:*:*")]
    public async Task OpenTicketFormFinal(string selectedValue, string customId)
    {
        string server = selectedValue;
        var parts = customId.Split(':');
        string category = parts[1];
        string game = parts[2];

        var modal = new ModalBuilder()
            .WithTitle("Create a Ticket")
            .WithCustomId("ticket_submission")
            .AddTextInput("Subject", "subject", placeholder: "Enter a brief subject", minLength: 5, maxLength: 100, required: true)
            .AddTextInput("Category", "category", value: category, required: true) // Pre-filled
            .AddTextInput("Game", "game", value: game, required: true) // Pre-filled
            .AddTextInput("Server", "server", value: server, required: true) // Pre-filled
            .AddTextInput("Description", "description", TextInputStyle.Paragraph, "Describe your issue in detail", required: true);

        await RespondWithModalAsync(modal.Build());
    }
}
