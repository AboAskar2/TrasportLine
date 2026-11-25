using System.ComponentModel.DataAnnotations;

namespace TransportLines.ViewModels;

// كلاس يطابق فورم تسجيل الدخول (رقم الهاتف + كلمة المرور)
public class LoginViewModel
{
    // رقم الهاتف المستخدم بالتسجيل
    [Required(ErrorMessage = "رقم الهاتف مطلوب")]
    [RegularExpression("^07[0-9]{9}$", ErrorMessage = "رقم الهاتف لازم يكون 11 رقم ويبدأ بـ 07")]
    [Display(Name = "رقم الهاتف")]
    public string PhoneNumber { get; set; } = "";

    // كلمة المرور
    [Required(ErrorMessage = "كلمة المرور مطلوبة")]
    [StringLength(64, ErrorMessage = "كلمة المرور طويلة جداً")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string Password { get; set; } = "";

    // تذكرني: يخلي الدخول محفوظ بعد غلق المتصفح
    [Display(Name = "تذكرني")]
    public bool RememberMe { get; set; }
}
