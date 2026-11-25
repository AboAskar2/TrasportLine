using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.Services;
using TransportLines.ViewModels;

namespace TransportLines.Tests;

// أدوات مساعدة للاختبارات: داتابيس وهمية بالذاكرة، صور وهمية، وفورمات تسجيل جاهزة
// الداتابيس الوهمية تعني إن الاختبارات ما تلمس الداتابيس الحقيقية أبداً
public static class TestHelper
{
    // أول بايتات ملف JPG حقيقي
    private static readonly byte[] JpgBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };

    // أول بايتات ملف PNG حقيقي
    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // يسوي داتابيس جديدة فارغة بالذاكرة ويضيف بيها الأدوار ونوعين سيارات
    public static AppDbContext CreateDatabase()
    {
        // اسم عشوائي حتى كل اختبار ياخذ داتابيس خاصة بيه
        // الداتابيس الوهمية ما تدعم Transactions، فنخليها تتجاهلها بدل ما تطلع خطأ
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        AppDbContext db = new AppDbContext(options);
        SeedLookupData(db);
        return db;
    }

    // يضيف بيانات الجداول المرجعية: الأدوار وأنواع السيارات
    private static void SeedLookupData(AppDbContext db)
    {
        db.Roles.Add(new Role { RoleId = 1, RoleName = "Passenger" });
        db.Roles.Add(new Role { RoleId = 2, RoleName = "Driver" });
        db.Roles.Add(new Role { RoleId = 3, RoleName = "Admin" });

        // نوع فعّال (أقصى مقاعد 14) ونوع موقوف
        db.Vehicletypes.Add(new Vehicletype { VehicleTypeId = 1, TypeName = "Van", MaxSeats = 14, IsActive = true });
        db.Vehicletypes.Add(new Vehicletype { VehicleTypeId = 2, TypeName = "OldBus", MaxSeats = 30, IsActive = false });

        // منطقة وحدة بيها 4 محطات فعّالة (1 إلى 4) ومحطة موقوفة (5)
        db.Areas.Add(new Area { AreaId = 1, AreaName = "الكرادة", IsActive = true });
        db.Stops.Add(new Stop { StopId = 1, AreaId = 1, StopName = "ساحة الحرية", IsActive = true });
        db.Stops.Add(new Stop { StopId = 2, AreaId = 1, StopName = "شارع 62", IsActive = true });
        db.Stops.Add(new Stop { StopId = 3, AreaId = 1, StopName = "الجامعة التكنولوجية", IsActive = true });
        db.Stops.Add(new Stop { StopId = 4, AreaId = 1, StopName = "ساحة الأندلس", IsActive = true });
        db.Stops.Add(new Stop { StopId = 5, AreaId = 1, StopName = "محطة قديمة", IsActive = false });
        db.SaveChanges();
    }

    // يسجل سائق ويوافق عليه مباشرة (رقم مختلف لكل سائق حتى ما يصير تكرار)، ويرجعه
    public static async Task<Driver> AddApprovedDriverAsync(AppDbContext db, int number = 1)
    {
        AccountService accountService = new AccountService(db, new FileUploadService(CreateTempFolder()));
        RegisterViewModel model = ValidDriver();
        model.PhoneNumber = "0780000000" + number;
        model.Email = "driver" + number + "@example.com";
        model.LicenseNumber = "LIC-" + number;
        model.NationalIdNumber = "10000000000" + number;
        model.PlateNumber = "1000" + number + " بغداد";
        await accountService.RegisterAsync(model);

        Driver driver = await db.Drivers.Include(d => d.Vehicles).SingleAsync(d => d.LicenseNumber == model.LicenseNumber);
        driver.VerificationStatus = "Approved";
        await db.SaveChangesAsync();
        return driver;
    }

    // يسجل راكب (ذكر أو أنثى) برقم مختلف لكل راكب، ويرجعه
    public static async Task<User> AddPassengerAsync(AppDbContext db, int number = 1, string gender = "Male")
    {
        AccountService accountService = new AccountService(db, new FileUploadService(CreateTempFolder()));
        RegisterViewModel model = ValidPassenger();
        model.PhoneNumber = "0770000000" + number;
        model.Email = "passenger" + number + "@example.com";
        model.Gender = gender;
        await accountService.RegisterAsync(model);
        return await db.Users.SingleAsync(u => u.PhoneNumber == model.PhoneNumber);
    }

    // فورم خط صحيح: 3 محطات (1، 2، 3)، 3 أيام، وسعرين (منها سعر الخط الكامل 1 ← 3)
    public static CreateLineViewModel ValidLine()
    {
        CreateLineViewModel model = new CreateLineViewModel();
        model.LineName = "الكرادة - الجامعة";
        model.DepartureTime = new TimeSpan(7, 0, 0);
        model.TotalSeats = 10;
        model.MinSubscriptionMonths = 1;
        model.SelectedDays = new List<int> { 0, 1, 2 };
        model.Stops.Add(new LineStopInput { StopId = 1, EstimatedArrivalTime = new TimeSpan(7, 0, 0) });
        model.Stops.Add(new LineStopInput { StopId = 2, EstimatedArrivalTime = new TimeSpan(7, 15, 0) });
        model.Stops.Add(new LineStopInput { StopId = 3, EstimatedArrivalTime = new TimeSpan(7, 40, 0) });
        model.Prices.Add(new LinePriceInput { FromStopId = 1, ToStopId = 3, MonthlyPrice = 50000 });
        model.Prices.Add(new LinePriceInput { FromStopId = 1, ToStopId = 2, MonthlyPrice = 30000 });
        return model;
    }

    // يضيف حساب أدمن مباشرة بالداتابيس الوهمية (بالواقع الأدمن ينضاف بسكربت، مو من فورم التسجيل)
    public static User AddAdmin(AppDbContext db, string phone = "07500000001")
    {
        User admin = new User();
        admin.RoleId = 3;
        admin.FullName = "مدير النظام";
        admin.PhoneNumber = phone;
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin1234", 4);
        admin.AccountStatus = "Active";
        admin.CreatedAt = DateTime.Now;
        db.Users.Add(admin);
        db.SaveChanges();
        return admin;
    }

    // يسوي مجلد مؤقت جديد لرفع الصور بالاختبار
    public static string CreateTempFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "TransportLinesTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    // يسوي ملف مرفوع وهمي بالبايتات والاسم المطلوب
    public static IFormFile CreateFile(byte[] content, string fileName)
    {
        MemoryStream stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName);
    }

    // صورة JPG وهمية صحيحة
    public static IFormFile CreateJpg(string fileName = "photo.jpg")
    {
        return CreateFile(JpgBytes, fileName);
    }

    // صورة PNG وهمية صحيحة
    public static IFormFile CreatePng(string fileName = "photo.png")
    {
        return CreateFile(PngBytes, fileName);
    }

    // فورم تسجيل راكب صحيح (كل الحقول سليمة)
    public static RegisterViewModel ValidPassenger()
    {
        RegisterViewModel model = new RegisterViewModel();
        model.AccountType = "Passenger";
        model.FullName = "علي حسين";
        model.PhoneNumber = "07701234567";
        model.Email = "ali@example.com";
        model.Password = "Pass1234";
        model.ConfirmPassword = "Pass1234";
        model.Gender = "Male";
        return model;
    }

    // فورم تسجيل سائق صحيح (الحقول المشتركة + الإجازة + السيارة + الصور)
    public static RegisterViewModel ValidDriver()
    {
        RegisterViewModel model = ValidPassenger();
        model.AccountType = "Driver";
        model.PhoneNumber = "07809876543";
        model.Email = "driver@example.com";
        model.LicenseNumber = "LIC-12345";
        model.LicenseExpiryDate = DateTime.Today.AddYears(2);
        model.NationalIdNumber = "199012345678";
        model.LicensePhoto = CreateJpg();
        model.IdPhoto = CreatePng();
        AddValidVehicle(model);
        return model;
    }

    // يملي بيانات سيارة صحيحة بالفورم
    private static void AddValidVehicle(RegisterViewModel model)
    {
        model.VehicleTypeId = 1;
        model.PlateNumber = "12345 بغداد";
        model.Brand = "Toyota";
        model.VehicleModel = "Hiace";
        model.Color = "أبيض";
        model.ManufactureYear = 2018;
        model.SeatCapacity = 12;
        model.InspectionExpiryDate = DateTime.Today.AddYears(1);
        model.RegistrationPhoto = CreateJpg();
        model.HasAirConditioning = true;
    }
}

