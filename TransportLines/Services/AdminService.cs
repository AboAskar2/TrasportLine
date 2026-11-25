using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;

namespace TransportLines.Services;

// خدمة الأدمن: عرض السائقين اللي ينتظرون الموافقة، الموافقة أو الرفض، وجلب مسارات صورهم
public class AdminService
{
    // أقل وأكثر طول لسبب الرفض (الحد الأعلى نفس حجم العمود بالداتابيس)
    public const int MinReasonLength = 5;
    public const int MaxReasonLength = 255;

    private readonly AppDbContext _db;

    // يستلم الداتابيس من الـ DI
    public AdminService(AppDbContext db)
    {
        _db = db;
    }

    // ================= عرض السائقين =================

    // يرجع السائقين اللي حالتهم Pending ويا بيانات المستخدم والسيارة، الأقدم أولاً
    public async Task<List<Driver>> GetPendingDriversAsync()
    {
        return await _db.Drivers
            .Include(d => d.User)
            .Include(d => d.Vehicles).ThenInclude(v => v.VehicleType)
            .Where(d => d.VerificationStatus == "Pending" && !d.User.IsDeleted)
            .OrderBy(d => d.User.CreatedAt)
            .ToListAsync();
    }

    // يرجع تفاصيل سائق واحد (أو null إذا مو موجود)
    public async Task<Driver?> GetDriverDetailsAsync(int driverId)
    {
        return await _db.Drivers
            .Include(d => d.User)
            .Include(d => d.Vehicles).ThenInclude(v => v.VehicleType)
            .FirstOrDefaultAsync(d => d.DriverId == driverId);
    }

    // ================= الموافقة والرفض =================

    // يوافق على سائق: لازم يكون Pending وإجازته سارية، ويرسل له إشعار
    public async Task<ServiceResult> ApproveDriverAsync(int driverId, int adminUserId)
    {
        ServiceResult result = new ServiceResult();
        Driver? driver = await FindPendingDriverAsync(driverId, adminUserId, result);
        if (driver == null)
            return result;

        // الإجازة ممكن تنتهي بين التسجيل والموافقة، فنفحصها مرة ثانية
        if (driver.LicenseExpiryDate <= DateOnly.FromDateTime(DateTime.Today))
        {
            result.AddError("", "إجازة السائق منتهية، ما تكدر توافق عليه");
            return result;
        }

        SetVerification(driver, "Approved", adminUserId, null);
        AddNotification(driver.UserId, "DriverApproved", "تمت الموافقة على حسابك",
            "تمت الموافقة على حساب السائق، تكدر هسه تنشر خطوطك.");
        await _db.SaveChangesAsync();
        return result;
    }

    // يرفض سائق مع سبب إجباري، ويرسل له إشعار بالسبب
    public async Task<ServiceResult> RejectDriverAsync(int driverId, int adminUserId, string? reason)
    {
        ServiceResult result = new ServiceResult();
        string cleanReason = (reason ?? "").Trim();
        if (cleanReason.Length < MinReasonLength || cleanReason.Length > MaxReasonLength)
        {
            result.AddError("Reason", "اكتب سبب الرفض (من 5 إلى 255 حرف)");
            return result;
        }

        Driver? driver = await FindPendingDriverAsync(driverId, adminUserId, result);
        if (driver == null)
            return result;

        SetVerification(driver, "Rejected", adminUserId, cleanReason);
        AddNotification(driver.UserId, "DriverRejected", "تم رفض حساب السائق", BuildRejectMessage(cleanReason));
        await _db.SaveChangesAsync();
        return result;
    }

    // يتأكد إن المستخدم أدمن فعلاً، وإن السائق موجود وحالته Pending. يرجع null ويضيف الخطأ إذا لا
    private async Task<Driver?> FindPendingDriverAsync(int driverId, int adminUserId, ServiceResult result)
    {
        if (!await CheckAdminAsync(adminUserId, result))
            return null;

        Driver? driver = await _db.Drivers.FirstOrDefaultAsync(d => d.DriverId == driverId);
        if (driver == null)
        {
            result.AddError("", "السائق غير موجود");
            return null;
        }

        if (driver.VerificationStatus != "Pending")
        {
            result.AddError("", "هذا السائق تمت مراجعته من قبل");
            return null;
        }

        return driver;
    }

    // فحص إضافي بالخدمة نفسها (حتى لو الكونترولر عليه [Authorize(Roles = "Admin")]): يضيف خطأ إذا المستخدم مو أدمن
    private async Task<bool> CheckAdminAsync(int adminUserId, ServiceResult result)
    {
        if (await IsAdminAsync(adminUserId))
            return true;

        result.AddError("", "ما عندك صلاحية لهذي العملية");
        return false;
    }

    // يرجع true إذا المستخدم موجود ودوره Admin وحسابه فعّال
    private async Task<bool> IsAdminAsync(int userId)
    {
        return await _db.Users.AnyAsync(u => u.UserId == userId
            && u.Role.RoleName == "Admin"
            && u.AccountStatus == "Active"
            && !u.IsDeleted);
    }

    // يغير حالة التحقق ويسجل منو راجع ومتى
    private static void SetVerification(Driver driver, string status, int adminUserId, string? reason)
    {
        driver.VerificationStatus = status;
        driver.VerifiedByUserId = adminUserId;
        driver.VerifiedAt = DateTime.Now;
        driver.RejectionReason = reason;
    }

    // يبني رسالة الرفض ويقصها إذا طالت أكثر من حجم عمود Message (255)
    private static string BuildRejectMessage(string reason)
    {
        string message = "تم رفض حسابك. السبب: " + reason;
        if (message.Length > 255)
            message = message.Substring(0, 255);

        return message;
    }

    // يضيف إشعار جديد للمستخدم (ينحفظ ويا الـ SaveChanges الجاي)
    private void AddNotification(int userId, string type, string title, string message)
    {
        Notification notification = new Notification();
        notification.UserId = userId;
        notification.Type = type;
        notification.Title = title;
        notification.Message = message;
        notification.IsRead = false;
        notification.CreatedAt = DateTime.Now;
        _db.Notifications.Add(notification);
    }

    // ================= صور السائق =================

    // يرجع مسار صورة السائق حسب نوعها: license أو id أو registration أو vehicle
    // أي نوع ثاني يرجع null (قائمة مسموحة، ما نستخدم النص اللي جاي من الرابط كمسار)
    public async Task<string?> GetDriverPhotoPathAsync(int driverId, string? photoType)
    {
        Driver? driver = await GetDriverDetailsAsync(driverId);
        if (driver == null)
            return null;

        Vehicle? vehicle = driver.Vehicles.FirstOrDefault();
        if (photoType == "license")
            return driver.LicensePhotoUrl;
        if (photoType == "id")
            return driver.IdphotoUrl;
        if (photoType == "registration")
            return vehicle?.RegistrationPhotoUrl;
        if (photoType == "vehicle")
            return vehicle?.VehiclePhotoUrl;

        return null;
    }
}
