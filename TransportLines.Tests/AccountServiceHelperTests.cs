using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.Services;

namespace TransportLines.Tests;

// اختبارات أدوات AccountService اللي يستخدمها الكونترولر: بناء الـ Claims، فحص حالة الحساب، وأنواع السيارات
public class AccountServiceHelperTests
{
    private readonly AppDbContext _db;
    private readonly AccountService _service;

    // كل اختبار ياخذ داتابيس وهمية جديدة
    public AccountServiceHelperTests()
    {
        _db = TestHelper.CreateDatabase();
        _service = new AccountService(_db, new FileUploadService(TestHelper.CreateTempFolder()));
    }

    // يسجل راكب تجريبي ويرجعه من الداتابيس
    private async Task<User> RegisterTestUserAsync()
    {
        await _service.RegisterAsync(TestHelper.ValidPassenger());
        return await _db.Users.SingleAsync();
    }

    // الـ Claims لازم تحمل رقم المستخدم واسمه ودوره، والهوية تكون مسجلة دخول
    [Fact]
    public void CreatePrincipal_ContainsIdNameAndRole()
    {
        LoginResult login = new LoginResult { Success = true, UserId = 7, FullName = "علي حسين", RoleName = "Driver" };

        ClaimsPrincipal principal = AccountService.CreatePrincipal(login);

        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Equal("7", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("علي حسين", principal.FindFirstValue(ClaimTypes.Name));
        Assert.True(principal.IsInRole("Driver"));
        Assert.False(principal.IsInRole("Admin"));
    }

    // حساب فعّال يرجع true
    [Fact]
    public async Task IsUserActive_ActiveUser_ReturnsTrue()
    {
        User user = await RegisterTestUserAsync();

        Assert.True(await _service.IsUserActiveAsync(user.UserId));
    }

    // حساب موقوف أو محظور يرجع false (حتى يطلع من الموقع فوراً)
    [Theory]
    [InlineData("Suspended")]
    [InlineData("Banned")]
    public async Task IsUserActive_SuspendedOrBanned_ReturnsFalse(string status)
    {
        User user = await RegisterTestUserAsync();
        user.AccountStatus = status;
        await _db.SaveChangesAsync();

        Assert.False(await _service.IsUserActiveAsync(user.UserId));
    }

    // حساب محذوف يرجع false
    [Fact]
    public async Task IsUserActive_DeletedUser_ReturnsFalse()
    {
        User user = await RegisterTestUserAsync();
        user.IsDeleted = true;
        await _db.SaveChangesAsync();

        Assert.False(await _service.IsUserActiveAsync(user.UserId));
    }

    // رقم مستخدم مو موجود يرجع false
    [Fact]
    public async Task IsUserActive_UnknownUser_ReturnsFalse()
    {
        Assert.False(await _service.IsUserActiveAsync(999));
    }

    // قائمة أنواع السيارات ترجع الفعّالة بس (Van) وما ترجع الموقوفة (OldBus)
    [Fact]
    public async Task GetActiveVehicleTypes_ReturnsOnlyActive()
    {
        List<Vehicletype> types = await _service.GetActiveVehicleTypesAsync();

        Assert.Single(types);
        Assert.Equal("Van", types[0].TypeName);
    }
}
