namespace TransportLines.Services;

// كلاس بسيط ترجعه الخدمات: يحمل قائمة الأخطاء (اسم الحقل + الرسالة)
// إذا القائمة فارغة معناها العملية نجحت
public class ServiceResult
{
    // الأخطاء: المفتاح اسم الحقل بالفورم، والقيمة رسالة الخطأ
    public Dictionary<string, string> Errors { get; } = new Dictionary<string, string>();

    // يرجع true إذا ماكو ولا خطأ
    public bool Success
    {
        get { return Errors.Count == 0; }
    }

    // يضيف خطأ لحقل معين (إذا الحقل عنده خطأ من قبل ما نكتب فوقه)
    public void AddError(string fieldName, string message)
    {
        if (!Errors.ContainsKey(fieldName))
        {
            Errors.Add(fieldName, message);
        }
    }
}
