using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("areas")]
[Index("AreaName", Name = "uq_areas_name", IsUnique = true)]
public partial class Area
{
    [Key]
    [Column("AreaID")]
    public int AreaId { get; set; }

    [StringLength(60)]
    public string AreaName { get; set; } = null!;

    [StringLength(60)]
    public string? Governorate { get; set; }

    [Required]
    public bool? IsActive { get; set; }

    [InverseProperty("Area")]
    public virtual ICollection<Stop> Stops { get; set; } = new List<Stop>();
}
