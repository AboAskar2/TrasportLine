using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.Services;

namespace TransportLines.Tests;

// اختبارات تسجيل الدخول بـ AccountService
public class AccountServiceLoginTests
{
    private const string Phone = "07701234567";
    private const string Password = "Pass1234";
    private const string WrongLoginMessage = "رقم الهاتف أو كلمة المرور غير صحيحة";

    private readonly AppDbContext _db;
    private readonly AccountService _service;

    // كل اختبار ياخذ داتابيس وهمية جديدة
    public AccountServiceLoginTests()
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

    // رقم وكلمة مرور صحيحة: الدخول ينجح ووقت آخر دخول يتحدث
    [Fact]
    public async Task Login_CorrectData_Succeeds()
    {
        await RegisterTestUserAsync();

        LoginResult result = await _service.LoginAsync(Phone, Password);

        Assert.True(result.Success);
        Assert.Equal("Passenger", result.RoleName);
        User user = await _db.Users.SingleAsync();
        Assert.NotNull(user.LastLoginAt);
    }

    // كلمة مرور غلط: الدخول يفشل برسالة عامة
    [Fact]
    public async Task Login_WrongPassword_Fails()
    {
        await RegisterTestUserAsync();

        LoginResult result = await _service.LoginAsync(Phone, "Wrong1234");

        Assert.False(result.Success);
        Assert.Equal(WrongLoginMessage, result.ErrorMessage);
    }

    // رقم مو مسجل: نفس الرسالة العامة (حتى ما نكشف الأرقام المسجلة)
    [Fact]
    public async Task Login_UnknownPhone_FailsWithSameMessage()
    {
        LoginResult result = await _service.LoginAsync("07709999999", Password);

        Assert.False(result.Success);
        Assert.Equal(WrongLoginMessage, result.ErrorMessage);
    }

    // محاولات SQL Injection أو قيم فارغة: الدخول يفشل بدون ما يطيح البرنامج
    [Theory]
    [InlineData("' OR '1'='1", "' OR '1'='1")]
    [InlineData("07701234567' --", "anything")]
    [InlineData(null, null)]
    [InlineData("", "")]
    public async Task Login_InjectionOrEmpty_Fails(string? phone, string? password)
    {
        await RegisterTestUserAsync();

        LoginResult result = await _service.LoginAsync(phone, password);

        Assert.False(result.Success);
    }

    // حساب موقوف: الدخول يفشل برسالة الإيقاف
    [Fact]
    public async Task Login_SuspendedAccount_Fails()
    {
        User user = await RegisterTestUserAsync();
        user.AccountStatus = "Suspended";
        await _db.SaveChangesAsync();

        LoginResult result = await _service.LoginAsync(Phone, Password);

        Assert.False(result.Success);
        Assert.Equal("حسابك موقوف، راجع الإدارة", result.ErrorMessage);
    }

    // حساب محذوف (Soft delete): الدخول يفشل بالرسالة العامة
    [Fact]
    public async Task Login_DeletedAccount_Fails()
    {
        User user = await RegisterTestUserAsync();
        user.IsDeleted = true;
        await _db.SaveChangesAsync();

        LoginResult result = await _service.LoginAsync(Phone, Password);

        Assert.False(result.Success);
        Assert.Equal(WrongLoginMessage, result.ErrorMessage);
    }

    // كلمة مرور صحيحة لكن بحروف كبيرة/صغيرة مختلفة لازم تفشل
    [Fact]
    public async Task Login_PasswordIsCaseSensitive()
    {
        await RegisterTestUserAsync();

        LoginResult result = await _service.LoginAsync(Phone, "pass1234");

        Assert.False(result.Success);
    }
}
