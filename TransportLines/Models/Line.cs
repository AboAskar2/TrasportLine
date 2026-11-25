using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("lines")]
[Index("DriverId", Name = "fk_lines_driver")]
[Index("VehicleId", Name = "fk_lines_vehicle")]
public partial class Line
{
    [Key]
    [Column("LineID")]
    public int LineId { get; set; }

    [Column("DriverID")]
    public int DriverId { get; set; }

    [Column("VehicleID")]
    public int VehicleId { get; set; }

    [StringLength(100)]
    public string? LineName { get; set; }

    [Column(TypeName = "time")]
    public TimeOnly DepartureTime { get; set; }

    public int TotalSeats { get; set; }

    public int MinSubscriptionMonths { get; set; }

    [StringLength(15)]
    public string LineStatus { get; set; } = null!;

    [StringLength(255)]
    public string? Notes { get; set; }

    public bool IsWomenOnly { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("DriverId")]
    [InverseProperty("Lines")]
    public virtual Driver Driver { get; set; } = null!;

    [InverseProperty("Line")]
    public virtual ICollection<Lineday> Linedays { get; set; } = new List<Lineday>();

    [InverseProperty("Line")]
    public virtual ICollection<Lineprice> Lineprices { get; set; } = new List<Lineprice>();

    [InverseProperty("Line")]
    public virtual ICollection<Linestop> Linestops { get; set; } = new List<Linestop>();

    [InverseProperty("RelatedLine")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [InverseProperty("Line")]
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    [ForeignKey("VehicleId")]
    [InverseProperty("Lines")]
    public virtual Vehicle Vehicle { get; set; } = null!;
}
