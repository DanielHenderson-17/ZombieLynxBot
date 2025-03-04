using System;
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
    private string _token;

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

        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();

        // Register commands after bot is ready
        _client.Ready += RegisterCommandsAsync;

        await Task.Delay(-1);
    }

    private void LoadConfig()
    {
        var configText = File.ReadAllText("botconfig.json");
        var configJson = JsonSerializer.Deserialize<BotConfig>(configText);
        _token = configJson.Token;
    }

    private async Task LogAsync(LogMessage log)
    {
        Console.WriteLine(log);
    }

    private async Task ReadyAsync()
    {
        Console.WriteLine($"✅ Logged in as {_client.CurrentUser.Username}");
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

    private class BotConfig
    {
        public string Token { get; set; }
    }
}
