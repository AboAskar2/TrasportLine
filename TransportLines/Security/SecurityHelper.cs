using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TransportLines.Services;

namespace TransportLines.Security;

// أدوات الأمان العامة للموقع: تحديد عدد المحاولات، رؤوس الحماية، وفحص الـ Cookie مع كل طلب
// كلها دوال بسيطة نربطها بـ Program.cs
public static class SecurityHelper
{
    // اسم سياسة تحديد محاولات الدخول
    public const string LoginPolicy = "login";

    // اسم سياسة تحديد محاولات التسجيل
    public const string RegisterPolicy = "register";

    // ================= إعدادات تنربط بـ Program.cs =================

    // إعدادات الـ Cookie مال تسجيل الدخول
    public static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        // الصفحات اللي يتحول لها المستخدم إذا مو داخل أو ما عنده صلاحية
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";

        // الـ Cookie تنتهي بعد 8 ساعات بدون نشاط
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // HttpOnly: الجافاسكربت ما يكدر يقراها (ضد سرقتها بـ XSS)
        // Secure: تنرسل بس على https. SameSite Strict: ما تنرسل من مواقع ثانية (ضد CSRF)
        options.Cookie.Name = "TransportLines.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;

        // نفحص حالة الحساب مع كل طلب
        options.Events.OnValidatePrincipal = ValidateCookieAsync;
    }

    // إعدادات تحديد عدد المحاولات: سياسة للدخول وسياسة للتسجيل
    public static void ConfigureRateLimiter(RateLimiterOptions options)
    {
        // 429 = طلبات كثيرة
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy<string>(LoginPolicy, GetLoginPartition);
        options.AddPolicy<string>(RegisterPolicy, GetRegisterPartition);
    }

    // كل صفحة تحتاج تسجيل دخول إلا اللي عليها [AllowAnonymous] (أأمن من إننا ننسى [Authorize])
    public static void ConfigureAuthorization(AuthorizationOptions options)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    }

    // كل فورم POST لازم يجي ويا Anti-Forgery Token (ضد CSRF) حتى لو نسينا [ValidateAntiForgeryToken]
    public static void ConfigureMvc(MvcOptions options)
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    }

    // ================= تحديد عدد المحاولات (ضد تخمين كلمات المرور) =================

    // يقسم محاولات الدخول حسب IP المستخدم: كل IP عنده حد خاص بيه
    public static RateLimitPartition<string> GetLoginPartition(HttpContext context)
    {
        string ip = GetClientIp(context);
        return RateLimitPartition.GetFixedWindowLimiter(ip, CreateLoginLimitOptions);
    }

    // يقسم محاولات التسجيل حسب IP المستخدم
    public static RateLimitPartition<string> GetRegisterPartition(HttpContext context)
    {
        string ip = GetClientIp(context);
        return RateLimitPartition.GetFixedWindowLimiter(ip, CreateRegisterLimitOptions);
    }

    // الدخول: 5 محاولات بالدقيقة لكل IP، وبعدها يرفض لحد ما تخلص الدقيقة
    private static FixedWindowRateLimiterOptions CreateLoginLimitOptions(string ip)
    {
        FixedWindowRateLimiterOptions options = new FixedWindowRateLimiterOptions();
        options.PermitLimit = 5;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueLimit = 0;
        return options;
    }

    // التسجيل: 5 حسابات كل 10 دقائق لكل IP (ضد إنشاء حسابات وهمية بالجملة)
    private static FixedWindowRateLimiterOptions CreateRegisterLimitOptions(string ip)
    {
        FixedWindowRateLimiterOptions options = new FixedWindowRateLimiterOptions();
        options.PermitLimit = 5;
        options.Window = TimeSpan.FromMinutes(10);
        options.QueueLimit = 0;
        return options;
    }

    // يرجع IP المستخدم كنص (أو "unknown" إذا ما معروف)
    private static string GetClientIp(HttpContext context)
    {
        if (context.Connection.RemoteIpAddress == null)
            return "unknown";

        return context.Connection.RemoteIpAddress.ToString();
    }

    // ================= رؤوس الحماية (Security Headers) =================

    // يضيف رؤوس حماية لكل رد من السيرفر، ثم يكمل للخطوة الجاية
    public static async Task AddSecurityHeaders(HttpContext context, RequestDelegate next)
    {
        // يمنع المتصفح يخمن نوع الملف (مثلاً يشغل صورة كسكربت)
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // يمنع عرض موقعنا داخل iframe بموقع ثاني (ضد Clickjacking)
        context.Response.Headers["X-Frame-Options"] = "DENY";

        // ما يرسل الرابط الكامل للمواقع الثانية
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // يسمح بالسكربتات والملفات من موقعنا بس (ضد XSS)
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; " +
            "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

        await next(context);
    }

    // ================= فحص الـ Cookie مع كل طلب =================

    // مع كل طلب: إذا الحساب انحذف أو انوقف بعد ما دخل، نطلعه فوراً بدل ما ينتظر انتهاء الـ Cookie
    public static async Task ValidateCookieAsync(CookieValidatePrincipalContext context)
    {
        string? userIdText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdText, out int userId))
        {
            await RejectAsync(context);
            return;
        }

        // ناخذ AccountService من الـ DI ونفحص حالة الحساب بالداتابيس
        AccountService accountService = context.HttpContext.RequestServices.GetRequiredService<AccountService>();
        bool isActive = await accountService.IsUserActiveAsync(userId);
        if (!isActive)
        {
            await RejectAsync(context);
        }
    }

    // يرفض الـ Cookie ويسوي تسجيل خروج
    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
