using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.ViewModels;

namespace TransportLines.Services;

// خدمة الاشتراكات (جهة الراكب): البحث عن خطوط، طلب اشتراك (Pending)، عرض اشتراكاتي، وإلغاء اشتراك
// الطلب Pending ما يحجز مقاعد؛ المقاعد تنفحص لما السائق يوافق (خطوة جاية)
public class SubscriptionService
{
    // أبعد تاريخ بداية مسموح من اليوم (بالأيام)
    public const int MaxDaysBeforeStart = 60;

    private readonly AppDbContext _db;

    // يستلم الداتابيس من الـ DI
    public SubscriptionService(AppDbContext db)
    {
        _db = db;
    }

    // ================= البحث =================

    // يرجع الخطوط المفتوحة لسائقين موافق عليهم، وإذا انطت محطتين يرجع بس الخطوط اللي تمر بيهم بالترتيب وإلها سعر
    // الخطوط النسائية تظهر بس للراكبات
    public async Task<List<Line>> SearchLinesAsync(int userId, int? fromStopId, int? toStopId)
    {
        bool isFemale = await IsFemaleAsync(userId);
        List<Line> openLines = await GetOpenLinesAsync(isFemale);
        if (fromStopId == null || toStopId == null)
            return openLines;

        List<Line> matchingLines = new List<Line>();
        foreach (Line line in openLines)
        {
            if (FindPrice(line, fromStopId.Value, toStopId.Value) != null)
                matchingLines.Add(line);
        }
        return matchingLines;
    }

    // يرجع كل الخطوط المفتوحة (ويا محطاتها وأسعارها وسائقها)، ويشيل النسائية إذا الراكب مو أنثى
    private async Task<List<Line>> GetOpenLinesAsync(bool includeWomenOnly)
    {
        IQueryable<Line> query = LinesWithDetails()
            .Where(l => l.LineStatus == "Open"
                && l.Driver.VerificationStatus == "Approved"
                && l.Driver.User.AccountStatus == "Active"
                && !l.Driver.User.IsDeleted);

        if (!includeWomenOnly)
            query = query.Where(l => !l.IsWomenOnly);

        return await query.OrderBy(l => l.DepartureTime).ToListAsync();
    }

    // استعلام الخطوط ويا كل التفاصيل اللي نحتاجها بالعرض
    private IQueryable<Line> LinesWithDetails()
    {
        return _db.Lines
            .Include(l => l.Driver).ThenInclude(d => d.User)
            .Include(l => l.Vehicle)
            .Include(l => l.Linestops).ThenInclude(ls => ls.Stop)
            .Include(l => l.Linedays)
            .Include(l => l.Lineprices);
    }

    // يرجع تفاصيل خط مفتوح للراكب (أو null إذا الخط مو موجود أو مغلق أو نسائي والراكب مو أنثى)
    public async Task<Line?> GetOpenLineAsync(int userId, int lineId)
    {
        bool isFemale = await IsFemaleAsync(userId);
        List<Line> openLines = await GetOpenLinesAsync(isFemale);
        foreach (Line line in openLines)
        {
            if (line.LineId == lineId)
                return line;
        }
        return null;
    }

    // ================= طلب اشتراك =================

    // يسجل طلب اشتراك جديد (Pending) بعد ما يتحقق من كل القواعد، ويرسل إشعار للسائق
    public async Task<ServiceResult> RequestSubscriptionAsync(int userId, RequestSubscriptionViewModel model)
    {
        ServiceResult result = new ServiceResult();
        User? passenger = await GetActivePassengerAsync(userId, result);
        if (passenger == null)
            return result;

        Line? line = await FindLineForRequestAsync(userId, model.LineId, result);
        if (line == null)
            return result;

        Lineprice? price = ValidateStopsAndPrice(model, line, result);
        ValidateSeatsAndDates(model, line, result);
        await CheckNoOpenSubscriptionAsync(userId, line.LineId, result);
        if (!result.Success)
            return result;

        await SaveRequestAsync(model, line, passenger, price!);
        return result;
    }

    // يرجع المستخدم إذا هو راكب فعّال، وإلا يضيف خطأ ويرجع null
    private async Task<User?> GetActivePassengerAsync(int userId, ServiceResult result)
    {
        User? user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null || user.IsDeleted || user.AccountStatus != "Active" || user.Role.RoleName != "Passenger")
        {
            result.AddError("", "بس الركاب يكدرون يطلبون اشتراك");
            return null;
        }
        return user;
    }

    // يرجع الخط إذا مفتوح ومتاح لهذا الراكب، وإلا يضيف خطأ ويرجع null
    private async Task<Line?> FindLineForRequestAsync(int userId, int lineId, ServiceResult result)
    {
        Line? line = await GetOpenLineAsync(userId, lineId);
        if (line == null)
            result.AddError("", "الخط غير متوفر");

        return line;
    }

    // يتأكد إن محطة الصعود والنزول على الخط (الصعود قبل النزول) وإن أكو سعر لهذا الزوج، ويرجع السعر
    private static Lineprice? ValidateStopsAndPrice(RequestSubscriptionViewModel model, Line line, ServiceResult result)
    {
        if (model.PickupStopId == null || model.DropoffStopId == null)
        {
            result.AddError("PickupStopId", "اختر محطة الصعود والنزول");
            return null;
        }

        Lineprice? price = FindPrice(line, model.PickupStopId.Value, model.DropoffStopId.Value);
        if (price == null)
            result.AddError("DropoffStopId", "ماكو اشتراك متوفر بين هاتين المحطتين (النزول لازم يكون بعد الصعود)");

        return price;
    }

    // يرجع سعر الرحلة بين محطتين على الخط إذا الصعود قبل النزول وأكو سعر محدد، وإلا null
    public static Lineprice? FindPrice(Line line, int fromStopId, int toStopId)
    {
        int fromOrder = GetStopOrder(line, fromStopId);
        int toOrder = GetStopOrder(line, toStopId);
        if (fromOrder == 0 || toOrder == 0 || fromOrder >= toOrder)
            return null;

        foreach (Lineprice price in line.Lineprices)
        {
            if (price.FromStopId == fromStopId && price.ToStopId == toStopId)
                return price;
        }
        return null;
    }

    // يرجع ترتيب المحطة بالخط، أو 0 إذا المحطة مو على الخط
    private static int GetStopOrder(Line line, int stopId)
    {
        foreach (Linestop lineStop in line.Linestops)
        {
            if (lineStop.StopId == stopId)
                return lineStop.StopOrder;
        }
        return 0;
    }

    // يتحقق من عدد المقاعد وتاريخ البداية وعدد الأشهر
    private static void ValidateSeatsAndDates(RequestSubscriptionViewModel model, Line line, ServiceResult result)
    {
        if (model.SeatsCount == null || model.SeatsCount < 1 || model.SeatsCount > line.TotalSeats)
            result.AddError("SeatsCount", "عدد المقاعد لازم يكون من 1 إلى " + line.TotalSeats);

        DateTime today = DateTime.Today;
        if (model.StartDate == null || model.StartDate.Value.Date < today || model.StartDate.Value.Date > today.AddDays(MaxDaysBeforeStart))
            result.AddError("StartDate", "تاريخ البداية لازم يكون من اليوم لحد " + MaxDaysBeforeStart + " يوم");

        // المدة لازم تكون على الأقل أقل مدة يحددها السائق للخط
        if (model.Months == null || model.Months < line.MinSubscriptionMonths || model.Months > 12)
            result.AddError("Months", "عدد الأشهر لازم يكون من " + line.MinSubscriptionMonths + " إلى 12");
    }

    // يتأكد إن الراكب ما عنده طلب معلق أو اشتراك فعّال على نفس الخط (نفس قاعدة الداتابيس)
    private async Task CheckNoOpenSubscriptionAsync(int userId, int lineId, ServiceResult result)
    {
        bool hasOpen = await _db.Subscriptions.AnyAsync(s => s.LineId == lineId
            && s.PassengerId == userId
            && (s.SubscriptionStatus == "Pending" || s.SubscriptionStatus == "Active"));

        if (hasOpen)
            result.AddError("", "عندك طلب أو اشتراك مفتوح على هذا الخط من قبل");
    }

    // يحفظ الطلب (السعر ينحفظ نسخة وقت الطلب) ويرسل إشعار للسائق
    private async Task SaveRequestAsync(RequestSubscriptionViewModel model, Line line, User passenger, Lineprice price)
    {
        Subscription subscription = new Subscription();
        subscription.LineId = line.LineId;
        subscription.PassengerId = passenger.UserId;
        subscription.PickupStopId = model.PickupStopId!.Value;
        subscription.DropoffStopId = model.DropoffStopId!.Value;
        subscription.SeatsCount = model.SeatsCount!.Value;
        subscription.MonthlyPrice = price.MonthlyPrice;
        subscription.StartDate = DateOnly.FromDateTime(model.StartDate!.Value);
        subscription.EndDate = CalculateEndDate(subscription.StartDate, model.Months!.Value);
        subscription.SubscriptionStatus = "Pending";
        subscription.CreatedAt = DateTime.Now;
        _db.Subscriptions.Add(subscription);

        AddNotification(line.Driver.UserId, line.LineId, "SubscriptionRequest", "طلب اشتراك جديد",
            passenger.FullName + " طلب اشتراك على خطك.");
        await _db.SaveChangesAsync();
    }

    // تاريخ النهاية = البداية + عدد الأشهر - يوم واحد (مثال: من 1/12 لشهر واحد ينتهي 31/12)
    public static DateOnly CalculateEndDate(DateOnly startDate, int months)
    {
        return startDate.AddMonths(months).AddDays(-1);
    }

    // ================= اشتراكاتي =================

    // يرجع كل اشتراكات الراكب ويا الخط والمحطات، الأحدث أولاً
    // ملاحظة: أسماء الـ scaffold مربكة: Linestop = محطة النزول، LinestopNavigation = محطة الصعود
    public async Task<List<Subscription>> GetMySubscriptionsAsync(int userId)
    {
        return await _db.Subscriptions
            .Include(s => s.Line)
            .Include(s => s.Linestop).ThenInclude(ls => ls.Stop)
            .Include(s => s.LinestopNavigation).ThenInclude(ls => ls.Stop)
            .Where(s => s.PassengerId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    // ================= إلغاء اشتراك =================

    // الراكب يلغي طلبه المعلق أو اشتراكه الفعّال (بس اشتراكه هو)، ويرسل إشعار للسائق
    public async Task<ServiceResult> CancelSubscriptionAsync(int userId, int subscriptionId, string? reason)
    {
        ServiceResult result = new ServiceResult();
        string? cleanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (cleanReason != null && cleanReason.Length > 255)
        {
            result.AddError("Reason", "سبب الإلغاء طويل جداً (أكثر شي 255 حرف)");
            return result;
        }

        Subscription? subscription = await FindOpenSubscriptionAsync(userId, subscriptionId, result);
        if (subscription == null)
            return result;

        subscription.SubscriptionStatus = "Cancelled";
        subscription.CancelledAt = DateTime.Now;
        subscription.CancelReason = cleanReason;
        AddNotification(subscription.Line.Driver.UserId, subscription.LineId, "SubscriptionCancelled",
            "تم إلغاء اشتراك", "راكب ألغى اشتراكه على خطك.");
        await _db.SaveChangesAsync();
        return result;
    }

    // يرجع اشتراك الراكب إذا موجود وحالته Pending أو Active، وإلا يضيف خطأ ويرجع null
    private async Task<Subscription?> FindOpenSubscriptionAsync(int userId, int subscriptionId, ServiceResult result)
    {
        // نبحث بالرقم ورقم الراكب سوية، حتى ما يكدر يلغي اشتراك غيره
        Subscription? subscription = await _db.Subscriptions
            .Include(s => s.Line).ThenInclude(l => l.Driver)
            .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId && s.PassengerId == userId);

        if (subscription == null)
        {
            result.AddError("", "الاشتراك غير موجود");
            return null;
        }

        if (subscription.SubscriptionStatus != "Pending" && subscription.SubscriptionStatus != "Active")
        {
            result.AddError("", "ما تكدر تلغي هذا الاشتراك بحالته الحالية");
            return null;
        }
        return subscription;
    }

    // ================= أدوات مساعدة =================

    // يرجع true إذا جنس المستخدم Female
    private async Task<bool> IsFemaleAsync(int userId)
    {
        return await _db.Users.AnyAsync(u => u.UserId == userId && u.Gender == "Female");
    }

    // يضيف إشعار جديد (ينحفظ ويا الـ SaveChanges الجاي)
    private void AddNotification(int userId, int lineId, string type, string title, string message)
    {
        Notification notification = new Notification();
        notification.UserId = userId;
        notification.RelatedLineId = lineId;
        notification.Type = type;
        notification.Title = title;
        notification.Message = message.Length > 255 ? message.Substring(0, 255) : message;
        notification.IsRead = false;
        notification.CreatedAt = DateTime.Now;
        _db.Notifications.Add(notification);
    }
}
