// ملف بداية البرنامج: يجهز الخدمات (الداتابيس، الأمان، الـ MVC) ويشغّل الموقع
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Security;
using TransportLines.Services;

var builder = WebApplication.CreateBuilder(args);

// تفعيل الـ MVC، وكل فورم POST لازم يحمل Anti-Forgery Token
builder.Services.AddControllersWithViews(SecurityHelper.ConfigureMvc);

// قراءة سلسلة الاتصال من user-secrets (كلمة المرور مو مكتوبة بالكود)
string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("سلسلة الاتصال DefaultConnection غير موجودة. شغّل dotnet user-secrets set.");
}

// نسخة MySQL مكتوبة يدوياً حتى البرنامج ما يفتح اتصال إضافي بس حتى يعرفها
MySqlServerVersion serverVersion = new MySqlServerVersion(new Version(8, 0, 45));

// تسجيل AppDbContext حتى الـ Controllers والـ Services تستلمه جاهز
builder.Services.AddDbContext<AppDbContext>(options => options.UseMySql(connectionString, serverVersion));

// مجلد رفع الصور: App_Data/uploads خارج wwwroot، حتى الصور الحساسة (الإجازة، البطاقة) ما تنفتح برابط مباشر
string uploadFolder = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "uploads");
builder.Services.AddSingleton(new FileUploadService(uploadFolder));

// خدمة الحسابات (تسجيل ودخول): نسخة جديدة لكل طلب لأنها تستخدم AppDbContext
builder.Services.AddScoped<AccountService>();

// خدمة الأدمن (مراجعة السائقين) وخدمة السائق (ملفه الشخصي)
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<DriverService>();

// خدمة الخطوط (إنشاء خط، عرض خطوط السائق، إغلاق خط)
builder.Services.AddScoped<LineService>();

// خدمة الاشتراكات (بحث، طلب اشتراك، اشتراكاتي، إلغاء)
builder.Services.AddScoped<SubscriptionService>();

// خدمة موافقة السائق على طلبات الاشتراك (مع فحص المقاعد لكل مقطع)
builder.Services.AddScoped<SubscriptionApprovalService>();

// تسجيل الدخول بالـ Cookie (الإعدادات بـ SecurityHelper.ConfigureCookie)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(SecurityHelper.ConfigureCookie);

// الصلاحيات: كل صفحة تحتاج دخول إلا اللي عليها [AllowAnonymous]
builder.Services.AddAuthorization(SecurityHelper.ConfigureAuthorization);

// تحديد عدد محاولات الدخول والتسجيل لكل IP (ضد تخمين كلمات المرور)
builder.Services.AddRateLimiter(SecurityHelper.ConfigureRateLimiter);

var app = builder.Build();

// خارج بيئة التطوير: نعرض صفحة خطأ عامة بدل تفاصيل الخطأ، ونفعّل HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// رؤوس الحماية لكل رد (ضد XSS و Clickjacking)
app.Use(SecurityHelper.AddSecurityHeaders);

// تحويل أي طلب http إلى https
app.UseHttpsRedirection();

// السماح بقراءة ملفات wwwroot (css, js, صور الموقع)
app.UseStaticFiles();

// التوجيه، ثم تحديد المحاولات، ثم معرفة المستخدم (Authentication)، ثم فحص صلاحياته (Authorization)
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// شكل الرابط الافتراضي: /Controller/Action/id
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// تشغيل الموقع
app.Run();
