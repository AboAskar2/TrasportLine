using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.Services;

namespace TransportLines.Tests;

// اختبارات خدمة الأدمن: الموافقة والرفض على السائقين وجلب صورهم
public class AdminServiceTests
{
    private readonly AppDbContext _db;
    private readonly AccountService _accountService;
    private readonly AdminService _adminService;
    private readonly User _admin;

    // كل اختبار ياخذ داتابيس وهمية جديدة بيها أدمن واحد
    public AdminServiceTests()
    {
        _db = TestHelper.CreateDatabase();
        _accountService = new AccountService(_db, new FileUploadService(TestHelper.CreateTempFolder()));
        _adminService = new AdminService(_db);
        _admin = TestHelper.AddAdmin(_db);
    }

    // يسجل سائق تجريبي (حالته Pending) ويرجعه
    private async Task<Driver> RegisterDriverAsync()
    {
        await _accountService.RegisterAsync(TestHelper.ValidDriver());
        return await _db.Drivers.SingleAsync();
    }

    // ===== عرض السائقين =====

    // القائمة ترجع السائقين Pending بس
    [Fact]
    public async Task GetPendingDrivers_ReturnsOnlyPending()
    {
        Driver driver = await RegisterDriverAsync();
        Assert.Single(await _adminService.GetPendingDriversAsync());

        await _adminService.ApproveDriverAsync(driver.DriverId, _admin.UserId);
        Assert.Empty(await _adminService.GetPendingDriversAsync());
    }

    // ===== الموافقة =====

    // موافقة صحيحة: الحالة تصير Approved، وينسجل منو وافق ومتى، وينرسل إشعار
    [Fact]
    public async Task Approve_PendingDriver_SetsApprovedAndNotifies()
    {
        Driver driver = await RegisterDriverAsync();

        ServiceResult result = await _adminService.ApproveDriverAsync(driver.DriverId, _admin.UserId);

        Assert.True(result.Success);
        Assert.Equal("Approved", driver.VerificationStatus);
        Assert.Equal(_admin.UserId, driver.VerifiedByUserId);
        Assert.NotNull(driver.VerifiedAt);
        Notification notification = await _db.Notifications.SingleAsync();
        Assert.Equal(driver.UserId, notification.UserId);
        Assert.Equal("DriverApproved", notification.Type);
    }

    // ما نكدر نوافق على سائق مرتين
    [Fact]
    public async Task Approve_AlreadyApproved_Fails()
    {
        Driver driver = await RegisterDriverAsync();
        await _adminService.ApproveDriverAsync(driver.DriverId, _admin.UserId);

        ServiceResult result = await _adminService.ApproveDriverAsync(driver.DriverId, _admin.UserId);

        Assert.False(result.Success);
    }

    // سائق مو موجود يرجع خطأ
    [Fact]
    public async Task Approve_UnknownDriver_Fails()
    {
        ServiceResult result = await _adminService.ApproveDriverAsync(999, _admin.UserId);

        Assert.False(result.Success);
    }

    // مستخدم مو أدمن (حتى لو وصل للخدمة) ما يكدر يوافق
    [Fact]
    public async Task Approve_ByNonAdmin_Fails()
    {
        Driver driver = await RegisterDriverAsync();

        ServiceResult result = await _adminService.ApproveDriverAsync(driver.DriverId, driver.UserId);

        Assert.False(result.Success);
        Assert.Equal("Pending", driver.VerificationStatus);
    }

    // أدمن موقوف ما يكدر يوافق
    [Fact]
    public async Task Approve_BySuspendedAdmin_Fails()
    {
        Driver driver = await RegisterDriverAsync();
        _admin.AccountStatus = "Suspended";
        await _db.SaveChangesAsync();

        ServiceResult result = await _adminService.ApproveDriverAsync(driver.DriverId, _admin.UserId);

        Assert.False(result.Success);
    }

    // إذا الإجازة انتهت قبل الموافقة، الموافقة ترفض
    [Fact]
    public async Task Approve_ExpiredLicense_Fails()
    {
        Driver driver = await RegisterDriverAsync();
        driver.LicenseExpiryDate = DateOnly.FromDateTime(DateTime.Today);
        await _db.SaveChangesAsync();

        ServiceResult result = await _adminService.ApproveDriverAsync(driver.DriverId, _admin.UserId);

        Assert.False(result.Success);
        Assert.Equal("Pending", driver.VerificationStatus);
    }

    // ===== الرفض =====

    // رفض صحيح: الحالة Rejected والسبب ينحفظ، وينرسل إشعار بالسبب
    [Fact]
    public async Task Reject_WithReason_SetsRejectedAndNotifies()
    {
        Driver driver = await RegisterDriverAsync();

        ServiceResult result = await _adminService.RejectDriverAsync(driver.DriverId, _admin.UserId, "  صورة الإجازة غير واضحة  ");

        Assert.True(result.Success);
        Assert.Equal("Rejected", driver.VerificationStatus);
        Assert.Equal("صورة الإجازة غير واضحة", driver.RejectionReason);
        Notification notification = await _db.Notifications.SingleAsync();
        Assert.Contains("صورة الإجازة غير واضحة", notification.Message);
    }

    // رفض بدون سبب أو بسبب قصير أو طويل جداً يرجع خطأ
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("لا")]
    public async Task Reject_InvalidReason_Fails(string? reason)
    {
        Driver driver = await RegisterDriverAsync();

        ServiceResult result = await _adminService.RejectDriverAsync(driver.DriverId, _admin.UserId, reason);

        Assert.True(result.Errors.ContainsKey("Reason"));
        Assert.Equal("Pending", driver.VerificationStatus);
    }

    // سبب أطول من 255 حرف يرجع خطأ
    [Fact]
    public async Task Reject_TooLongReason_Fails()
    {
        Driver driver = await RegisterDriverAsync();

        ServiceResult result = await _adminService.RejectDriverAsync(driver.DriverId, _admin.UserId, new string('a', 256));

        Assert.True(result.Errors.ContainsKey("Reason"));
    }

    // ما نكدر نرفض سائق موافق عليه
    [Fact]
    public async Task Reject_AlreadyApproved_Fails()
    {
        Driver driver = await RegisterDriverAsync();
        await _adminService.ApproveDriverAsync(driver.DriverId, _admin.UserId);

        ServiceResult result = await _adminService.RejectDriverAsync(driver.DriverId, _admin.UserId, "سبب الرفض هنا");

        Assert.False(result.Success);
        Assert.Equal("Approved", driver.VerificationStatus);
    }

    // ===== صور السائق =====

    // الأنواع المسموحة ترجع مسار، والأنواع الغريبة أو محاولات المسار ترجع null
    [Fact]
    public async Task GetDriverPhotoPath_OnlyAllowedTypes()
    {
        Driver driver = await RegisterDriverAsync();

        Assert.Equal(driver.LicensePhotoUrl, await _adminService.GetDriverPhotoPathAsync(driver.DriverId, "license"));
        Assert.Equal(driver.IdphotoUrl, await _adminService.GetDriverPhotoPathAsync(driver.DriverId, "id"));
        Assert.NotNull(await _adminService.GetDriverPhotoPathAsync(driver.DriverId, "registration"));
        Assert.Null(await _adminService.GetDriverPhotoPathAsync(driver.DriverId, "vehicle"));
        Assert.Null(await _adminService.GetDriverPhotoPathAsync(driver.DriverId, "../../appsettings.json"));
        Assert.Null(await _adminService.GetDriverPhotoPathAsync(driver.DriverId, null));
        Assert.Null(await _adminService.GetDriverPhotoPathAsync(999, "license"));
    }
}
