using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TransportLines.Models;

[Table("test_results")]
public partial class TestResult
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("test_name")]
    [StringLength(100)]
    public string? TestName { get; set; }

    [Column("result")]
    [StringLength(10)]
    public string? Result { get; set; }
}
