using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Ticket
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Subject { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Game { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Server { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Open";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ulong? DiscordChannelId { get; set; }
    public ulong? DiscordUserId { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public int? UserProfileId { get; set; }

    [ForeignKey("UserProfileId")]
    public UserProfile UserProfile { get; set; }
}
