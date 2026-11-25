using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("linestops")]
[Index("StopId", Name = "fk_linestops_stop")]
[Index("LineId", "StopOrder", Name = "uq_linestops_line_order", IsUnique = true)]
[Index("LineId", "StopId", Name = "uq_linestops_line_stop", IsUnique = true)]
public partial class Linestop
{
    [Key]
    [Column("LineStopID")]
    public int LineStopId { get; set; }

    [Column("LineID")]
    public int LineId { get; set; }

    [Column("StopID")]
    public int StopId { get; set; }

    public int StopOrder { get; set; }

    [Column(TypeName = "time")]
    public TimeOnly? EstimatedArrivalTime { get; set; }

    [ForeignKey("LineId")]
    [InverseProperty("Linestops")]
    public virtual Line Line { get; set; } = null!;

    [InverseProperty("LinestopNavigation")]
    public virtual ICollection<Lineprice> LinepriceLinestopNavigations { get; set; } = new List<Lineprice>();

    [InverseProperty("Linestop")]
    public virtual ICollection<Lineprice> LinepriceLinestops { get; set; } = new List<Lineprice>();

    [ForeignKey("StopId")]
    [InverseProperty("Linestops")]
    public virtual Stop Stop { get; set; } = null!;

    [InverseProperty("LinestopNavigation")]
    public virtual ICollection<Subscription> SubscriptionLinestopNavigations { get; set; } = new List<Subscription>();

    [InverseProperty("Linestop")]
    public virtual ICollection<Subscription> SubscriptionLinestops { get; set; } = new List<Subscription>();
}
