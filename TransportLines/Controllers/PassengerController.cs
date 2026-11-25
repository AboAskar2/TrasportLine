using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TransportLines.Models;
using TransportLines.Security;
using TransportLines.Services;
using TransportLines.ViewModels;

namespace TransportLines.Controllers;

// كونترولر الراكب: البحث عن خطوط، تفاصيل الخط، طلب اشتراك، اشتراكاتي، وإلغاء اشتراك
// [Authorize(Roles = "Passenger")]: الصفحات هنا للراكب بس
// رقم الراكب ياخذه من الـ Cookie مو من الرابط، حتى محد يشوف أو يلغي اشتراك غيره
[Authorize(Roles = "Passenger")]
public class PassengerController : Controller
{
    private readonly SubscriptionService _subscriptionService;
    private readonly LineService _lineService;

    // يستلم خدمة الاشتراكات وخدمة الخطوط (لقائمة المحطات) من الـ DI
    public PassengerController(SubscriptionService subscriptionService, LineService lineService)
    {
        _subscriptionService = subscriptionService;
        _lineService = lineService;
    }

    // ================= البحث =================

    // يعرض فورم البحث والنتائج (إذا اختار محطتين يطلع بس الخطوط اللي تمر بيهم)
    [HttpGet]
    public async Task<IActionResult> SearchLines(int? fromStopId, int? toStopId)
    {
        int userId = UserHelper.GetUserId(User);
        List<Line> lines = await _subscriptionService.SearchLinesAsync(userId, fromStopId, toStopId);

        ViewBag.FromStopId = fromStopId;
        ViewBag.ToStopId = toStopId;
        await LoadAllStopsAsync();
        return View(lines);
    }

    // يعرض تفاصيل خط مفتوح (محطات، أيام، أسعار، السائق والسيارة)
    [HttpGet]
    public async Task<IActionResult> LineDetails(int id)
    {
        int userId = UserHelper.GetUserId(User);
        Line? line = await _subscriptionService.GetOpenLineAsync(userId, id);
        if (line == null)
            return NotFound();

        return View(line);
    }

    // ================= طلب اشتراك =================

    // يعرض فورم طلب الاشتراك على خط معين
    [HttpGet]
    public async Task<IActionResult> RequestSubscription(int lineId)
    {
        int userId = UserHelper.GetUserId(User);
        Line? line = await _subscriptionService.GetOpenLineAsync(userId, lineId);
        if (line == null)
            return NotFound();

        LoadLineStops(line);
        RequestSubscriptionViewModel form = new RequestSubscriptionViewModel();
        form.LineId = lineId;
        form.StartDate = DateTime.Today;
        form.Months = line.MinSubscriptionMonths;
        return View(form);
    }

    // يستلم فورم الطلب ويحفظه كطلب Pending ينتظر موافقة السائق
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestSubscription(RequestSubscriptionViewModel form)
    {
        int userId = UserHelper.GetUserId(User);
        if (ModelState.IsValid)
        {
            ServiceResult result = await _subscriptionService.RequestSubscriptionAsync(userId, form);
            if (result.Success)
            {
                TempData["Success"] = "تم إرسال طلب الاشتراك، ينتظر موافقة السائق.";
                return RedirectToAction("MySubscriptions");
            }
            AddErrorsToModelState(result);
        }

        // في أخطاء: نرجع نفس الفورم (إذا الخط بعده مفتوح)
        Line? line = await _subscriptionService.GetOpenLineAsync(userId, form.LineId);
        if (line == null)
            return NotFound();

        LoadLineStops(line);
        return View(form);
    }

    // ================= اشتراكاتي =================

    // يعرض كل اشتراكات الراكب وحالتها
    [HttpGet]
    public async Task<IActionResult> MySubscriptions()
    {
        int userId = UserHelper.GetUserId(User);
        List<Subscription> subscriptions = await _subscriptionService.GetMySubscriptionsAsync(userId);
        return View(subscriptions);
    }

    // يلغي طلب أو اشتراك (POST فقط، والـ View يطلب تأكيد قبل الإرسال)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSubscription(int id, string? reason)
    {
        int userId = UserHelper.GetUserId(User);
        ServiceResult result = await _subscriptionService.CancelSubscriptionAsync(userId, id, reason);

        if (result.Success)
            TempData["Success"] = "تم إلغاء الاشتراك.";
        else
            TempData["Error"] = result.Errors.Values.First();

        return RedirectToAction("MySubscriptions");
    }

    // ================= أدوات مساعدة =================

    // يجهز كل المحطات الفعّالة لقوائم البحث (من - إلى)
    private async Task LoadAllStopsAsync()
    {
        List<Stop> stops = await _lineService.GetActiveStopsAsync();
        List<SelectListItem> items = new List<SelectListItem>();
        foreach (Stop stop in stops)
        {
            items.Add(new SelectListItem(stop.Area.AreaName + " - " + stop.StopName, stop.StopId.ToString()));
        }
        ViewBag.Stops = items;
    }

    // يجهز محطات هذا الخط بس (بالترتيب) لقوائم الصعود والنزول، ويحط الخط بالـ ViewBag للعرض
    private void LoadLineStops(Line line)
    {
        List<SelectListItem> items = new List<SelectListItem>();
        foreach (Linestop lineStop in line.Linestops.OrderBy(ls => ls.StopOrder))
        {
            items.Add(new SelectListItem(lineStop.StopOrder + ". " + lineStop.Stop.StopName, lineStop.StopId.ToString()));
        }
        ViewBag.LineStops = items;
        ViewBag.Line = line;
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
