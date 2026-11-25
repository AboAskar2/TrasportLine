using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.ViewModels;

namespace TransportLines.Services;

// خدمة الخطوط: السائق الموافق عليه ينشئ خط (محطات، أيام، أسعار)، يشوف خطوطه، ويغلق خط
// الخط ثابت بعد الإنشاء: إذا السائق يريد يغير شي، يغلق الخط ويسوي خط جديد
public class LineService
{
    // أقل وأكثر عدد محطات بالخط
    public const int MinStops = 2;
    public const int MaxStops = 20;

    // أعلى سعر شهري مسموح (دينار)
    public const decimal MaxPrice = 10000000;

    private readonly AppDbContext _db;

    // يستلم الداتابيس من الـ DI
    public LineService(AppDbContext db)
    {
        _db = db;
    }

    // ================= بيانات للفورم =================

    // يرجع المحطات الفعّالة (ويا منطقتها) مرتبة بالمنطقة ثم الاسم، لقوائم الاختيار
    public async Task<List<Stop>> GetActiveStopsAsync()
    {
        return await _db.Stops
            .Include(s => s.Area)
            .Where(s => s.IsActive == true && s.Area.IsActive == true)
            .OrderBy(s => s.Area.AreaName)
            .ThenBy(s => s.StopName)
            .ToListAsync();
    }

    // ================= إنشاء خط =================

    // ينشئ خط جديد للسائق الداخل بعد ما يتحقق من كل شي
    public async Task<ServiceResult> CreateLineAsync(int userId, CreateLineViewModel model)
    {
        ServiceResult result = new ServiceResult();
        CleanInput(model);

        // السائق لازم يكون موافق عليه وعنده سيارة فعّالة فحصها ساري
        Driver? driver = await GetApprovedDriverAsync(userId, result);
        Vehicle? vehicle = GetActiveVehicle(driver, result);
        if (driver == null || vehicle == null)
            return result;

        ValidateLineInfo(model, vehicle, result);
        ValidateDays(model, result);
        await ValidateStopsAsync(model, result);
        ValidatePrices(model, result);
        if (!result.Success)
            return result;

        await SaveLineAsync(model, driver, vehicle);
        return result;
    }

    // يشيل المسافات الزايدة، ويحذف صفوف المحطات والأسعار الفارغة (الفورم ممكن يرسل صفوف زايدة فارغة)
    private static void CleanInput(CreateLineViewModel model)
    {
        model.LineName = string.IsNullOrWhiteSpace(model.LineName) ? null : model.LineName.Trim();
        model.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();

        if (model.SelectedDays == null)
            model.SelectedDays = new List<int>();
        if (model.Stops == null)
            model.Stops = new List<LineStopInput>();
        if (model.Prices == null)
            model.Prices = new List<LinePriceInput>();

        model.Stops.RemoveAll(s => s.StopId == null);
        model.Prices.RemoveAll(p => p.FromStopId == null && p.ToStopId == null && p.MonthlyPrice == null);
    }

    // يرجع السائق إذا موافق عليه، وإلا يضيف خطأ ويرجع null
    private async Task<Driver?> GetApprovedDriverAsync(int userId, ServiceResult result)
    {
        Driver? driver = await _db.Drivers
            .Include(d => d.Vehicles)
            .FirstOrDefaultAsync(d => d.UserId == userId);

        if (driver == null || driver.VerificationStatus != "Approved")
        {
            result.AddError("", "لازم الإدارة توافق على حسابك قبل ما تنشر خط");
            return null;
        }

        return driver;
    }

    // يرجع أول سيارة فعّالة فحصها ساري للسائق، وإلا يضيف خطأ ويرجع null
    private static Vehicle? GetActiveVehicle(Driver? driver, ServiceResult result)
    {
        if (driver == null)
            return null;

        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        foreach (Vehicle vehicle in driver.Vehicles)
        {
            if (vehicle.IsActive == true && vehicle.InspectionExpiryDate > today)
                return vehicle;
        }

        result.AddError("", "ما عندك سيارة فعّالة فحصها ساري");
        return null;
    }

    // يتحقق من بيانات الخط الأساسية: الاسم، الوقت، المقاعد، أقل مدة، الملاحظات
    private static void ValidateLineInfo(CreateLineViewModel model, Vehicle vehicle, ServiceResult result)
    {
        if (model.LineName != null && !Regex.IsMatch(model.LineName, @"^[\p{L}0-9 \-]{3,100}$"))
            result.AddError("LineName", "اسم الخط لازم يكون حروف وأرقام (3 إلى 100)");

        if (model.DepartureTime == null || model.DepartureTime.Value < TimeSpan.Zero || model.DepartureTime.Value >= TimeSpan.FromDays(1))
            result.AddError("DepartureTime", "وقت الانطلاق غير صحيح");

        // المقاعد ما تتجاوز مقاعد السيارة (نفس قاعدة الـ Trigger بالداتابيس)
        if (model.TotalSeats == null || model.TotalSeats < 1 || model.TotalSeats > vehicle.SeatCapacity)
            result.AddError("TotalSeats", "عدد المقاعد لازم يكون من 1 إلى " + vehicle.SeatCapacity + " (مقاعد سيارتك)");

        if (model.MinSubscriptionMonths == null || model.MinSubscriptionMonths < 1 || model.MinSubscriptionMonths > 12)
            result.AddError("MinSubscriptionMonths", "أقل مدة اشتراك لازم تكون من 1 إلى 12 شهر");

        if (model.Notes != null && model.Notes.Length > 255)
            result.AddError("Notes", "الملاحظات طويلة جداً (أكثر شي 255 حرف)");
    }

    // يتحقق من الأيام: يوم واحد على الأقل، كل يوم من 0 إلى 6، وبدون تكرار
    private static void ValidateDays(CreateLineViewModel model, ServiceResult result)
    {
        if (model.SelectedDays.Count == 0)
        {
            result.AddError("SelectedDays", "اختر يوم واحد على الأقل");
            return;
        }

        List<int> seenDays = new List<int>();
        foreach (int day in model.SelectedDays)
        {
            if (day < 0 || day > 6 || seenDays.Contains(day))
            {
                result.AddError("SelectedDays", "الأيام المختارة غير صحيحة");
                return;
            }
            seenDays.Add(day);
        }
    }

    // يتحقق من المحطات: العدد، كل محطة موجودة وفعّالة، بدون تكرار، وأوقات الوصول بالترتيب
    private async Task ValidateStopsAsync(CreateLineViewModel model, ServiceResult result)
    {
        if (model.Stops.Count < MinStops || model.Stops.Count > MaxStops)
        {
            result.AddError("Stops", "الخط لازم يكون بيه من " + MinStops + " إلى " + MaxStops + " محطة");
            return;
        }

        // إذا عدد المحطات بدون تكرار أقل من العدد الكلي، معناها أكو محطة مكررة
        List<int> stopIds = GetStopIds(model.Stops);
        if (stopIds.Distinct().Count() != stopIds.Count)
        {
            result.AddError("Stops", "نفس المحطة مكررة بالخط");
            return;
        }

        await CheckStopsExistAsync(stopIds, result);
        ValidateArrivalTimes(model, result);
    }

    // يرجع أرقام المحطات من صفوف الفورم
    private static List<int> GetStopIds(List<LineStopInput> stops)
    {
        List<int> stopIds = new List<int>();
        foreach (LineStopInput stop in stops)
        {
            stopIds.Add(stop.StopId!.Value);
        }
        return stopIds;
    }

    // يتأكد إن كل المحطات موجودة بالجدول وفعّالة
    private async Task CheckStopsExistAsync(List<int> stopIds, ServiceResult result)
    {
        int activeCount = await _db.Stops.CountAsync(s => stopIds.Contains(s.StopId) && s.IsActive == true);
        if (activeCount != stopIds.Count)
            result.AddError("Stops", "وحدة من المحطات غير موجودة أو موقوفة");
    }

    // أوقات الوصول (إذا انكتبت) لازم تكون بعد وقت الانطلاق وكل وقت بعد اللي قبله
    private static void ValidateArrivalTimes(CreateLineViewModel model, ServiceResult result)
    {
        TimeSpan previousTime = model.DepartureTime ?? TimeSpan.Zero;
        foreach (LineStopInput stop in model.Stops)
        {
            if (stop.EstimatedArrivalTime == null)
                continue;

            if (stop.EstimatedArrivalTime.Value < previousTime || stop.EstimatedArrivalTime.Value >= TimeSpan.FromDays(1))
            {
                result.AddError("Stops", "أوقات الوصول لازم تكون بالترتيب وبعد وقت الانطلاق");
                return;
            }
            previousTime = stop.EstimatedArrivalTime.Value;
        }
    }

    // يتحقق من الأسعار: كل سعر بين محطتين من الخط (الصعود قبل النزول)، أكبر من صفر، بدون تكرار،
    // ولازم يكون أكو سعر للخط الكامل (من أول محطة لآخر محطة)
    private static void ValidatePrices(CreateLineViewModel model, ServiceResult result)
    {
        if (model.Stops.Count < MinStops)
            return;

        Dictionary<int, int> stopOrders = BuildStopOrders(model.Stops);
        List<string> seenPairs = new List<string>();
        foreach (LinePriceInput price in model.Prices)
        {
            string? error = CheckOnePrice(price, stopOrders, seenPairs);
            if (error != null)
            {
                result.AddError("Prices", error);
                return;
            }
        }

        CheckFullRoutePrice(model, result);
    }

    // يبني جدول: رقم المحطة ← ترتيبها بالخط (1، 2، 3...)
    private static Dictionary<int, int> BuildStopOrders(List<LineStopInput> stops)
    {
        Dictionary<int, int> stopOrders = new Dictionary<int, int>();
        for (int i = 0; i < stops.Count; i++)
        {
            stopOrders[stops[i].StopId!.Value] = i + 1;
        }
        return stopOrders;
    }

    // يفحص سعر واحد ويرجع رسالة الخطأ، أو null إذا السعر سليم
    private static string? CheckOnePrice(LinePriceInput price, Dictionary<int, int> stopOrders, List<string> seenPairs)
    {
        if (price.FromStopId == null || price.ToStopId == null
            || !stopOrders.ContainsKey(price.FromStopId.Value) || !stopOrders.ContainsKey(price.ToStopId.Value))
            return "كل سعر لازم يكون بين محطتين من محطات الخط";

        if (stopOrders[price.FromStopId.Value] >= stopOrders[price.ToStopId.Value])
            return "محطة النزول لازم تكون بعد محطة الصعود";

        // DECIMAL(10,0) بالداتابيس يعني رقم صحيح بدون كسور
        if (price.MonthlyPrice == null || price.MonthlyPrice <= 0 || price.MonthlyPrice > MaxPrice || price.MonthlyPrice % 1 != 0)
            return "السعر لازم يكون رقم صحيح أكبر من صفر";

        string pairKey = price.FromStopId + "-" + price.ToStopId;
        if (seenPairs.Contains(pairKey))
            return "نفس زوج المحطات مكرر بالأسعار";

        seenPairs.Add(pairKey);
        return null;
    }

    // يتأكد إن أكو سعر من أول محطة لآخر محطة (حتى الخط الكامل له سعر دائماً)
    private static void CheckFullRoutePrice(CreateLineViewModel model, ServiceResult result)
    {
        int firstStopId = model.Stops[0].StopId!.Value;
        int lastStopId = model.Stops[model.Stops.Count - 1].StopId!.Value;

        foreach (LinePriceInput price in model.Prices)
        {
            if (price.FromStopId == firstStopId && price.ToStopId == lastStopId)
                return;
        }

        result.AddError("Prices", "لازم تحدد سعر للخط الكامل (من أول محطة لآخر محطة)");
    }

    // ================= حفظ الخط =================

    // يحفظ الخط ويا محطاته وأيامه وأسعاره بنفس الـ SaveChanges (Transaction وحدة: يا الكل يا ولا شي)
    private async Task SaveLineAsync(CreateLineViewModel model, Driver driver, Vehicle vehicle)
    {
        Line line = BuildLine(model, driver, vehicle);
        Dictionary<int, Linestop> lineStops = AddStops(line, model.Stops);
        AddDays(line, model.SelectedDays);
        AddPrices(line, model.Prices, lineStops);

        _db.Lines.Add(line);
        await _db.SaveChangesAsync();
    }

    // يبني كائن Line من الفورم، حالته Open
    private static Line BuildLine(CreateLineViewModel model, Driver driver, Vehicle vehicle)
    {
        Line line = new Line();
        line.DriverId = driver.DriverId;
        line.VehicleId = vehicle.VehicleId;
        line.LineName = model.LineName;
        line.DepartureTime = TimeOnly.FromTimeSpan(model.DepartureTime!.Value);
        line.TotalSeats = model.TotalSeats!.Value;
        line.MinSubscriptionMonths = model.MinSubscriptionMonths!.Value;
        line.Notes = model.Notes;
        line.IsWomenOnly = model.IsWomenOnly;
        line.LineStatus = "Open";
        line.CreatedAt = DateTime.Now;
        return line;
    }

    // يضيف المحطات للخط بالترتيب (1، 2، 3...) ويرجع جدول: رقم المحطة ← صف المحطة بالخط
    private static Dictionary<int, Linestop> AddStops(Line line, List<LineStopInput> stops)
    {
        Dictionary<int, Linestop> lineStops = new Dictionary<int, Linestop>();
        for (int i = 0; i < stops.Count; i++)
        {
            Linestop lineStop = new Linestop();
            lineStop.StopId = stops[i].StopId!.Value;
            lineStop.StopOrder = i + 1;
            if (stops[i].EstimatedArrivalTime != null)
                lineStop.EstimatedArrivalTime = TimeOnly.FromTimeSpan(stops[i].EstimatedArrivalTime!.Value);

            line.Linestops.Add(lineStop);
            lineStops[lineStop.StopId] = lineStop;
        }
        return lineStops;
    }

    // يضيف أيام الخط
    private static void AddDays(Line line, List<int> days)
    {
        foreach (int day in days)
        {
            Lineday lineDay = new Lineday();
            lineDay.DayOfWeek = (sbyte)day;
            line.Linedays.Add(lineDay);
        }
    }

    // يضيف الأسعار ويربط كل سعر بصف محطة الصعود والنزول (حتى EF يعرف يحفظها بعد المحطات)
    private static void AddPrices(Line line, List<LinePriceInput> prices, Dictionary<int, Linestop> lineStops)
    {
        foreach (LinePriceInput input in prices)
        {
            Lineprice price = new Lineprice();
            price.FromStopId = input.FromStopId!.Value;
            price.ToStopId = input.ToStopId!.Value;
            price.MonthlyPrice = input.MonthlyPrice!.Value;
            price.Linestop = lineStops[price.FromStopId];
            price.LinestopNavigation = lineStops[price.ToStopId];
            line.Lineprices.Add(price);
        }
    }

    // ================= خطوط السائق =================

    // يرجع كل خطوط السائق الداخل، الأحدث أولاً
    public async Task<List<Line>> GetDriverLinesAsync(int userId)
    {
        return await _db.Lines
            .Where(l => l.Driver.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
    }

    // يرجع خط واحد بكل تفاصيله، بس إذا الخط يخص هذا السائق (وإلا null)
    public async Task<Line?> GetDriverLineAsync(int userId, int lineId)
    {
        return await _db.Lines
            .Include(l => l.Linestops).ThenInclude(ls => ls.Stop)
            .Include(l => l.Linedays)
            .Include(l => l.Lineprices)
            .Include(l => l.Vehicle)
            .FirstOrDefaultAsync(l => l.LineId == lineId && l.Driver.UserId == userId);
    }

    // ================= إغلاق خط =================

    // يغلق خط السائق: الطلبات المعلقة تنرفض تلقائياً، وكل المشتركين ياخذون إشعار
    // الاشتراكات الفعّالة تبقى لحد ما تخلص مدتها
    public async Task<ServiceResult> CloseLineAsync(int userId, int lineId)
    {
        ServiceResult result = new ServiceResult();
        Line? line = await _db.Lines.FirstOrDefaultAsync(l => l.LineId == lineId && l.Driver.UserId == userId);
        if (line == null)
        {
            result.AddError("", "الخط غير موجود");
            return result;
        }

        if (line.LineStatus == "Closed")
        {
            result.AddError("", "الخط مغلق من قبل");
            return result;
        }

        line.LineStatus = "Closed";
        await HandleOpenSubscriptionsAsync(line);
        await _db.SaveChangesAsync();
        return result;
    }

    // يرفض الطلبات المعلقة ويرسل إشعار لكل مشترك عنده طلب معلق أو اشتراك فعّال
    private async Task HandleOpenSubscriptionsAsync(Line line)
    {
        List<Subscription> openSubscriptions = await _db.Subscriptions
            .Where(s => s.LineId == line.LineId && (s.SubscriptionStatus == "Pending" || s.SubscriptionStatus == "Active"))
            .ToListAsync();

        foreach (Subscription subscription in openSubscriptions)
        {
            if (subscription.SubscriptionStatus == "Pending")
            {
                subscription.SubscriptionStatus = "Rejected";
                subscription.RejectionReason = "السائق أغلق الخط";
            }
            AddLineClosedNotification(subscription.PassengerId, line.LineId);
        }
    }

    // يضيف إشعار "الخط انغلق" لمشترك
    private void AddLineClosedNotification(int passengerId, int lineId)
    {
        Notification notification = new Notification();
        notification.UserId = passengerId;
        notification.RelatedLineId = lineId;
        notification.Type = "LineClosed";
        notification.Title = "تم إغلاق الخط";
        notification.Message = "السائق أغلق الخط. الطلبات المعلقة انرفضت، والاشتراكات الفعّالة تبقى لحد نهاية مدتها.";
        notification.IsRead = false;
        notification.CreatedAt = DateTime.Now;
        _db.Notifications.Add(notification);
    }
}
