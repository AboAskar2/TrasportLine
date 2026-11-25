using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("vehicles")]
[Index("DriverId", Name = "fk_vehicles_driver")]
[Index("VehicleTypeId", Name = "fk_vehicles_type")]
[Index("PlateNumber", Name = "uq_vehicles_plate", IsUnique = true)]
public partial class Vehicle
{
    [Key]
    [Column("VehicleID")]
    public int VehicleId { get; set; }

    [Column("DriverID")]
    public int DriverId { get; set; }

    [Column("VehicleTypeID")]
    public int VehicleTypeId { get; set; }

    [StringLength(20)]
    public string PlateNumber { get; set; } = null!;

    [StringLength(40)]
    public string? Brand { get; set; }

    [StringLength(40)]
    public string? Model { get; set; }

    [StringLength(20)]
    public string? Color { get; set; }

    public int? ManufactureYear { get; set; }

    public int SeatCapacity { get; set; }

    public DateOnly InspectionExpiryDate { get; set; }

    [Column("RegistrationPhotoURL")]
    [StringLength(255)]
    public string RegistrationPhotoUrl { get; set; } = null!;

    [Column("VehiclePhotoURL")]
    [StringLength(255)]
    public string? VehiclePhotoUrl { get; set; }

    public bool HasAirConditioning { get; set; }

    [Required]
    public bool? IsActive { get; set; }

    [ForeignKey("DriverId")]
    [InverseProperty("Vehicles")]
    public virtual Driver Driver { get; set; } = null!;

    [InverseProperty("Vehicle")]
    public virtual ICollection<Line> Lines { get; set; } = new List<Line>();

    [ForeignKey("VehicleTypeId")]
    [InverseProperty("Vehicles")]
    public virtual Vehicletype VehicleType { get; set; } = null!;
}
