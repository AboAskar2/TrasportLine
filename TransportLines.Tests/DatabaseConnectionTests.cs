using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;

namespace TransportLines.Tests;

// اختبارات تتأكد إن البرنامج يكدر يتصل بالداتابيس line_subscription
// هذي الاختبارات تقرا بس (SELECT)، وتشتغل بس إذا RUN_REAL_DB_TESTS = 1 (مثل باقي اختبارات MySQL الحقيقية)
public class DatabaseConnectionTests
{
    // يتأكد إن الاتصال بالداتابيس ينجح بالمستخدم transport_app
    [RealDbFact]
    public async Task CanConnectToDatabase()
    {
        using AppDbContext db = RealDbHelper.CreateContext();

        // محاولة فتح اتصال بالداتابيس
        bool canConnect = await db.Database.CanConnectAsync();

        Assert.True(canConnect);
    }

    // يتأكد إن جدول Roles ينقرا وبيه أدوار
    [RealDbFact]
    public async Task CanReadRolesTable()
    {
        using AppDbContext db = RealDbHelper.CreateContext();

        // قراءة كل الأدوار من الجدول
        List<Role> roles = await db.Roles.ToListAsync();

        Assert.NotEmpty(roles);
    }
}
