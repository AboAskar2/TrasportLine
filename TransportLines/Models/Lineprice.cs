using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("lineprices")]
[Index("LineId", "ToStopId", Name = "fk_lineprices_to")]
[Index("LineId", "FromStopId", "ToStopId", Name = "uq_lineprices_pair", IsUnique = true)]
public partial class Lineprice
{
    [Key]
    [Column("LinePriceID")]
    public int LinePriceId { get; set; }

    [Column("LineID")]
    public int LineId { get; set; }

    [Column("FromStopID")]
    public int FromStopId { get; set; }

    [Column("ToStopID")]
    public int ToStopId { get; set; }

    [Precision(10, 0)]
    public decimal MonthlyPrice { get; set; }

    [ForeignKey("LineId")]
    [InverseProperty("Lineprices")]
    public virtual Line Line { get; set; } = null!;

    [ForeignKey("LineId, FromStopId")]
    [InverseProperty("LinepriceLinestops")]
    public virtual Linestop Linestop { get; set; } = null!;

    [ForeignKey("LineId, ToStopId")]
    [InverseProperty("LinepriceLinestopNavigations")]
    public virtual Linestop LinestopNavigation { get; set; } = null!;
}
