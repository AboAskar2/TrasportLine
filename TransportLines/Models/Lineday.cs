using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("linedays")]
[Index("LineId", "DayOfWeek", Name = "uq_linedays_line_day", IsUnique = true)]
public partial class Lineday
{
    [Key]
    [Column("LineDayID")]
    public int LineDayId { get; set; }

    [Column("LineID")]
    public int LineId { get; set; }

    public sbyte DayOfWeek { get; set; }

    [ForeignKey("LineId")]
    [InverseProperty("Linedays")]
    public virtual Line Line { get; set; } = null!;
}
