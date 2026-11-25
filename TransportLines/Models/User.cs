using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("users")]
[Index("RoleId", Name = "fk_users_role")]
[Index("Email", Name = "uq_users_email", IsUnique = true)]
[Index("PhoneNumber", Name = "uq_users_phone", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("UserID")]
    public int UserId { get; set; }

    [Column("RoleID")]
    public int RoleId { get; set; }

    [StringLength(100)]
    public string FullName { get; set; } = null!;

    [StringLength(20)]
    public string PhoneNumber { get; set; } = null!;

    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(255)]
    public string PasswordHash { get; set; } = null!;

    [StringLength(10)]
    public string? Gender { get; set; }

    [Column("ProfilePhotoURL")]
    [StringLength(255)]
    public string? ProfilePhotoUrl { get; set; }

    public bool IsPhoneVerified { get; set; }

    [StringLength(15)]
    public string AccountStatus { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? LastLoginAt { get; set; }

    public bool IsDeleted { get; set; }

    [InverseProperty("User")]
    public virtual Driver? DriverUser { get; set; }

    [InverseProperty("VerifiedByUser")]
    public virtual ICollection<Driver> DriverVerifiedByUsers { get; set; } = new List<Driver>();

    [InverseProperty("User")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [ForeignKey("RoleId")]
    [InverseProperty("Users")]
    public virtual Role Role { get; set; } = null!;

    [InverseProperty("Passenger")]
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
