using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("drivers")]
[Index("VerifiedByUserId", Name = "fk_drivers_verifier")]
[Index("LicenseNumber", Name = "uq_drivers_license", IsUnique = true)]
[Index("NationalIdnumber", Name = "uq_drivers_nid", IsUnique = true)]
[Index("UserId", Name = "uq_drivers_user", IsUnique = true)]
public partial class Driver
{
    [Key]
    [Column("DriverID")]
    public int DriverId { get; set; }

    [Column("UserID")]
    public int UserId { get; set; }

    [StringLength(30)]
    public string LicenseNumber { get; set; } = null!;

    public DateOnly LicenseExpiryDate { get; set; }

    [Column("NationalIDNumber")]
    [StringLength(20)]
    public string NationalIdnumber { get; set; } = null!;

    [Column("LicensePhotoURL")]
    [StringLength(255)]
    public string LicensePhotoUrl { get; set; } = null!;

    [Column("IDPhotoURL")]
    [StringLength(255)]
    public string IdphotoUrl { get; set; } = null!;

    [StringLength(15)]
    public string VerificationStatus { get; set; } = null!;

    [Column("VerifiedByUserID")]
    public int? VerifiedByUserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? VerifiedAt { get; set; }

    [StringLength(255)]
    public string? RejectionReason { get; set; }

    [InverseProperty("Driver")]
    public virtual ICollection<Line> Lines { get; set; } = new List<Line>();

    [ForeignKey("UserId")]
    [InverseProperty("DriverUser")]
    public virtual User User { get; set; } = null!;

    [InverseProperty("Driver")]
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();

    [ForeignKey("VerifiedByUserId")]
    [InverseProperty("DriverVerifiedByUsers")]
    public virtual User? VerifiedByUser { get; set; }
}
