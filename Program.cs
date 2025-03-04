using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

class Program
{
    private DiscordSocketClient _client;
    private string _token;

    static async Task Main(string[] args) => await new Program().RunBotAsync();

    public async Task RunBotAsync()
    {
        // Load bot token from config file
        LoadConfig();

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent
        });

        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.MessageReceived += MessageReceivedAsync;

        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();

        await Task.Delay(-1); // Keep the bot running
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

    private async Task MessageReceivedAsync(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        if (message.Content.ToLower() == "!ping")
        {
            await message.Channel.SendMessageAsync("Pong! 🏓");
        }
    }

    private class BotConfig
    {
        public string Token { get; set; }
    }
}
