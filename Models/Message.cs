using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Message
{
    [Key]
    public int Id { get; set; }

    [ForeignKey("Ticket")]

    public int MessageGroupId { get; set; }
    public Ticket Ticket { get; set; }

    [ForeignKey("UserProfile")]
    public int? UserProfileId { get; set; }
    public UserProfile UserProfile { get; set; }

    [Required]
    public string Content { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "jsonb")]
    public string ImgUrlsJson { get; set; } = "[]"; // Stored as JSON string

    [NotMapped] // Exclude from DB, only used in code
    public List<string> ImgUrls
    {
        get => string.IsNullOrEmpty(ImgUrlsJson) ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(ImgUrlsJson);
        set => ImgUrlsJson = System.Text.Json.JsonSerializer.Serialize(value);
    }

    public ulong? DiscordUserId { get; set; }
    public string? DiscordUserName { get; set; }
    public string? DiscordImgUrl { get; set; }
    public ulong? DiscordMessageId { get; set; }
    public bool SentToDiscord { get; set; } = false;

}
