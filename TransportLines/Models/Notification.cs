using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("notifications")]
[Index("RelatedLineId", Name = "fk_notifications_line")]
[Index("UserId", Name = "fk_notifications_user")]
public partial class Notification
{
    [Key]
    [Column("NotificationID")]
    public int NotificationId { get; set; }

    [Column("UserID")]
    public int UserId { get; set; }

    [Column("RelatedLineID")]
    public int? RelatedLineId { get; set; }

    [StringLength(30)]
    public string Type { get; set; } = null!;

    [StringLength(100)]
    public string Title { get; set; } = null!;

    [StringLength(255)]
    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("RelatedLineId")]
    [InverseProperty("Notifications")]
    public virtual Line? RelatedLine { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Notifications")]
    public virtual User User { get; set; } = null!;
}
