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


    public string? ImgUrl { get; set; }

    public ulong? DiscordUserId { get; set; }
    public string? DiscordUserName { get; set; }
    public ulong? DiscordMessageId { get; set; }
    public bool SentToDiscord { get; set; } = false;

}
