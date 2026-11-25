using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("stops")]
[Index("AreaId", "StopName", Name = "uq_stops_area_name", IsUnique = true)]
public partial class Stop
{
    [Key]
    [Column("StopID")]
    public int StopId { get; set; }

    [Column("AreaID")]
    public int AreaId { get; set; }

    [StringLength(80)]
    public string StopName { get; set; } = null!;

    [StringLength(150)]
    public string? Landmark { get; set; }

    [Precision(9, 6)]
    public decimal? Latitude { get; set; }

    [Precision(9, 6)]
    public decimal? Longitude { get; set; }

    [Required]
    public bool? IsActive { get; set; }

    [ForeignKey("AreaId")]
    [InverseProperty("Stops")]
    public virtual Area Area { get; set; } = null!;

    [InverseProperty("Stop")]
    public virtual ICollection<Linestop> Linestops { get; set; } = new List<Linestop>();
}
