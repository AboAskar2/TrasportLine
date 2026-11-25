using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("subscriptions")]
[Index("LineId", "DropoffStopId", Name = "fk_subscriptions_dropoff")]
[Index("PassengerId", Name = "fk_subscriptions_passenger")]
[Index("LineId", "PickupStopId", Name = "fk_subscriptions_pickup")]
[Index("LineId", "PassengerId", "OpenKey", Name = "uq_subscriptions_one_open", IsUnique = true)]
public partial class Subscription
{
    [Key]
    [Column("SubscriptionID")]
    public int SubscriptionId { get; set; }

    [Column("LineID")]
    public int LineId { get; set; }

    [Column("PassengerID")]
    public int PassengerId { get; set; }

    [Column("PickupStopID")]
    public int PickupStopId { get; set; }

    [Column("DropoffStopID")]
    public int DropoffStopId { get; set; }

    public int SeatsCount { get; set; }

    [Precision(10, 0)]
    public decimal MonthlyPrice { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    [StringLength(15)]
    public string SubscriptionStatus { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime? ApprovedAt { get; set; }

    [StringLength(255)]
    public string? RejectionReason { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CancelledAt { get; set; }

    [StringLength(255)]
    public string? CancelReason { get; set; }

    public sbyte? OpenKey { get; set; }

    [ForeignKey("LineId")]
    [InverseProperty("Subscriptions")]
    public virtual Line Line { get; set; } = null!;

    [ForeignKey("LineId, DropoffStopId")]
    [InverseProperty("SubscriptionLinestops")]
    public virtual Linestop Linestop { get; set; } = null!;

    [ForeignKey("LineId, PickupStopId")]
    [InverseProperty("SubscriptionLinestopNavigations")]
    public virtual Linestop LinestopNavigation { get; set; } = null!;

    [ForeignKey("PassengerId")]
    [InverseProperty("Subscriptions")]
    public virtual User Passenger { get; set; } = null!;

    [InverseProperty("Subscription")]
    public virtual Rating? Rating { get; set; }
}
