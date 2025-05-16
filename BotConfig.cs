using System.Collections.Generic;

public class BotConfig
{
    public string? Token { get; set; }
    public string? SupportChannelId { get; set; }
    public string GuildId { get; set; }
    public Dictionary<string, string[]>? GameServers { get; set; }
    public string AdminChannelId { get; set; }
    public string AdminRole { get; set; }
    public List<string> Admins { get; set; }
    public string TranscriptLogChannel { get; set; }
    public TicketsDbConfig TicketsDb { get; set; }
    public Dictionary<string, string> SupportRole { get; set; }
    public Dictionary<string, string> SupportCategory { get; set; }
    public Dictionary<string, string> SuggestionsChannels { get; set; }
}

public class TicketsDbConfig
{
    public string ConnectionString { get; set; }
    public string Provider { get; set; }
}
