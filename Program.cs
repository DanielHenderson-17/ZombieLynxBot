using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using ZombieLynxBot.Suggestions;


class Program
{
    private DiscordSocketClient _client;
    private InteractionService _commands;
    private IServiceProvider _services;
    private BotConfig _config;

    public static BotConfig Config { get; private set; } = new BotConfig();

    static async Task Main(string[] args) => await new Program().RunBotAsync();

    public async Task RunBotAsync()
    {
        LoadConfig();

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds |
                             GatewayIntents.GuildMessages |
                             GatewayIntents.MessageContent |
                             GatewayIntents.GuildMessageReactions
        });

        _commands = new InteractionService(_client.Rest);

        _services = new ServiceCollection()
            .AddSingleton(_client)
            .AddSingleton(_commands)
            .AddSingleton(_config)
            .AddSingleton<SuggestionHandler>()
            .AddSingleton<SuggestionExpirationService>()
            .AddSingleton<TimeoutMonitorService>()
            .BuildServiceProvider();

        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.InteractionCreated += HandleInteractionAsync;
        _client.ReactionAdded += HandleReactionAdded;


        await _client.LoginAsync(TokenType.Bot, _config.Token);
        await _client.StartAsync();
        
        _ = _services.GetRequiredService<TimeoutMonitorService>();

        // ✅ Register commands after bot is ready
        _client.Ready += RegisterCommandsAsync;


        // ✅ Initialize the Ticket Message Tracking Module
        new TicketMessageModule(_client);

        // ✅ Initialize the Ticket Message Syncing Service
        new TicketMessageSyncService(_client);

        await Task.Delay(-1); // ⬅️ This should always be at the very end.
    }

    private void LoadConfig()
    {
        var configText = File.ReadAllText("botconfig.json");
        _config = JsonSerializer.Deserialize<BotConfig>(configText);

        // Ensure GuildId is properly set
        if (string.IsNullOrWhiteSpace(_config.GuildId))
        {
            Console.WriteLine("❌ GuildId is missing or invalid in botconfig.json!");
        }

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

        // ✅ Initialize the Ticket Message Tracking Module
        new TicketMessageModule(_client);

        // ✅ Start the SuggestionExpirationService in the background
        var expirationService = _services.GetRequiredService<SuggestionExpirationService>();
        _ = Task.Run(() => expirationService.StartAsync(CancellationToken.None));
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
    private async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> messageCache, Cacheable<IMessageChannel, ulong> channelCache, SocketReaction reaction)
    {
        var handler = _services.GetRequiredService<SuggestionHandler>();
        await handler.HandleReactionAdded(messageCache, channelCache, reaction);
    }

}