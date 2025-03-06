using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

class Program
{
    private DiscordSocketClient _client;
    private InteractionService _commands;
    private IServiceProvider _services;
    private BotConfig _config;

    // Make the config accessible globally
    public static BotConfig Config { get; private set; } = new BotConfig();

    static async Task Main(string[] args) => await new Program().RunBotAsync();

    public async Task RunBotAsync()
    {
        LoadConfig();

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
        });

        _commands = new InteractionService(_client.Rest);

        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.InteractionCreated += HandleInteractionAsync;

        await _client.LoginAsync(TokenType.Bot, _config.Token);
        await _client.StartAsync();

        // Register commands after bot is ready
        _client.Ready += RegisterCommandsAsync;

        await Task.Delay(-1);
    }

    private void LoadConfig()
    {
        var configText = File.ReadAllText("botconfig.json");
        _config = JsonSerializer.Deserialize<BotConfig>(configText);

        // Set the global Config property
        Config = _config;
    }

    private async Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log);
    }

    private async Task ReadyAsync()
    {
        Console.WriteLine($"✅ Logged in as {_client.CurrentUser.Username}");

        try
        {
            using var dbContext = new TicketDbContext(Config.TicketsDb.ConnectionString, Config.TicketsDb.Provider);
            dbContext.Database.EnsureCreated();
            Console.WriteLine("✅ Successfully connected to PostgreSQL database.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Database connection failed: {ex.Message}");
        }
    }


    private async Task RegisterCommandsAsync()
    {
        await _commands.AddModulesAsync(typeof(Program).Assembly, _services);
        Console.WriteLine("✅ Commands and interactions loaded.");
        foreach (var guild in _client.Guilds)
        {
            await _commands.RegisterCommandsToGuildAsync(guild.Id, true);
        }
        Console.WriteLine("✅ Slash commands registered.");
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        var context = new SocketInteractionContext(_client, interaction);
        await _commands.ExecuteCommandAsync(context, _services);
    }

    public class BotConfig
    {
        public string Token { get; set; }
        public Dictionary<string, string[]> GameServers { get; set; }

        public TicketsDbConfig TicketsDb { get; set; }
        public string SupportChannelId { get; set; }  // ✅ Add this
        public Dictionary<string, string> SupportRole { get; set; }  // ✅ Add this
        public Dictionary<string, string> SupportCategory { get; set; }  // ✅ Add this
    }

    public class TicketsDbConfig
    {
        public string ConnectionString { get; set; }
        public string Provider { get; set; }
    }


}