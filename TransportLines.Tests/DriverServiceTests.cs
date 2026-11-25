using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.Services;

namespace TransportLines.Tests;

// اختبارات خدمة السائق: الملف الشخصي وحالة الموافقة
public class DriverServiceTests
{
    private readonly AppDbContext _db;
    private readonly AccountService _accountService;
    private readonly DriverService _driverService;

    // كل اختبار ياخذ داتابيس وهمية جديدة
    public DriverServiceTests()
    {
        _db = TestHelper.CreateDatabase();
        _accountService = new AccountService(_db, new FileUploadService(TestHelper.CreateTempFolder()));
        _driverService = new DriverService(_db);
    }

    // يسجل سائق تجريبي ويرجعه
    private async Task<Driver> RegisterDriverAsync()
    {
        await _accountService.RegisterAsync(TestHelper.ValidDriver());
        return await _db.Drivers.SingleAsync();
    }

    // الملف الشخصي يرجع السائق نفسه ويا سيارته
    [Fact]
    public async Task GetDriverProfile_ReturnsOwnDriverWithVehicle()
    {
        Driver driver = await RegisterDriverAsync();

        Driver? profile = await _driverService.GetDriverProfileAsync(driver.UserId);

        Assert.NotNull(profile);
        Assert.Equal(driver.DriverId, profile.DriverId);
        Assert.Single(profile.Vehicles);
    }

    // راكب (مو سائق) يرجع null
    [Fact]
    public async Task GetDriverProfile_ForPassenger_ReturnsNull()
    {
        await _accountService.RegisterAsync(TestHelper.ValidPassenger());
        User passenger = await _db.Users.SingleAsync();

        Assert.Null(await _driverService.GetDriverProfileAsync(passenger.UserId));
    }

    // السائق Pending مو موافق عليه، وبعد ما تصير حالته Approved يصير موافق
    [Fact]
    public async Task IsApprovedDriver_DependsOnStatus()
    {
        Driver driver = await RegisterDriverAsync();
        Assert.False(await _driverService.IsApprovedDriverAsync(driver.UserId));

        driver.VerificationStatus = "Approved";
        await _db.SaveChangesAsync();
        Assert.True(await _driverService.IsApprovedDriverAsync(driver.UserId));
    }

    // رسالة الحالة تتغير حسب حالة السائق، والرفض يعرض السبب
    [Fact]
    public void GetStatusMessage_ShowsCorrectText()
    {
        Driver driver = new Driver { VerificationStatus = "Rejected", RejectionReason = "صورة غير واضحة" };
        Assert.Contains("صورة غير واضحة", DriverService.GetStatusMessage(driver));

        driver.VerificationStatus = "Pending";
        Assert.Contains("ينتظر", DriverService.GetStatusMessage(driver));

        driver.VerificationStatus = "Approved";
        Assert.Contains("موافق", DriverService.GetStatusMessage(driver));
    }
}
