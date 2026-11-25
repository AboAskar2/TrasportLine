using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransportLines.ViewModels;

namespace TransportLines.Controllers;

// الكونترولر الرئيسي: يعرض الصفحة الرئيسية وصفحة الخصوصية وصفحة الخطأ
// [AllowAnonymous]: هذي الصفحات عامة، تنفتح بدون تسجيل دخول
[AllowAnonymous]
public class HomeController : Controller
{
    // يعرض الصفحة الرئيسية
    public IActionResult Index()
    {
        return View();
    }

    // يعرض صفحة سياسة الخصوصية
    public IActionResult Privacy()
    {
        return View();
    }

    // يعرض صفحة الخطأ مع رقم الطلب حتى نكدر نتتبع المشكلة
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        // رقم الطلب: ناخذه من الـ Activity إذا موجود، وإلا من HttpContext
        string requestId = HttpContext.TraceIdentifier;
        if (Activity.Current != null)
        {
            requestId = Activity.Current.Id ?? requestId;
        }

        ErrorViewModel model = new ErrorViewModel();
        model.RequestId = requestId;
        return View(model);
    }
}
