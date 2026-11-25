using System.ComponentModel.DataAnnotations;

namespace TransportLines.ViewModels;

// كلاس يطابق فورم إنشاء خط جديد: بيانات الخط + الأيام + المحطات بالترتيب + الأسعار
// الفالديشن الكامل (المحطات، الأسعار، المقاعد) يصير بـ LineService
public class CreateLineViewModel
{
    // اسم الخط (اختياري)، مثال: "الكرادة - الجامعة التكنولوجية"
    [StringLength(100, ErrorMessage = "اسم الخط طويل جداً")]
    [Display(Name = "اسم الخط")]
    public string? LineName { get; set; }

    // وقت الانطلاق من أول محطة
    [Required(ErrorMessage = "وقت الانطلاق مطلوب")]
    [DataType(DataType.Time)]
    [Display(Name = "وقت الانطلاق")]
    public TimeSpan? DepartureTime { get; set; }

    // عدد المقاعد المعروضة للاشتراك (ما يتجاوز مقاعد السيارة)
    [Required(ErrorMessage = "عدد المقاعد مطلوب")]
    [Range(1, 100, ErrorMessage = "عدد المقاعد غير صحيح")]
    [Display(Name = "عدد المقاعد")]
    public int? TotalSeats { get; set; }

    // أقل مدة اشتراك بالأشهر
    [Required(ErrorMessage = "أقل مدة اشتراك مطلوبة")]
    [Range(1, 12, ErrorMessage = "أقل مدة اشتراك لازم تكون من 1 إلى 12 شهر")]
    [Display(Name = "أقل مدة اشتراك (أشهر)")]
    public int? MinSubscriptionMonths { get; set; } = 1;

    // ملاحظات للركاب (اختياري)
    [StringLength(255, ErrorMessage = "الملاحظات طويلة جداً (أكثر شي 255 حرف)")]
    [Display(Name = "ملاحظات")]
    public string? Notes { get; set; }

    // هل الخط للنساء فقط
    [Display(Name = "الخط للنساء فقط")]
    public bool IsWomenOnly { get; set; }

    // أيام الدوام: أرقام من 0 (الأحد) إلى 6 (السبت)
    [Display(Name = "أيام الخط")]
    public List<int> SelectedDays { get; set; } = new List<int>();

    // المحطات بالترتيب: أول عنصر = أول محطة
    public List<LineStopInput> Stops { get; set; } = new List<LineStopInput>();

    // الأسعار الشهرية لكل زوج محطات (من - إلى)
    public List<LinePriceInput> Prices { get; set; } = new List<LinePriceInput>();
}

// صف واحد من جدول المحطات بالفورم
public class LineStopInput
{
    // رقم المحطة من جدول Stops
    [Display(Name = "المحطة")]
    public int? StopId { get; set; }

    // وقت الوصول التقريبي للمحطة (اختياري)
    [DataType(DataType.Time)]
    [Display(Name = "وقت الوصول التقريبي")]
    public TimeSpan? EstimatedArrivalTime { get; set; }
}

// صف واحد من جدول الأسعار بالفورم
public class LinePriceInput
{
    // محطة الصعود
    [Display(Name = "من محطة")]
    public int? FromStopId { get; set; }

    // محطة النزول
    [Display(Name = "إلى محطة")]
    public int? ToStopId { get; set; }

    // السعر الشهري بالدينار
    [Range(1, 10000000, ErrorMessage = "السعر لازم يكون أكبر من صفر")]
    [Display(Name = "السعر الشهري (دينار)")]
    public decimal? MonthlyPrice { get; set; }
}
