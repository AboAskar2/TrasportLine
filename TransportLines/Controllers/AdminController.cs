using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransportLines.Models;
using TransportLines.Security;
using TransportLines.Services;
using TransportLines.ViewModels;

namespace TransportLines.Controllers;

// كونترولر الأدمن: مراجعة السائقين (عرض، موافقة، رفض) وعرض صور وثائقهم
// [Authorize(Roles = "Admin")]: كل الصفحات هنا للأدمن بس، أي أحد ثاني يتحول لصفحة "ما عندك صلاحية"
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AdminService _adminService;
    private readonly FileUploadService _fileService;

    // يستلم خدمة الأدمن وخدمة الصور من الـ DI
    public AdminController(AdminService adminService, FileUploadService fileService)
    {
        _adminService = adminService;
        _fileService = fileService;
    }

    // يعرض قائمة السائقين اللي ينتظرون الموافقة
    [HttpGet]
    public async Task<IActionResult> PendingDrivers()
    {
        List<Driver> drivers = await _adminService.GetPendingDriversAsync();
        return View(drivers);
    }

    // يعرض تفاصيل سائق واحد ووثائقه حتى الأدمن يقرر
    [HttpGet]
    public async Task<IActionResult> DriverDetails(int id)
    {
        Driver? driver = await _adminService.GetDriverDetailsAsync(id);
        if (driver == null)
            return NotFound();

        // فورم الرفض فارغ وجاهز بنفس الصفحة
        ViewBag.RejectForm = new RejectDriverViewModel { DriverId = id };
        return View(driver);
    }

    // يوافق على السائق (رقم الأدمن ناخذه من الـ Cookie مو من الفورم)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveDriver(int id)
    {
        int adminUserId = UserHelper.GetUserId(User);
        ServiceResult result = await _adminService.ApproveDriverAsync(id, adminUserId);

        SetResultMessage(result, "تمت الموافقة على السائق.");
        return RedirectToAction("PendingDrivers");
    }

    // يرفض السائق مع السبب
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDriver(RejectDriverViewModel form)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "اكتب سبب الرفض (من 5 إلى 255 حرف).";
            return RedirectToAction("DriverDetails", new { id = form.DriverId });
        }

        int adminUserId = UserHelper.GetUserId(User);
        ServiceResult result = await _adminService.RejectDriverAsync(form.DriverId, adminUserId, form.Reason);

        SetResultMessage(result, "تم رفض السائق وإرسال السبب له.");
        return RedirectToAction("PendingDrivers");
    }

    // يعرض صورة من وثائق السائق (license أو id أو registration أو vehicle) للأدمن بس
    [HttpGet]
    public async Task<IActionResult> DriverPhoto(int id, string type)
    {
        string? relativePath = await _adminService.GetDriverPhotoPathAsync(id, type);
        string? fullPath = _fileService.GetSafeFullPath(relativePath);
        if (fullPath == null)
            return NotFound();

        // ما نخلي المتصفح يحفظ الصورة بالكاش لأنها وثيقة شخصية
        Response.Headers["Cache-Control"] = "no-store";
        return PhysicalFile(fullPath, FileUploadService.GetContentType(fullPath));
    }

    // يحط رسالة نجاح أو أول رسالة خطأ بالـ TempData حتى تنعرض بالصفحة الجاية
    private void SetResultMessage(ServiceResult result, string successMessage)
    {
        if (result.Success)
            TempData["Success"] = successMessage;
        else
            TempData["Error"] = result.Errors.Values.First();
    }
}
