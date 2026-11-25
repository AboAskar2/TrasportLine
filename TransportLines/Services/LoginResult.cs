namespace TransportLines.Services;

// نتيجة تسجيل الدخول: هل نجح، وبيانات المستخدم إذا نجح، ورسالة الخطأ إذا فشل
public class LoginResult
{
    // هل الدخول نجح
    public bool Success { get; set; }

    // رسالة الخطأ تنعرض للمستخدم
    public string ErrorMessage { get; set; } = "";

    // رقم المستخدم (يستخدم لاحقاً بالـ Cookie)
    public int UserId { get; set; }

    // الاسم الكامل (للعرض بالواجهة)
    public string FullName { get; set; } = "";

    // اسم الدور: Passenger أو Driver أو Admin
    public string RoleName { get; set; } = "";
}
