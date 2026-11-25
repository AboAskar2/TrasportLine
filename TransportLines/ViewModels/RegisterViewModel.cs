using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TransportLines.ViewModels;

// كلاس يطابق فورم التسجيل: حقول مشتركة للكل + حقول إضافية للسائق فقط
// الفالديشن هنا هو الطبقة الأولى، والطبقة الثانية (اللي ما تنتجاوز) بـ AccountService
public class RegisterViewModel
{
    // ===== الحقول المشتركة (راكب وسائق) =====

    // نوع الحساب: بس Passenger أو Driver (Admin ممنوع)
    [Required(ErrorMessage = "اختر نوع الحساب")]
    [RegularExpression("^(Passenger|Driver)$", ErrorMessage = "نوع الحساب غير صحيح")]
    [Display(Name = "نوع الحساب")]
    public string AccountType { get; set; } = "Passenger";

    // الاسم الكامل: حروف ومسافات فقط
    [Required(ErrorMessage = "الاسم مطلوب")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "الاسم لازم يكون بين 3 و 100 حرف")]
    [RegularExpression(@"^[\p{L} ]+$", ErrorMessage = "الاسم لازم يكون حروف ومسافات فقط")]
    [Display(Name = "الاسم الكامل")]
    public string FullName { get; set; } = "";

    // رقم الهاتف العراقي: يبدأ بـ 07 وبعده 9 أرقام
    [Required(ErrorMessage = "رقم الهاتف مطلوب")]
    [RegularExpression("^07[0-9]{9}$", ErrorMessage = "رقم الهاتف لازم يكون 11 رقم ويبدأ بـ 07")]
    [Display(Name = "رقم الهاتف")]
    public string PhoneNumber { get; set; } = "";

    // الإيميل اختياري
    [EmailAddress(ErrorMessage = "صيغة الإيميل غير صحيحة")]
    [StringLength(100, ErrorMessage = "الإيميل طويل جداً")]
    [Display(Name = "البريد الإلكتروني (اختياري)")]
    public string? Email { get; set; }

    // كلمة المرور: 8 إلى 64 حرف إنكليزي/رقم/رمز
    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [StringLength(64, MinimumLength = 8, ErrorMessage = "كلمة المرور لازم تكون بين 8 و 64 حرف")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string Password { get; set; } = "";

    // تأكيد كلمة المرور لازم يطابق كلمة المرور
    [Required(ErrorMessage = "أعد كتابة كلمة المرور")]
    [Compare("Password", ErrorMessage = "كلمتا المرور غير متطابقتين")]
    [DataType(DataType.Password)]
    [Display(Name = "تأكيد كلمة المرور")]
    public string ConfirmPassword { get; set; } = "";

    // الجنس: Male أو Female
    [Required(ErrorMessage = "اختر الجنس")]
    [RegularExpression("^(Male|Female)$", ErrorMessage = "قيمة الجنس غير صحيحة")]
    [Display(Name = "الجنس")]
    public string Gender { get; set; } = "";

    // ===== حقول السائق (إجبارية إذا AccountType = Driver، والتحقق منها بـ AccountService) =====

    // رقم إجازة السوق
    [StringLength(30, ErrorMessage = "رقم الإجازة طويل جداً")]
    [Display(Name = "رقم إجازة السوق")]
    public string? LicenseNumber { get; set; }

    // تاريخ انتهاء الإجازة
    [DataType(DataType.Date)]
    [Display(Name = "تاريخ انتهاء الإجازة")]
    public DateTime? LicenseExpiryDate { get; set; }

    // رقم البطاقة الوطنية
    [StringLength(20, ErrorMessage = "رقم البطاقة طويل جداً")]
    [Display(Name = "رقم البطاقة الوطنية")]
    public string? NationalIdNumber { get; set; }

    // صورة الإجازة
    [Display(Name = "صورة الإجازة")]
    public IFormFile? LicensePhoto { get; set; }

    // صورة البطاقة الوطنية
    [Display(Name = "صورة البطاقة الوطنية")]
    public IFormFile? IdPhoto { get; set; }

    // ===== حقول السيارة (للسائق فقط) =====

    // نوع السيارة (رقم من جدول VehicleTypes)
    [Display(Name = "نوع السيارة")]
    public int? VehicleTypeId { get; set; }

    // رقم اللوحة
    [StringLength(20, ErrorMessage = "رقم اللوحة طويل جداً")]
    [Display(Name = "رقم اللوحة")]
    public string? PlateNumber { get; set; }

    // الشركة المصنعة (اختياري)
    [StringLength(40, ErrorMessage = "اسم الشركة طويل جداً")]
    [Display(Name = "الشركة المصنعة")]
    public string? Brand { get; set; }

    // موديل السيارة (اختياري). اسمه VehicleModel مو Model حتى ما يتعارض ويا كلمة model بالكونترولر والـ View
    [StringLength(40, ErrorMessage = "اسم الموديل طويل جداً")]
    [Display(Name = "الموديل")]
    public string? VehicleModel { get; set; }

    // اللون (اختياري)
    [StringLength(20, ErrorMessage = "اللون طويل جداً")]
    [Display(Name = "اللون")]
    public string? Color { get; set; }

    // سنة الصنع (اختياري)
    [Range(1980, 2100, ErrorMessage = "سنة الصنع غير صحيحة")]
    [Display(Name = "سنة الصنع")]
    public int? ManufactureYear { get; set; }

    // عدد المقاعد
    [Range(1, 100, ErrorMessage = "عدد المقاعد غير صحيح")]
    [Display(Name = "عدد المقاعد")]
    public int? SeatCapacity { get; set; }

    // تاريخ انتهاء الفحص
    [DataType(DataType.Date)]
    [Display(Name = "تاريخ انتهاء الفحص")]
    public DateTime? InspectionExpiryDate { get; set; }

    // صورة السنوية
    [Display(Name = "صورة السنوية")]
    public IFormFile? RegistrationPhoto { get; set; }

    // صورة السيارة (اختياري)
    [Display(Name = "صورة السيارة (اختياري)")]
    public IFormFile? VehiclePhoto { get; set; }

    // هل بيها تبريد
    [Display(Name = "السيارة بيها تبريد")]
    public bool HasAirConditioning { get; set; }
}

