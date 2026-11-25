using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.Services;
using TransportLines.ViewModels;

namespace TransportLines.Tests;

// اختبارات التسجيل بـ AccountService: الحالات الصحيحة ومحاولات الإدخال الخاطئ أو الخبيث
public class AccountServiceRegisterTests
{
    private readonly AppDbContext _db;
    private readonly string _uploadFolder;
    private readonly AccountService _service;

    // كل اختبار ياخذ داتابيس وهمية جديدة ومجلد صور مؤقت
    public AccountServiceRegisterTests()
    {
        _db = TestHelper.CreateDatabase();
        _uploadFolder = TestHelper.CreateTempFolder();
        _service = new AccountService(_db, new FileUploadService(_uploadFolder));
    }

    // ===== تسجيل راكب =====

    // راكب ببيانات صحيحة لازم ينحفظ، وكلمة المرور تنحفظ هاش مو نص
    [Fact]
    public async Task Register_ValidPassenger_SavesUserWithHashedPassword()
    {
        ServiceResult result = await _service.RegisterAsync(TestHelper.ValidPassenger());

        Assert.True(result.Success);
        User user = await _db.Users.SingleAsync();
        Assert.Equal(1, user.RoleId);
        Assert.Equal("Active", user.AccountStatus);
        Assert.NotEqual("Pass1234", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Pass1234", user.PasswordHash));
    }

    // إذا أحد عدّل الفورم وكتب Admin لازم ينرفض وما ينحفظ شي
    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("SuperUser")]
    [InlineData("")]
    public async Task Register_NotAllowedAccountType_IsRejected(string accountType)
    {
        RegisterViewModel model = TestHelper.ValidPassenger();
        model.AccountType = accountType;

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.False(result.Success);
        Assert.True(result.Errors.ContainsKey("AccountType"));
        Assert.Equal(0, await _db.Users.CountAsync());
    }

    // رقم الهاتف المكرر لازم ينرفض
    [Fact]
    public async Task Register_DuplicatePhone_IsRejected()
    {
        await _service.RegisterAsync(TestHelper.ValidPassenger());
        RegisterViewModel second = TestHelper.ValidPassenger();
        second.Email = "other@example.com";

        ServiceResult result = await _service.RegisterAsync(second);

        Assert.True(result.Errors.ContainsKey("PhoneNumber"));
    }

    // الإيميل المكرر لازم ينرفض حتى لو اختلفت الحروف الكبيرة والصغيرة
    [Fact]
    public async Task Register_DuplicateEmailDifferentCase_IsRejected()
    {
        await _service.RegisterAsync(TestHelper.ValidPassenger());
        RegisterViewModel second = TestHelper.ValidPassenger();
        second.PhoneNumber = "07701111111";
        second.Email = "  ALI@Example.com ";

        ServiceResult result = await _service.RegisterAsync(second);

        Assert.True(result.Errors.ContainsKey("Email"));
    }

    // أرقام هاتف غلط لازم تنرفض
    [Theory]
    [InlineData("0770123456")]
    [InlineData("077012345678")]
    [InlineData("08701234567")]
    [InlineData("0770abc4567")]
    [InlineData("' OR 1=1 --")]
    public async Task Register_InvalidPhone_IsRejected(string phone)
    {
        RegisterViewModel model = TestHelper.ValidPassenger();
        model.PhoneNumber = phone;

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.True(result.Errors.ContainsKey("PhoneNumber"));
    }

    // كلمات مرور ضعيفة أو بيها حروف غير مسموحة لازم تنرفض
    [Theory]
    [InlineData("short1")]
    [InlineData("onlyletters")]
    [InlineData("12345678")]
    [InlineData("كلمةسر123")]
    [InlineData("Pass 1234")]
    public async Task Register_WeakPassword_IsRejected(string password)
    {
        RegisterViewModel model = TestHelper.ValidPassenger();
        model.Password = password;
        model.ConfirmPassword = password;

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.True(result.Errors.ContainsKey("Password"));
    }

    // تأكيد كلمة المرور لازم يطابق
    [Fact]
    public async Task Register_ConfirmPasswordMismatch_IsRejected()
    {
        RegisterViewModel model = TestHelper.ValidPassenger();
        model.ConfirmPassword = "Different1";

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.True(result.Errors.ContainsKey("ConfirmPassword"));
    }

    // اسم بيه أكواد أو أرقام (محاولة XSS) لازم ينرفض
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("Ali123")]
    [InlineData("ab")]
    public async Task Register_InvalidName_IsRejected(string name)
    {
        RegisterViewModel model = TestHelper.ValidPassenger();
        model.FullName = name;

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.True(result.Errors.ContainsKey("FullName"));
    }

    // قيمة جنس غير Male و Female لازم تنرفض
    [Fact]
    public async Task Register_InvalidGender_IsRejected()
    {
        RegisterViewModel model = TestHelper.ValidPassenger();
        model.Gender = "Other";

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.True(result.Errors.ContainsKey("Gender"));
    }

    // ===== تسجيل سائق =====

    // سائق ببيانات صحيحة: ينحفظ User + Driver (Pending) + Vehicle، والصور تنحفظ بالمجلد
    [Fact]
    public async Task Register_ValidDriver_SavesUserDriverAndVehicle()
    {
        ServiceResult result = await _service.RegisterAsync(TestHelper.ValidDriver());

        Assert.True(result.Success);
        User user = await _db.Users.SingleAsync();
        Driver driver = await _db.Drivers.SingleAsync();
        Vehicle vehicle = await _db.Vehicles.SingleAsync();
        Assert.Equal(2, user.RoleId);
        Assert.Equal("Pending", driver.VerificationStatus);
        Assert.Equal(driver.DriverId, vehicle.DriverId);
        Assert.True(File.Exists(Path.Combine(_uploadFolder, driver.LicensePhotoUrl)));
    }

    // سائق بدون صور لازم ينرفض بخطأ لكل صورة إجبارية
    [Fact]
    public async Task Register_DriverWithoutPhotos_IsRejected()
    {
        RegisterViewModel model = TestHelper.ValidDriver();
        model.LicensePhoto = null;
        model.IdPhoto = null;
        model.RegistrationPhoto = null;

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.True(result.Errors.ContainsKey("LicensePhoto"));
        Assert.True(result.Errors.ContainsKey("IdPhoto"));
        Assert.True(result.Errors.ContainsKey("RegistrationPhoto"));
        Assert.Equal(0, await _db.Users.CountAsync());
    }

    // سائق بدون بيانات الإجازة والسيارة لازم ينرفض
    [Fact]
    public async Task Register_DriverMissingFields_IsRejected()
    {
        RegisterViewModel model = TestHelper.ValidDriver();
        model.LicenseNumber = null;
        model.NationalIdNumber = null;
        model.VehicleTypeId = null;
        model.PlateNumber = null;

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.True(result.Errors.ContainsKey("LicenseNumber"));
        Assert.True(result.Errors.ContainsKey("NationalIdNumber"));
        Assert.True(result.Errors.ContainsKey("VehicleTypeId"));
        Assert.True(result.Errors.ContainsKey("PlateNumber"));
    }

    // إجازة منتهية أو فحص منتهي لازم ينرفض
    [Fact]
    public async Task Register_DriverExpiredDates_IsRejected()
    {
        RegisterViewModel model = TestHelper.ValidDriver();
        model.LicenseExpiryDate = DateTime.Today;
        model.InspectionExpiryDate = DateTime.Today.AddDays(-1);

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.True(result.Errors.ContainsKey("LicenseExpiryDate"));
        Assert.True(result.Errors.ContainsKey("InspectionExpiryDate"));
    }

    // عدد مقاعد أكثر من الحد الأعلى لنوع السيارة (14) لازم ينرفض
    [Fact]
    public async Task Register_DriverTooManySeats_IsRejected()
    {
        RegisterViewModel model = TestHelper.ValidDriver();
        model.SeatCapacity = 15;

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.True(result.Errors.ContainsKey("SeatCapacity"));
    }

    // نوع سيارة موقوف أو مو موجود لازم ينرفض
    [Theory]
    [InlineData(2)]
    [InlineData(999)]
    public async Task Register_DriverInvalidVehicleType_IsRejected(int typeId)
    {
        RegisterViewModel model = TestHelper.ValidDriver();
        model.VehicleTypeId = typeId;

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.True(result.Errors.ContainsKey("VehicleTypeId"));
    }

    // رقم لوحة أو إجازة مسجل من قبل لازم ينرفض
    [Fact]
    public async Task Register_DriverDuplicatePlateAndLicense_IsRejected()
    {
        await _service.RegisterAsync(TestHelper.ValidDriver());
        RegisterViewModel second = TestHelper.ValidDriver();
        second.PhoneNumber = "07805555555";
        second.Email = "second@example.com";
        second.NationalIdNumber = "199099999999";

        ServiceResult result = await _service.RegisterAsync(second);

        Assert.True(result.Errors.ContainsKey("PlateNumber"));
        Assert.True(result.Errors.ContainsKey("LicenseNumber"));
    }

    // صورة مزيفة (سكربت اسمه .jpg) بفورم السائق لازم تنرفض
    [Fact]
    public async Task Register_DriverFakeImage_IsRejected()
    {
        RegisterViewModel model = TestHelper.ValidDriver();
        byte[] script = System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>");
        model.LicensePhoto = TestHelper.CreateFile(script, "license.jpg");

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.True(result.Errors.ContainsKey("LicensePhoto"));
    }

    // حقول السائق تنتجاهل إذا الحساب راكب (ما ينحفظ Driver)
    [Fact]
    public async Task Register_PassengerWithDriverFields_DoesNotCreateDriver()
    {
        RegisterViewModel model = TestHelper.ValidDriver();
        model.AccountType = "Passenger";

        ServiceResult result = await _service.RegisterAsync(model);

        Assert.True(result.Success);
        Assert.Equal(0, await _db.Drivers.CountAsync());
    }
}
