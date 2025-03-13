// BotConfig.cs
using System.Collections.Generic;

public class BotConfig
{
    public string? Token { get; set; }
    public string? SupportChannelId { get; set; }
    public Dictionary<string, string[]>? GameServers { get; set; }
    public string AdminRole { get; set; }

}