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
using Serilog;
using Microsoft.Extensions.Configuration;



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
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        Log.Information("🟢 Bot is starting...");

        LoadConfig();


        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds
                   | GatewayIntents.GuildMessages
                   | GatewayIntents.MessageContent
                   | GatewayIntents.GuildMessageReactions
                   | GatewayIntents.GuildMembers
        });

        _commands = new InteractionService(_client.Rest);

        _services = new ServiceCollection()
            .AddSingleton(_client)
            .AddSingleton(_commands)
            .AddSingleton(_config)

            // Add these:
            .AddSingleton<TicketService>()
            .AddSingleton<CloseTicketListener>()
            .AddSingleton<UserCardService>()
            .AddSingleton<SuggestionHandler>()
            .AddSingleton<SuggestionExpirationService>()
            .AddSingleton<TimeoutMonitorService>()
            .AddScoped<TicketDbContext>(provider =>
                new TicketDbContext(Config.TicketsDb.ConnectionString, Config.TicketsDb.Provider))
            .BuildServiceProvider();


        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.InteractionCreated += HandleInteractionAsync;
        _client.ReactionAdded += HandleReactionAdded;


        await _client.LoginAsync(TokenType.Bot, _config.Token);
        await _client.StartAsync();

        _ = _services.GetRequiredService<TimeoutMonitorService>();

        _client.Ready += RegisterCommandsAsync;

        new TicketMessageListener(_client);

        new TicketMessageSyncService(_client);

        AppDomain.CurrentDomain.ProcessExit += (s, e) => Log.CloseAndFlush();
        await Task.Delay(-1);
    }

    private void LoadConfig()
    {
        var configText = File.ReadAllText("botconfig.json");
        _config = JsonSerializer.Deserialize<BotConfig>(configText);

        if (string.IsNullOrWhiteSpace(_config.GuildId))
        {
            Log.Information("❌ GuildId is missing or invalid in botconfig.json!");
        }

        Config = _config;
    }

    private async Task LogAsync(LogMessage log)
    {
        Log.Information(log.ToString());
    }

    private async Task ReadyAsync()
    {
        Log.Information($"✅ Logged in as {_client.CurrentUser.Username}");

        try
        {
            using var dbContext = new TicketDbContext(Config.TicketsDb.ConnectionString, Config.TicketsDb.Provider);
            dbContext.Database.EnsureCreated();
            Log.Information("✅ Successfully connected to PostgreSQL database.");
        }
        catch (Exception ex)
        {
            Log.Information($"❌ Database connection failed: {ex.Message}");
        }
        new TicketMessageListener(_client);
        var expirationService = _services.GetRequiredService<SuggestionExpirationService>();
        _ = Task.Run(() => expirationService.StartAsync(CancellationToken.None));
    }


    private async Task RegisterCommandsAsync()
    {
        await _commands.AddModulesAsync(typeof(Program).Assembly, _services);
        Log.Information("✅ Commands and interactions loaded.");
        foreach (var guild in _client.Guilds)
        {
            await _commands.RegisterCommandsToGuildAsync(guild.Id, true);
        }
        Log.Information("✅ Slash commands registered.");
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