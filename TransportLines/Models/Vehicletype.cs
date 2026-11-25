using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("vehicletypes")]
[Index("TypeName", Name = "uq_vehicletypes_name", IsUnique = true)]
public partial class Vehicletype
{
    [Key]
    [Column("VehicleTypeID")]
    public int VehicleTypeId { get; set; }

    [StringLength(50)]
    public string TypeName { get; set; } = null!;

    public int MaxSeats { get; set; }

    [Required]
    public bool? IsActive { get; set; }

    [InverseProperty("VehicleType")]
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
