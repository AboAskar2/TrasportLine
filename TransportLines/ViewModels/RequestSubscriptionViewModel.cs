using System.ComponentModel.DataAnnotations;

namespace TransportLines.ViewModels;

// كلاس يطابق فورم طلب الاشتراك: الخط، محطة الصعود والنزول، عدد المقاعد، تاريخ البداية، وعدد الأشهر
// الفالديشن الكامل (المحطات على الخط، السعر، المدة) يصير بـ SubscriptionService
public class RequestSubscriptionViewModel
{
    // رقم الخط
    [Required]
    public int LineId { get; set; }

    // محطة الصعود
    [Required(ErrorMessage = "اختر محطة الصعود")]
    [Display(Name = "محطة الصعود")]
    public int? PickupStopId { get; set; }

    // محطة النزول
    [Required(ErrorMessage = "اختر محطة النزول")]
    [Display(Name = "محطة النزول")]
    public int? DropoffStopId { get; set; }

    // عدد المقاعد المطلوبة
    [Required(ErrorMessage = "عدد المقاعد مطلوب")]
    [Range(1, 10, ErrorMessage = "عدد المقاعد لازم يكون من 1 إلى 10")]
    [Display(Name = "عدد المقاعد")]
    public int? SeatsCount { get; set; } = 1;

    // تاريخ بداية الاشتراك
    [Required(ErrorMessage = "تاريخ البداية مطلوب")]
    [DataType(DataType.Date)]
    [Display(Name = "تاريخ البداية")]
    public DateTime? StartDate { get; set; }

    // عدد الأشهر
    [Required(ErrorMessage = "عدد الأشهر مطلوب")]
    [Range(1, 12, ErrorMessage = "عدد الأشهر لازم يكون من 1 إلى 12")]
    [Display(Name = "عدد الأشهر")]
    public int? Months { get; set; } = 1;
}
