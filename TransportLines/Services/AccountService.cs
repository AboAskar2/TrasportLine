using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.ViewModels;

namespace TransportLines.Services;

// خدمة الحسابات: تسجيل راكب، تسجيل سائق (مع سيارته)، وتسجيل الدخول
// هنا الفالديشن الحقيقي اللي ما ينتجاوز حتى لو أحد عدّل الفورم بالمتصفح
public class AccountService
{
    // قوة تشفير bcrypt (كل ما يزيد الرقم يصير التخمين أبطأ على المخترق)
    private const int BcryptWorkFactor = 11;

    // هاش وهمي نستخدمه إذا رقم الهاتف مو موجود، حتى وقت الرد يبقى نفسه
    // وهيج المخترق ما يكدر يعرف الرقم مسجل لو لا من سرعة الرد
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("dummy-password-123", BcryptWorkFactor);

    // رسالة خطأ واحدة للدخول الفاشل، حتى ما نكشف إذا الرقم موجود لو لا
    private const string WrongLoginMessage = "رقم الهاتف أو كلمة المرور غير صحيحة";

    private readonly AppDbContext _db;
    private readonly FileUploadService _fileService;

    // يستلم الداتابيس وخدمة رفع الصور
    public AccountService(AppDbContext db, FileUploadService fileService)
    {
        _db = db;
        _fileService = fileService;
    }

    // ================= التسجيل =================

    // يسجل حساب جديد (راكب أو سائق) بعد ما يتحقق من كل الحقول
    public async Task<ServiceResult> RegisterAsync(RegisterViewModel model)
    {
        ServiceResult result = new ServiceResult();
        CleanInput(model);

        // التحقق من الحقول المشتركة ثم حقول السائق
        ValidateBasicFields(model, result);
        await CheckUniqueUserFieldsAsync(model, result);
        if (model.AccountType == "Driver")
        {
            await ValidateDriverAsync(model, result);
        }

        if (!result.Success)
        {
            return result;
        }

        await SaveAccountAsync(model, result);
        return result;
    }

    // يحفظ الحساب، وإذا صار تكرار بنفس اللحظة (رقم أو إيميل) يرجع رسالة واضحة بدل ما يطيح البرنامج
    private async Task SaveAccountAsync(RegisterViewModel model, ServiceResult result)
    {
        try
        {
            if (model.AccountType == "Driver")
            {
                await SaveDriverAsync(model);
            }
            else
            {
                await SavePassengerAsync(model);
            }
        }
        catch (DbUpdateException)
        {
            result.AddError("", "صار خطأ بالحفظ، يمكن البيانات مسجلة من قبل. حاول مرة ثانية.");
        }
    }

    // يشيل المسافات الزايدة من النصوص ويحول الإيميل لحروف صغيرة
    private static void CleanInput(RegisterViewModel model)
    {
        model.AccountType = (model.AccountType ?? "").Trim();
        model.FullName = (model.FullName ?? "").Trim();
        model.PhoneNumber = (model.PhoneNumber ?? "").Trim();
        model.Gender = (model.Gender ?? "").Trim();
        model.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim().ToLower();
        model.LicenseNumber = model.LicenseNumber?.Trim().ToUpper();
        model.NationalIdNumber = model.NationalIdNumber?.Trim();
        model.PlateNumber = model.PlateNumber?.Trim().ToUpper();
        model.Brand = model.Brand?.Trim();
        model.VehicleModel = model.VehicleModel?.Trim();
        model.Color = model.Color?.Trim();
    }

    // يتحقق من الحقول المشتركة: نوع الحساب والجنس، ثم الاسم والهاتف والإيميل، ثم كلمة المرور
    private static void ValidateBasicFields(RegisterViewModel model, ServiceResult result)
    {
        // نرفض أي نوع غير Passenger و Driver (يعني Admin مرفوض حتى لو انكتب بالفورم)
        if (model.AccountType != "Passenger" && model.AccountType != "Driver")
            result.AddError("AccountType", "نوع الحساب غير مسموح");

        if (model.Gender != "Male" && model.Gender != "Female")
            result.AddError("Gender", "اختر الجنس");

        ValidateNameAndContact(model, result);
        ValidatePassword(model, result);
    }

    // يتحقق من صيغة الاسم ورقم الهاتف والإيميل
    private static void ValidateNameAndContact(RegisterViewModel model, ServiceResult result)
    {
        if (!Regex.IsMatch(model.FullName, @"^[\p{L} ]{3,100}$"))
            result.AddError("FullName", "الاسم لازم يكون حروف ومسافات فقط (3 إلى 100 حرف)");

        if (!Regex.IsMatch(model.PhoneNumber, "^07[0-9]{9}$"))
            result.AddError("PhoneNumber", "رقم الهاتف لازم يكون 11 رقم ويبدأ بـ 07");

        if (model.Email != null && !Regex.IsMatch(model.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            result.AddError("Email", "صيغة الإيميل غير صحيحة");
    }

    // يتحقق من قوة كلمة المرور وإن التأكيد يطابقها
    private static void ValidatePassword(RegisterViewModel model, ServiceResult result)
    {
        if (!IsStrongPassword(model.Password))
            result.AddError("Password", "كلمة المرور لازم تكون 8 إلى 64 حرف إنكليزي، وبيها حرف ورقم على الأقل");

        if (model.Password != model.ConfirmPassword)
            result.AddError("ConfirmPassword", "كلمتا المرور غير متطابقتين");
    }

    // كلمة مرور قوية: 8 إلى 64 حرف، حروف إنكليزية/أرقام/رموز بس، وبيها حرف واحد ورقم واحد على الأقل
    public static bool IsStrongPassword(string? password)
    {
        if (password == null || password.Length < 8 || password.Length > 64)
            return false;

        bool hasLetter = false;
        bool hasDigit = false;
        foreach (char c in password)
        {
            // نسمح بس بالحروف المطبوعة الإنكليزية (من ! إلى ~) حتى bcrypt يشتغل صح
            if (c < '!' || c > '~')
                return false;
            if (char.IsLetter(c))
                hasLetter = true;
            if (char.IsDigit(c))
                hasDigit = true;
        }

        return hasLetter && hasDigit;
    }

    // يتأكد إن رقم الهاتف والإيميل مو مستخدمين من حساب ثاني
    private async Task CheckUniqueUserFieldsAsync(RegisterViewModel model, ServiceResult result)
    {
        bool phoneExists = await _db.Users.AnyAsync(u => u.PhoneNumber == model.PhoneNumber);
        if (phoneExists)
            result.AddError("PhoneNumber", "رقم الهاتف مسجل من قبل");

        if (model.Email != null)
        {
            bool emailExists = await _db.Users.AnyAsync(u => u.Email == model.Email);
            if (emailExists)
                result.AddError("Email", "الإيميل مسجل من قبل");
        }
    }

    // ================= تحقق السائق والسيارة =================

    // يجمع كل فحوصات السائق: الإجازة، البطاقة، السيارة، الصور، وعدم التكرار
    private async Task ValidateDriverAsync(RegisterViewModel model, ServiceResult result)
    {
        ValidateDriverFields(model, result);
        await ValidateVehicleFieldsAsync(model, result);
        ValidateDriverPhotos(model, result);
        await CheckUniqueDriverFieldsAsync(model, result);
    }

    // يتحقق من رقم الإجازة وتاريخها ورقم البطاقة الوطنية
    private static void ValidateDriverFields(RegisterViewModel model, ServiceResult result)
    {
        if (string.IsNullOrEmpty(model.LicenseNumber) || !Regex.IsMatch(model.LicenseNumber, "^[A-Z0-9-]{3,30}$"))
            result.AddError("LicenseNumber", "رقم الإجازة لازم يكون 3 إلى 30 حرف إنكليزي أو رقم");

        if (model.LicenseExpiryDate == null || model.LicenseExpiryDate.Value.Date <= DateTime.Today)
            result.AddError("LicenseExpiryDate", "الإجازة لازم تكون سارية (تاريخ انتهائها بالمستقبل)");

        if (string.IsNullOrEmpty(model.NationalIdNumber) || !Regex.IsMatch(model.NationalIdNumber, "^[0-9]{8,20}$"))
            result.AddError("NationalIdNumber", "رقم البطاقة الوطنية لازم يكون أرقام فقط (8 إلى 20 رقم)");
    }

    // يتحقق من بيانات السيارة: النوع موجود، المقاعد ضمن الحد، اللوحة، سنة الصنع، تاريخ الفحص
    private async Task ValidateVehicleFieldsAsync(RegisterViewModel model, ServiceResult result)
    {
        await ValidateVehicleTypeAndSeatsAsync(model, result);

        if (string.IsNullOrEmpty(model.PlateNumber) || !Regex.IsMatch(model.PlateNumber, @"^[\p{L}0-9 -]{2,20}$"))
            result.AddError("PlateNumber", "رقم اللوحة لازم يكون حروف وأرقام (2 إلى 20)");

        int maxYear = DateTime.Today.Year + 1;
        if (model.ManufactureYear != null && (model.ManufactureYear < 1980 || model.ManufactureYear > maxYear))
            result.AddError("ManufactureYear", "سنة الصنع لازم تكون بين 1980 و " + maxYear);

        if (model.InspectionExpiryDate == null || model.InspectionExpiryDate.Value.Date <= DateTime.Today)
            result.AddError("InspectionExpiryDate", "فحص السيارة لازم يكون ساري (تاريخ انتهائه بالمستقبل)");

        ValidateOptionalText(model.Brand, "Brand", result);
        ValidateOptionalText(model.VehicleModel, "VehicleModel", result);
        ValidateOptionalText(model.Color, "Color", result);
    }

    // يتأكد إن نوع السيارة موجود وفعّال بالجدول، وعدد المقاعد ما يتجاوز الحد الأعلى لهذا النوع
    private async Task ValidateVehicleTypeAndSeatsAsync(RegisterViewModel model, ServiceResult result)
    {
        Vehicletype? type = null;
        if (model.VehicleTypeId != null)
        {
            type = await _db.Vehicletypes.FirstOrDefaultAsync(t => t.VehicleTypeId == model.VehicleTypeId && t.IsActive == true);
        }

        if (type == null)
        {
            result.AddError("VehicleTypeId", "اختر نوع سيارة صحيح");
            return;
        }

        if (model.SeatCapacity == null || model.SeatCapacity < 1 || model.SeatCapacity > type.MaxSeats)
            result.AddError("SeatCapacity", "عدد المقاعد لازم يكون من 1 إلى " + type.MaxSeats + " لهذا النوع");
    }

    // يتحقق من نص اختياري (الشركة، الموديل، اللون): إذا موجود لازم يكون حروف وأرقام ومسافات بس
    private static void ValidateOptionalText(string? value, string fieldName, ServiceResult result)
    {
        if (!string.IsNullOrEmpty(value) && !Regex.IsMatch(value, @"^[\p{L}0-9 -]{1,40}$"))
            result.AddError(fieldName, "مسموح بس حروف وأرقام ومسافات");
    }

    // يفحص صور السائق: الإجازة والبطاقة والسنوية إجبارية، وصورة السيارة اختيارية
    private void ValidateDriverPhotos(RegisterViewModel model, ServiceResult result)
    {
        AddPhotoError(model.LicensePhoto, "LicensePhoto", result);
        AddPhotoError(model.IdPhoto, "IdPhoto", result);
        AddPhotoError(model.RegistrationPhoto, "RegistrationPhoto", result);

        // صورة السيارة نفحصها بس إذا المستخدم رفعها
        if (model.VehiclePhoto != null)
            AddPhotoError(model.VehiclePhoto, "VehiclePhoto", result);
    }

    // يفحص صورة وحدة ويضيف الخطأ للنتيجة إذا الصورة مرفوضة
    private void AddPhotoError(IFormFile? photo, string fieldName, ServiceResult result)
    {
        string? error = _fileService.ValidateImage(photo);
        if (error != null)
            result.AddError(fieldName, error);
    }

    // يتأكد إن رقم الإجازة ورقم البطاقة ورقم اللوحة مو مسجلين من قبل
    private async Task CheckUniqueDriverFieldsAsync(RegisterViewModel model, ServiceResult result)
    {
        if (await _db.Drivers.AnyAsync(d => d.LicenseNumber == model.LicenseNumber))
            result.AddError("LicenseNumber", "رقم الإجازة مسجل من قبل");

        if (await _db.Drivers.AnyAsync(d => d.NationalIdnumber == model.NationalIdNumber))
            result.AddError("NationalIdNumber", "رقم البطاقة مسجل من قبل");

        if (await _db.Vehicles.AnyAsync(v => v.PlateNumber == model.PlateNumber))
            result.AddError("PlateNumber", "رقم اللوحة مسجل من قبل");
    }

    // ================= الحفظ =================

    // يرجع رقم الدور من جدول Roles حسب اسمه (ما نكتب الأرقام يدوياً بالكود)
    private async Task<int> GetRoleIdAsync(string roleName)
    {
        Role role = await _db.Roles.FirstAsync(r => r.RoleName == roleName);
        return role.RoleId;
    }

    // يبني كائن User جديد، وكلمة المرور تتخزن هاش bcrypt مو نص عادي
    private static User CreateUser(RegisterViewModel model, int roleId)
    {
        User user = new User();
        user.RoleId = roleId;
        user.FullName = model.FullName;
        user.PhoneNumber = model.PhoneNumber;
        user.Email = model.Email;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password, BcryptWorkFactor);
        user.Gender = model.Gender;
        user.AccountStatus = "Active";
        user.CreatedAt = DateTime.Now;
        user.IsDeleted = false;
        return user;
    }

    // يحفظ حساب راكب جديد
    private async Task SavePassengerAsync(RegisterViewModel model)
    {
        int roleId = await GetRoleIdAsync("Passenger");
        User user = CreateUser(model, roleId);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
    }

    // يحفظ السائق: يرفع الصور، ثم يحفظ User + Driver + Vehicle بنفس الـ SaveChanges
    // (SaveChanges وحدة = Transaction وحدة: يا ينحفظ الكل يا ولا شي)
    private async Task SaveDriverAsync(RegisterViewModel model)
    {
        List<string> savedPhotos = new List<string>();
        try
        {
            int roleId = await GetRoleIdAsync("Driver");
            User user = CreateUser(model, roleId);
            Driver driver = await CreateDriverAsync(model, savedPhotos);
            Vehicle vehicle = await CreateVehicleAsync(model, savedPhotos);

            // ربط الكائنات ببعض حتى EF يحفظهم سوية
            driver.Vehicles.Add(vehicle);
            user.DriverUser = driver;
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        catch
        {
            // إذا فشل الحفظ نحذف الصور اللي انرفعت حتى ما تبقى ملفات بدون صاحب
            DeletePhotos(savedPhotos);
            throw;
        }
    }

    // يبني كائن Driver ويرفع صورة الإجازة والبطاقة
    private async Task<Driver> CreateDriverAsync(RegisterViewModel model, List<string> savedPhotos)
    {
        Driver driver = new Driver();
        driver.LicenseNumber = model.LicenseNumber!;
        driver.LicenseExpiryDate = DateOnly.FromDateTime(model.LicenseExpiryDate!.Value);
        driver.NationalIdnumber = model.NationalIdNumber!;
        driver.VerificationStatus = "Pending";

        driver.LicensePhotoUrl = await SavePhotoAsync(model.LicensePhoto!, "licenses", savedPhotos);
        driver.IdphotoUrl = await SavePhotoAsync(model.IdPhoto!, "ids", savedPhotos);
        return driver;
    }

    // يبني كائن Vehicle ويرفع صورة السنوية وصورة السيارة (إذا موجودة)
    private async Task<Vehicle> CreateVehicleAsync(RegisterViewModel model, List<string> savedPhotos)
    {
        Vehicle vehicle = new Vehicle();
        vehicle.VehicleTypeId = model.VehicleTypeId!.Value;
        vehicle.PlateNumber = model.PlateNumber!;
        vehicle.Brand = model.Brand;
        vehicle.Model = model.VehicleModel;
        vehicle.Color = model.Color;
        vehicle.ManufactureYear = model.ManufactureYear;
        vehicle.SeatCapacity = model.SeatCapacity!.Value;
        vehicle.InspectionExpiryDate = DateOnly.FromDateTime(model.InspectionExpiryDate!.Value);
        vehicle.HasAirConditioning = model.HasAirConditioning;
        vehicle.IsActive = true;

        vehicle.RegistrationPhotoUrl = await SavePhotoAsync(model.RegistrationPhoto!, "vehicles", savedPhotos);
        if (model.VehiclePhoto != null)
            vehicle.VehiclePhotoUrl = await SavePhotoAsync(model.VehiclePhoto, "vehicles", savedPhotos);
        return vehicle;
    }

    // يرفع صورة وحدة ويضيف مسارها للقائمة (حتى نكدر نحذفها إذا فشل الحفظ)
    private async Task<string> SavePhotoAsync(IFormFile photo, string folder, List<string> savedPhotos)
    {
        string path = await _fileService.SaveImageAsync(photo, folder);
        savedPhotos.Add(path);
        return path;
    }

    // يحذف قائمة صور من القرص
    private void DeletePhotos(List<string> photos)
    {
        foreach (string path in photos)
        {
            _fileService.DeleteImage(path);
        }
    }

    // ================= تسجيل الدخول =================

    // يتحقق من رقم الهاتف وكلمة المرور، ويرجع بيانات المستخدم إذا صحيحة
    public async Task<LoginResult> LoginAsync(string? phoneNumber, string? password)
    {
        string phone = (phoneNumber ?? "").Trim();
        string pass = password ?? "";

        User? user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.PhoneNumber == phone);

        // إذا الحساب مو موجود أو محذوف نفحص هاش وهمي (حتى الوقت يبقى نفسه) ونرجع نفس الرسالة
        if (user == null || user.IsDeleted)
        {
            BCrypt.Net.BCrypt.Verify(pass, DummyHash);
            return Fail(WrongLoginMessage);
        }

        if (!BCrypt.Net.BCrypt.Verify(pass, user.PasswordHash))
            return Fail(WrongLoginMessage);

        // حالة الحساب نفحصها بعد كلمة المرور حتى ما نكشفها لأي أحد
        if (user.AccountStatus != "Active")
            return Fail("حسابك موقوف، راجع الإدارة");

        await UpdateLastLoginAsync(user);
        return Succeed(user);
    }

    // يحدّث وقت آخر دخول للمستخدم
    private async Task UpdateLastLoginAsync(User user)
    {
        user.LastLoginAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    // يبني نتيجة دخول فاشلة برسالة
    private static LoginResult Fail(string message)
    {
        LoginResult result = new LoginResult();
        result.Success = false;
        result.ErrorMessage = message;
        return result;
    }

    // يبني نتيجة دخول ناجحة ببيانات المستخدم
    private static LoginResult Succeed(User user)
    {
        LoginResult result = new LoginResult();
        result.Success = true;
        result.UserId = user.UserId;
        result.FullName = user.FullName;
        result.RoleName = user.Role.RoleName;
        return result;
    }

    // ================= أدوات للكونترولر =================

    // يبني هوية المستخدم (Claims) اللي تنحفظ بالـ Cookie: رقمه واسمه ودوره
    public static ClaimsPrincipal CreatePrincipal(LoginResult login)
    {
        List<Claim> claims = new List<Claim>();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, login.UserId.ToString()));
        claims.Add(new Claim(ClaimTypes.Name, login.FullName));
        claims.Add(new Claim(ClaimTypes.Role, login.RoleName));

        ClaimsIdentity identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    // يرجع true إذا المستخدم موجود وفعّال ومو محذوف (نفحصه مع كل طلب حتى الحساب الموقوف يطلع فوراً)
    public async Task<bool> IsUserActiveAsync(int userId)
    {
        User? user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null || user.IsDeleted)
            return false;

        return user.AccountStatus == "Active";
    }

    // يرجع أنواع السيارات الفعّالة مرتبة بالاسم (لقائمة الاختيار بفورم التسجيل)
    public async Task<List<Vehicletype>> GetActiveVehicleTypesAsync()
    {
        return await _db.Vehicletypes
            .Where(t => t.IsActive == true)
            .OrderBy(t => t.TypeName)
            .ToListAsync();
    }
}

