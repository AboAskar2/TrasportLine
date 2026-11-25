using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TransportLines.Data;
using TransportLines.Models;

namespace TransportLines.Tests;

// أدوات اختبارات الداتابيس الحقيقية (قراءة فقط):
// 1) إنشاء اتصال بـ MySQL من user-secrets
// 2) صورة (Snapshot) للداتابيس قبل وبعد كل اختبار، حتى نتأكد إن ولا شي انكتب
// 3) إيجاد البيانات التجريبية الموجودة (سائق، خط، راكب، طلب معلق)
public static class RealDbHelper
{
    // يقرا سلسلة الاتصال من user-secrets ويرجع AppDbContext متصل بـ MySQL الحقيقية
    public static AppDbContext CreateContext()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddUserSecrets<DatabaseConnectionTests>()
            .Build();

        string? connectionString = config.GetConnectionString("DefaultConnection");
        Assert.False(string.IsNullOrEmpty(connectionString), "سلسلة الاتصال غير موجودة بـ user-secrets");

        MySqlServerVersion serverVersion = new MySqlServerVersion(new Version(8, 0, 45));
        DbContextOptionsBuilder<AppDbContext> optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseMySql(connectionString, serverVersion);
        return new AppDbContext(optionsBuilder.Options);
    }

    // ================= التأكد إن ولا شي انكتب =================

    // ياخذ صورة نصية للداتابيس: عدد الصفوف بالجداول المهمة + الحالات + وقت آخر دخول
    // إذا أي INSERT أو UPDATE أو DELETE صار، الصورة تتغير
    public static async Task<string> TakeSnapshotAsync()
    {
        using AppDbContext db = CreateContext();
        string counts = await GetCountsAsync(db);
        string statuses = await GetStatusesAsync(db);
        return counts + " | " + statuses;
    }

    // يتأكد إن الداتابيس هسه نفس الصورة اللي انأخذت قبل الاختبار
    public static async Task AssertNothingChangedAsync(string snapshotBefore)
    {
        string snapshotAfter = await TakeSnapshotAsync();
        Assert.Equal(snapshotBefore, snapshotAfter);
    }

    // عدد الصفوف بكل جدول ممكن الكود يكتب بيه
    private static async Task<string> GetCountsAsync(AppDbContext db)
    {
        return "users=" + await db.Users.CountAsync()
            + " drivers=" + await db.Drivers.CountAsync()
            + " vehicles=" + await db.Vehicles.CountAsync()
            + " lines=" + await db.Lines.CountAsync()
            + " linestops=" + await db.Linestops.CountAsync()
            + " linedays=" + await db.Linedays.CountAsync()
            + " lineprices=" + await db.Lineprices.CountAsync()
            + " subscriptions=" + await db.Subscriptions.CountAsync()
            + " notifications=" + await db.Notifications.CountAsync();
    }

    // الحالات اللي الكود يغيرها: حالة السائق، حالة الخط، حالة الاشتراك، ووقت آخر دخول
    private static async Task<string> GetStatusesAsync(AppDbContext db)
    {
        string result = "";
        foreach (Driver d in await db.Drivers.AsNoTracking().OrderBy(d => d.DriverId).ToListAsync())
            result += "D" + d.DriverId + ":" + d.VerificationStatus + ";";
        foreach (Line l in await db.Lines.AsNoTracking().OrderBy(l => l.LineId).ToListAsync())
            result += "L" + l.LineId + ":" + l.LineStatus + ";";
        foreach (Subscription s in await db.Subscriptions.AsNoTracking().OrderBy(s => s.SubscriptionId).ToListAsync())
            result += "S" + s.SubscriptionId + ":" + s.SubscriptionStatus + ";";
        foreach (User u in await db.Users.AsNoTracking().OrderBy(u => u.UserId).ToListAsync())
            result += "U" + u.UserId + ":" + u.LastLoginAt + ";";
        return result;
    }

    // ================= البيانات التجريبية =================

    // يلكى البيانات التجريبية الموجودة بالداتابيس: خط مفتوح لسائق موافق عليه، وراكب مشترك عليه، وطلب معلق
    public static async Task<RealSampleData> LoadSampleDataAsync(AppDbContext db)
    {
        RealSampleData data = new RealSampleData();

        Line? line = await db.Lines.Include(l => l.Driver)
            .FirstOrDefaultAsync(l => l.LineStatus == "Open" && l.Driver.VerificationStatus == "Approved");
        Assert.True(line != null, "ماكو خط مفتوح لسائق موافق عليه بالداتابيس (البيانات التجريبية ناقصة)");
        data.Line = line!;
        data.DriverUserId = line!.Driver.UserId;

        Subscription? anySubscription = await db.Subscriptions.FirstOrDefaultAsync(s => s.LineId == line.LineId);
        Assert.True(anySubscription != null, "ماكو اشتراكات على الخط التجريبي");
        data.PassengerUserId = anySubscription!.PassengerId;

        Subscription? pending = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.LineId == line.LineId && s.SubscriptionStatus == "Pending");
        data.PendingSubscriptionId = pending?.SubscriptionId;
        return data;
    }
}

// البيانات التجريبية اللي تستخدمها الاختبارات
public class RealSampleData
{
    // الخط التجريبي (مفتوح وسائقه موافق عليه)
    public Line Line { get; set; } = null!;

    // رقم المستخدم مال سائق الخط
    public int DriverUserId { get; set; }

    // رقم راكب عنده اشتراك على الخط
    public int PassengerUserId { get; set; }

    // رقم طلب معلق على الخط (إذا موجود)
    public int? PendingSubscriptionId { get; set; }
}
