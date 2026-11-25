using Microsoft.AspNetCore.Http;

namespace TransportLines.Services;

// خدمة رفع الصور: تتأكد إن الملف صورة حقيقية (JPG أو PNG) وحجمه مقبول،
// وتحفظه باسم عشوائي بمجلد خارج wwwroot حتى محد يكدر يفتحه أو يشغّله مباشرة
public class FileUploadService
{
    // أقصى حجم للصورة: 2 ميگابايت
    public const long MaxFileSize = 2 * 1024 * 1024;

    // المجلد الرئيسي اللي تنحفظ بيه الصور
    private readonly string _uploadRoot;

    // يستلم مسار مجلد الرفع (يتحدد بـ Program.cs)
    public FileUploadService(string uploadRoot)
    {
        _uploadRoot = uploadRoot;
    }

    // يفحص الصورة ويرجع رسالة خطأ، أو null إذا الصورة سليمة
    public string? ValidateImage(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return "الصورة مطلوبة";
        }

        if (file.Length > MaxFileSize)
        {
            return "حجم الصورة لازم يكون أقل من 2 ميگابايت";
        }

        // الامتداد لازم يكون jpg أو jpeg أو png
        string extension = Path.GetExtension(file.FileName).ToLower();
        if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
        {
            return "مسموح بس صور JPG أو PNG";
        }

        // نفحص أول بايتات من الملف حتى نتأكد إنه صورة فعلاً مو ملف ثاني اسمه .jpg
        if (!HasImageSignature(file))
        {
            return "الملف مو صورة حقيقية";
        }

        return null;
    }

    // يحفظ الصورة باسم عشوائي داخل مجلد فرعي ويرجع المسار النسبي للحفظ بالداتابيس
    public async Task<string> SaveImageAsync(IFormFile file, string subFolder)
    {
        // إنشاء المجلد إذا مو موجود
        string folderPath = Path.Combine(_uploadRoot, subFolder);
        Directory.CreateDirectory(folderPath);

        // اسم عشوائي (Guid) + الامتداد، حتى ما نستخدم اسم الملف اللي جاي من المستخدم
        string extension = Path.GetExtension(file.FileName).ToLower();
        string newFileName = Guid.NewGuid().ToString("N") + extension;
        string fullPath = Path.Combine(folderPath, newFileName);

        // كتابة الملف على القرص
        using (FileStream stream = new FileStream(fullPath, FileMode.CreateNew))
        {
            await file.CopyToAsync(stream);
        }

        return subFolder + "/" + newFileName;
    }

    // يحذف صورة محفوظة (نستخدمها إذا فشل الحفظ بالداتابيس حتى ما تبقى ملفات زايدة)
    public void DeleteImage(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return;
        }

        string fullPath = Path.Combine(_uploadRoot, relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    // يرجع المسار الكامل لصورة محفوظة، أو null إذا الملف مو موجود أو المسار يحاول يطلع برا مجلد الرفع
    // (حماية من Path Traversal مثل "../../appsettings.json")
    public string? GetSafeFullPath(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return null;

        // نحول المسارين لمسارات كاملة حتى نقارنهم صح
        string rootFullPath = Path.GetFullPath(_uploadRoot) + Path.DirectorySeparatorChar;
        string fileFullPath = Path.GetFullPath(Path.Combine(_uploadRoot, relativePath));

        // الملف لازم يكون داخل مجلد الرفع وموجود فعلاً
        if (!fileFullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
            return null;
        if (!File.Exists(fileFullPath))
            return null;

        return fileFullPath;
    }

    // يرجع نوع المحتوى للمتصفح حسب امتداد الصورة
    public static string GetContentType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        if (extension == ".png")
            return "image/png";

        return "image/jpeg";
    }

    // يقرا أول 4 بايتات ويقارنها بتوقيع JPG (FF D8 FF) أو PNG (89 50 4E 47)
    private static bool HasImageSignature(IFormFile file)
    {
        byte[] header = new byte[4];
        using (Stream stream = file.OpenReadStream())
        {
            int bytesRead = stream.Read(header, 0, 4);
            if (bytesRead < 4)
            {
                return false;
            }
        }

        bool isJpg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        return isJpg || isPng;
    }
}
