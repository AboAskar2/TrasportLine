using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TransportLines.Models;
using TransportLines.Security;
using TransportLines.Services;
using TransportLines.ViewModels;

namespace TransportLines.Controllers;

// كونترولر السائق: ملفه الشخصي، وخطوطه (إنشاء، عرض، إغلاق)
// [Authorize(Roles = "Driver")]: الصفحات هنا للسائق بس
// كل الدوال تاخذ رقم السائق من الـ Cookie مو من الرابط، حتى محد يشوف أو يعدل بيانات غيره
[Authorize(Roles = "Driver")]
public class DriverController : Controller
{
    private readonly DriverService _driverService;
    private readonly LineService _lineService;
    private readonly SubscriptionApprovalService _approvalService;

    // يستلم خدمة السائق وخدمة الخطوط وخدمة الموافقة على الطلبات من الـ DI
    public DriverController(DriverService driverService, LineService lineService, SubscriptionApprovalService approvalService)
    {
        _driverService = driverService;
        _lineService = lineService;
        _approvalService = approvalService;
    }

    // ================= الملف الشخصي =================

    // يعرض ملف السائق الداخل: بياناته وسيارته وحالة الموافقة
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        int userId = UserHelper.GetUserId(User);
        Driver? driver = await _driverService.GetDriverProfileAsync(userId);
        if (driver == null)
            return NotFound();

        ViewBag.StatusMessage = DriverService.GetStatusMessage(driver);
        return View(driver);
    }

    // ================= الخطوط =================

    // يعرض قائمة خطوط السائق
    [HttpGet]
    public async Task<IActionResult> MyLines()
    {
        int userId = UserHelper.GetUserId(User);
        List<Line> lines = await _lineService.GetDriverLinesAsync(userId);
        return View(lines);
    }

    // يعرض تفاصيل خط واحد من خطوط السائق (إذا الخط مو إله يطلع 404)
    [HttpGet]
    public async Task<IActionResult> LineDetails(int id)
    {
        int userId = UserHelper.GetUserId(User);
        Line? line = await _lineService.GetDriverLineAsync(userId, id);
        if (line == null)
            return NotFound();

        return View(line);
    }

    // يعرض فورم إنشاء خط (بس للسائق الموافق عليه)
    [HttpGet]
    public async Task<IActionResult> CreateLine()
    {
        int userId = UserHelper.GetUserId(User);
        if (!await _driverService.IsApprovedDriverAsync(userId))
        {
            TempData["Error"] = "لازم الإدارة توافق على حسابك قبل ما تنشر خط.";
            return RedirectToAction("Profile");
        }

        await LoadStopsAsync();
        return View(new CreateLineViewModel());
    }

    // يستلم فورم إنشاء الخط ويحفظه إذا كل شي صحيح
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLine(CreateLineViewModel form)
    {
        if (ModelState.IsValid)
        {
            int userId = UserHelper.GetUserId(User);
            ServiceResult result = await _lineService.CreateLineAsync(userId, form);
            if (result.Success)
            {
                TempData["Success"] = "تم نشر الخط بنجاح.";
                return RedirectToAction("MyLines");
            }
            AddErrorsToModelState(result);
        }

        // في أخطاء: نرجع نفس الفورم ويا بيانات السائق
        await LoadStopsAsync();
        return View(form);
    }

    // يغلق خط السائق (POST فقط، والـ View يطلب تأكيد قبل الإرسال)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseLine(int id)
    {
        int userId = UserHelper.GetUserId(User);
        ServiceResult result = await _lineService.CloseLineAsync(userId, id);

        if (result.Success)
            TempData["Success"] = "تم إغلاق الخط وإبلاغ المشتركين.";
        else
            TempData["Error"] = result.Errors.Values.First();

        return RedirectToAction("MyLines");
    }

    // ================= طلبات الاشتراك =================

    // يعرض كل طلبات واشتراكات خط من خطوط السائق (إذا الخط مو إله يطلع 404)
    [HttpGet]
    public async Task<IActionResult> LineRequests(int id)
    {
        int userId = UserHelper.GetUserId(User);
        Line? line = await _lineService.GetDriverLineAsync(userId, id);
        if (line == null)
            return NotFound();

        ViewBag.Line = line;
        List<Subscription> subscriptions = await _approvalService.GetLineSubscriptionsAsync(userId, id);
        return View(subscriptions);
    }

    // يوافق على طلب اشتراك (الخدمة تفحص المقاعد لكل مقطع داخل Transaction)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRequest(int id, int lineId)
    {
        int userId = UserHelper.GetUserId(User);
        ServiceResult result = await _approvalService.ApproveAsync(userId, id);

        SetResultMessage(result, "تمت الموافقة على الطلب.");
        return RedirectToAction("LineRequests", new { id = lineId });
    }

    // يرفض طلب اشتراك مع السبب (الـ View يطلب تأكيد قبل الإرسال)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRequest(RejectRequestViewModel form)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "اكتب سبب الرفض (من 5 إلى 255 حرف).";
            return RedirectToAction("LineRequests", new { id = form.LineId });
        }

        int userId = UserHelper.GetUserId(User);
        ServiceResult result = await _approvalService.RejectAsync(userId, form.SubscriptionId, form.Reason);

        SetResultMessage(result, "تم رفض الطلب وإبلاغ الراكب.");
        return RedirectToAction("LineRequests", new { id = form.LineId });
    }

    // ================= أدوات مساعدة =================

    // يحط رسالة نجاح أو أول رسالة خطأ بالـ TempData حتى تنعرض بالصفحة الجاية
    private void SetResultMessage(ServiceResult result, string successMessage)
    {
        if (result.Success)
            TempData["Success"] = successMessage;
        else
            TempData["Error"] = result.Errors.Values.First();
    }

    // يجهز قائمة المحطات (اسم المنطقة - اسم المحطة) للـ dropdown بالفورم
    private async Task LoadStopsAsync()
    {
        List<Stop> stops = await _lineService.GetActiveStopsAsync();
        List<SelectListItem> items = new List<SelectListItem>();
        foreach (Stop stop in stops)
        {
            items.Add(new SelectListItem(stop.Area.AreaName + " - " + stop.StopName, stop.StopId.ToString()));
        }
        ViewBag.Stops = items;
    }

    // ينقل أخطاء الخدمة للـ ModelState حتى تنعرض بالفورم
    private void AddErrorsToModelState(ServiceResult result)
    {
        foreach (KeyValuePair<string, string> error in result.Errors)
        {
            ModelState.AddModelError(error.Key, error.Value);
        }
    }
}
