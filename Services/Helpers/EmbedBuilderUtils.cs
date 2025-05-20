using Discord;

public static class EmbedBuilderUtils
{
    public static Embed BuildTimeoutEmbed(IGuildUser user)
    {
        return new EmbedBuilder()
            .WithAuthor(user)
            .WithThumbnailUrl("https://i.imgur.com/dnlokbX.png")
            .WithColor(Color.Green)
            .AddField("Disciplinary Action", "Timeout", true)
            .AddField("Duration", "12 hours", true)
            .AddField("Reason", "Mentioned admin", true)
            .WithCurrentTimestamp()
            .Build();
    }
}
