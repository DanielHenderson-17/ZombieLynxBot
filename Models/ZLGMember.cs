using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class ZLGMember
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string? SteamId { get; set; }

    [MaxLength(100)]
    public string? SteamName { get; set; }

    [MaxLength(250)]
    public string? SteamImgUrl { get; set; }

    [MaxLength(100)]
    public string? DiscordId { get; set; }

    [MaxLength(100)]
    public string? DiscordName { get; set; }

    [MaxLength(250)]
    public string? DiscordImgUrl { get; set; }

    [MaxLength(100)]
    public string? EosId { get; set; }

    [MaxLength(100)]
    public string? EpicName { get; set; }

    [MaxLength(250)]
    public string? EpicImgUrl { get; set; }

    [MaxLength(36)]
    public string? MinecraftUuid { get; set; }

    [MaxLength(100)]
    public string? MinecraftUsername { get; set; }

    [MaxLength(250)]
    public string? MinecraftAvatarUrl { get; set; }

    [Required]
    public int UserProfileId { get; set; }

    [ForeignKey("UserProfileId")]
    public UserProfile UserProfile { get; set; }
}
