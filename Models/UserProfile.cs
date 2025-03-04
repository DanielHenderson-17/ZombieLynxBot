using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class UserProfile
{
    [Key]
    public int Id { get; set; }  // Primary Key

    [Required, MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public Guid UserId { get; set; }  // Foreign Key to User (not needed in the bot)

    // Navigation property for ZLGMember
    public ZLGMember? ZLGMember { get; set; }
}
