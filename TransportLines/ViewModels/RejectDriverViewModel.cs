using System.ComponentModel.DataAnnotations;

namespace TransportLines.ViewModels;

// كلاس يطابق فورم رفض السائق: رقم السائق + سبب الرفض
public class RejectDriverViewModel
{
    // رقم السائق اللي نرفضه
    [Required]
    public int DriverId { get; set; }

    // سبب الرفض: إجباري حتى السائق يعرف شنو يصلح
    [Required(ErrorMessage = "اكتب سبب الرفض")]
    [StringLength(255, MinimumLength = 5, ErrorMessage = "السبب لازم يكون من 5 إلى 255 حرف")]
    [Display(Name = "سبب الرفض")]
    public string Reason { get; set; } = "";
}
