using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using TransportLines.Models;
using TransportLines.Security;
using TransportLines.Services;
using TransportLines.ViewModels;

namespace TransportLines.Controllers;

// كونترولر الحسابات: التسجيل، تسجيل الدخول، تسجيل الخروج، وصفحة "ما عندك صلاحية"
// الكونترولر خفيف: يستلم الفورم ويرسله لـ AccountService، والمنطق كله بالخدمة
public class AccountController : Controller
{
    // أقصى حجم لطلب التسجيل: 4 صور × 2 ميگا + هامش للحقول = 10 ميگا
    private const long MaxRegisterRequestSize = 10 * 1024 * 1024;

    private readonly AccountService _accountService;

    // يستلم خدمة الحسابات من الـ DI
    public AccountController(AccountService accountService)
    {
        _accountService = accountService;
    }

    // ================= التسجيل =================

    // يعرض فورم التسجيل (فارغ)
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Register()
    {
        // إذا المستخدم داخل أصلاً ما يحتاج يسجل
        if (User.Identity != null && User.Identity.IsAuthenticated)
            return RedirectToAction("Index", "Home");

        await LoadVehicleTypesAsync();
        return View(new RegisterViewModel());
    }

    // يستلم فورم التسجيل ويحفظ الحساب إذا كل الحقول صحيحة
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityHelper.RegisterPolicy)]
    [RequestSizeLimit(MaxRegisterRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRegisterRequestSize)]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        // الطبقة الأولى: Data Annotations. والثانية (الأقوى) جوة RegisterAsync
        if (ModelState.IsValid)
        {
            ServiceResult result = await _accountService.RegisterAsync(model);
            if (result.Success)
                return RedirectAfterRegister(model.AccountType);

            AddErrorsToModelState(result);
        }

        // في أخطاء: نرجع نفس الفورم ويا بيانات المستخدم (بدون كلمات المرور)
        model.Password = "";
        model.ConfirmPassword = "";
        await LoadVehicleTypesAsync();
        return View(model);
    }

    // بعد التسجيل الناجح: رسالة نجاح ونحوله لصفحة الدخول
    private IActionResult RedirectAfterRegister(string accountType)
    {
        if (accountType == "Driver")
            TempData["Success"] = "تم إنشاء حسابك. حساب السائق ينتظر موافقة الإدارة قبل نشر الخطوط.";
        else
            TempData["Success"] = "تم إنشاء حسابك بنجاح، سجّل دخولك.";

        return RedirectToAction("Login");
    }

    // ================= تسجيل الدخول =================

    // يعرض فورم الدخول، و returnUrl هي الصفحة اللي يرجع لها بعد الدخول
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    // يستلم فورم الدخول: إذا البيانات صحيحة يسوي Cookie ويحوله
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityHelper.LoginPolicy)]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(model);

        LoginResult result = await _accountService.LoginAsync(model.PhoneNumber, model.Password);
        if (!result.Success)
        {
            ModelState.AddModelError("", result.ErrorMessage);
            model.Password = "";
            return View(model);
        }

        await SignInUserAsync(result, model.RememberMe);
        return RedirectToLocal(returnUrl);
    }

    // يسوي الـ Cookie للمستخدم، وإذا اختار "تذكرني" تبقى بعد غلق المتصفح
    private async Task SignInUserAsync(LoginResult result, bool rememberMe)
    {
        AuthenticationProperties properties = new AuthenticationProperties();
        properties.IsPersistent = rememberMe;

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AccountService.CreatePrincipal(result),
            properties);
    }

    // يحوله للرابط المطلوب بس إذا هو رابط داخل موقعنا (ضد Open Redirect لمواقع خبيثة)
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    // ================= تسجيل الخروج =================

    // يمسح الـ Cookie ويرجعه للصفحة الرئيسية (POST فقط حتى محد يطلعك برابط خبيث)
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "تم تسجيل الخروج.";
        return RedirectToAction("Index", "Home");
    }

    // ================= ما عندك صلاحية =================

    // تنعرض إذا المستخدم حاول يفتح صفحة مو إله (مثلاً راكب يفتح صفحة الأدمن)
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // ================= أدوات مساعدة =================

    // ينقل أخطاء الخدمة للـ ModelState حتى كل خطأ ينعرض جنب حقله بالفورم
    private void AddErrorsToModelState(ServiceResult result)
    {
        foreach (KeyValuePair<string, string> error in result.Errors)
        {
            ModelState.AddModelError(error.Key, error.Value);
        }
    }

    // يجهز قائمة أنواع السيارات من الداتابيس للـ dropdown بالفورم
    private async Task LoadVehicleTypesAsync()
    {
        List<Vehicletype> types = await _accountService.GetActiveVehicleTypesAsync();
        ViewBag.VehicleTypes = new SelectList(types, "VehicleTypeId", "TypeName");
    }
}
