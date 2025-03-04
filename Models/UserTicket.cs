using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class UserTicket
{
    [Key, Column(Order = 0)]
    [ForeignKey("UserProfile")]
    public int UserProfileId { get; set; }

    [Key, Column(Order = 1)]
    [ForeignKey("Ticket")]
    public int TicketId { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public UserProfile UserProfile { get; set; }
    public Ticket Ticket { get; set; }
}
