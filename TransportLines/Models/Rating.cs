using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("ratings")]
[Index("SubscriptionId", Name = "uq_ratings_subscription", IsUnique = true)]
public partial class Rating
{
    [Key]
    [Column("RatingID")]
    public int RatingId { get; set; }

    [Column("SubscriptionID")]
    public int SubscriptionId { get; set; }

    public sbyte RatingValue { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("SubscriptionId")]
    [InverseProperty("Rating")]
    public virtual Subscription Subscription { get; set; } = null!;
}
