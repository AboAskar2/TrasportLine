using System.ComponentModel.DataAnnotations;

namespace TransportLines.ViewModels;

// كلاس يطابق فورم رفض طلب اشتراك من السائق: رقم الطلب + سبب الرفض
public class RejectRequestViewModel
{
    // رقم طلب الاشتراك
    [Required]
    public int SubscriptionId { get; set; }

    // رقم الخط (حتى نرجع لصفحة طلبات نفس الخط بعد الرفض)
    public int LineId { get; set; }

    // سبب الرفض: إجباري حتى الراكب يعرف
    [Required(ErrorMessage = "اكتب سبب الرفض")]
    [StringLength(255, MinimumLength = 5, ErrorMessage = "السبب لازم يكون من 5 إلى 255 حرف")]
    [Display(Name = "سبب الرفض")]
    public string Reason { get; set; } = "";
}
