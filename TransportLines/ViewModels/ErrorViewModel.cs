namespace TransportLines.ViewModels;

// كلاس يحمل بيانات صفحة الخطأ (رقم الطلب)
public class ErrorViewModel
{
    // رقم الطلب اللي صار بيه الخطأ
    public string? RequestId { get; set; }

    // يرجع true إذا رقم الطلب موجود حتى ينعرض بالصفحة
    public bool ShowRequestId
    {
        get { return !string.IsNullOrEmpty(RequestId); }
    }
}
