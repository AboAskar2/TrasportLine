using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;

namespace TransportLines.Services;

// خدمة السائق: ملفه الشخصي (بياناته وسيارته وحالة الموافقة)
// كل الدوال تاخذ رقم المستخدم الداخل، فالسائق يشوف بياناته هو بس
public class DriverService
{
    private readonly AppDbContext _db;

    // يستلم الداتابيس من الـ DI
    public DriverService(AppDbContext db)
    {
        _db = db;
    }

    // يرجع ملف السائق المرتبط بهذا المستخدم ويا سيارته (أو null إذا المستخدم مو سائق)
    public async Task<Driver?> GetDriverProfileAsync(int userId)
    {
        return await _db.Drivers
            .Include(d => d.User)
            .Include(d => d.Vehicles).ThenInclude(v => v.VehicleType)
            .FirstOrDefaultAsync(d => d.UserId == userId);
    }

    // يرجع true إذا السائق تمت الموافقة عليه (نحتاجها لاحقاً: ما ينشر خط إلا إذا Approved)
    public async Task<bool> IsApprovedDriverAsync(int userId)
    {
        return await _db.Drivers.AnyAsync(d => d.UserId == userId && d.VerificationStatus == "Approved");
    }

    // يرجع رسالة توضح حالة الحساب للسائق بالعربي
    public static string GetStatusMessage(Driver driver)
    {
        if (driver.VerificationStatus == "Approved")
            return "حسابك موافق عليه، تكدر تنشر خطوطك.";

        if (driver.VerificationStatus == "Rejected")
            return "تم رفض حسابك. السبب: " + driver.RejectionReason;

        return "حسابك ينتظر موافقة الإدارة.";
    }
}
