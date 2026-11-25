using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;

namespace TransportLines.Services;

// خدمة موافقة السائق على طلبات الاشتراك أو رفضها
// أهم قاعدة: فحص المقاعد لكل مقطع (بين محطتين متتاليتين) يصير وقت الموافقة،
// داخل Transaction تقفل صف الخط، حتى موافقتين بنفس اللحظة ما يحجزون أكثر من المقاعد
public class SubscriptionApprovalService
{
    // أقل وأكثر طول لسبب الرفض
    public const int MinReasonLength = 5;
    public const int MaxReasonLength = 255;

    private readonly AppDbContext _db;

    // يستلم الداتابيس من الـ DI
    public SubscriptionApprovalService(AppDbContext db)
    {
        _db = db;
    }

    // ================= عرض طلبات الخط =================

    // يرجع كل اشتراكات خط السائق (المعلقة أولاً ثم الأحدث)، أو قائمة فارغة إذا الخط مو إله
    public async Task<List<Subscription>> GetLineSubscriptionsAsync(int userId, int lineId)
    {
        return await _db.Subscriptions
            .Include(s => s.Passenger)
            .Include(s => s.Linestop).ThenInclude(ls => ls.Stop)
            .Include(s => s.LinestopNavigation).ThenInclude(ls => ls.Stop)
            .Where(s => s.LineId == lineId && s.Line.Driver.UserId == userId)
            .OrderByDescending(s => s.SubscriptionStatus == "Pending")
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    // ================= الموافقة =================

    // يوافق على طلب: يقفل الخط، يفحص المقاعد لكل مقطع، وإذا أكو مكان يصير الطلب Active ويبلغ الراكب
    public async Task<ServiceResult> ApproveAsync(int userId, int subscriptionId)
    {
        ServiceResult result = new ServiceResult();

        // كل الخطوات داخل Transaction وحدة: يا تنجح كلها يا ولا وحدة
        await using var transaction = await _db.Database.BeginTransactionAsync();

        // القفل لازم يكون أول شي، حتى كل القراءات اللي بعده تشوف آخر بيانات محفوظة
        await LockLineAsync(subscriptionId);

        Subscription? subscription = await FindPendingRequestAsync(userId, subscriptionId, result);
        if (subscription == null)
            return result;

        await CheckSeatsAsync(subscription, result);
        if (!result.Success)
            return result;

        ActivateSubscription(subscription);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return result;
    }

    // يغير حالة الطلب لـ Active ويسجل وقت الموافقة ويبلغ الراكب
    private void ActivateSubscription(Subscription subscription)
    {
        subscription.SubscriptionStatus = "Active";
        subscription.ApprovedAt = DateTime.Now;
        AddNotification(subscription.PassengerId, subscription.LineId, "SubscriptionApproved",
            "تمت الموافقة على اشتراكك", "السائق وافق على اشتراكك. يبدي من " + subscription.StartDate.ToString("yyyy-MM-dd") + ".");
    }

    // يقفل صف الخط (وصف الطلب) بالداتابيس بـ SELECT ... FOR UPDATE لحد ما تخلص الـ Transaction
    // أي موافقة ثانية على نفس الخط تنتظر هنا، فما يصير حجز مقاعد مرتين بنفس اللحظة
    // نوصل للخط عن طريق رقم الطلب (JOIN) حتى القفل يكون أول أمر بالـ Transaction
    // (الداتابيس الوهمية بالاختبارات ما تدعم SQL، فنتخطى القفل هناك)
    private async Task LockLineAsync(int subscriptionId)
    {
        if (!_db.Database.IsRelational())
            return;

        // ExecuteSqlInterpolated يحول {subscriptionId} لـ Parameter، يعني آمن من SQL Injection
        // lines بين علامتين ` لأن Lines كلمة محجوزة بـ MySQL
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT l.LineID FROM `lines` l JOIN subscriptions s ON s.LineID = l.LineID WHERE s.SubscriptionID = {subscriptionId} FOR UPDATE");
    }

    // ================= فحص المقاعد لكل مقطع =================

    // يفحص كل مقطع يمر بيه الطلب: المقاعد المحجوزة + مقاعد الطلب لازم ما تتجاوز مقاعد الخط
    private async Task CheckSeatsAsync(Subscription request, ServiceResult result)
    {
        Dictionary<int, int> stopOrders = await GetStopOrdersAsync(request.LineId);
        List<Subscription> activeSubscriptions = await GetOverlappingActiveAsync(request);

        int pickupOrder = stopOrders[request.PickupStopId];
        int dropoffOrder = stopOrders[request.DropoffStopId];

        // المقطع رقم k = من المحطة k إلى المحطة k+1
        for (int segment = pickupOrder; segment < dropoffOrder; segment++)
        {
            int usedSeats = CountSeatsOnSegment(activeSubscriptions, stopOrders, segment);
            if (usedSeats + request.SeatsCount > request.Line.TotalSeats)
            {
                AddNoSeatsError(result, segment, request.Line.TotalSeats - usedSeats);
                return;
            }
        }
    }

    // يضيف رسالة "ماكو مقاعد كافية" ويا رقم المقطع وعدد المقاعد المتبقية
    private static void AddNoSeatsError(ServiceResult result, int segment, int freeSeats)
    {
        if (freeSeats < 0)
            freeSeats = 0;

        result.AddError("", "ماكو مقاعد كافية بين المحطة " + segment + " والمحطة " + (segment + 1)
            + " (المتبقي " + freeSeats + " مقعد)");
    }

    // يحسب مجموع مقاعد الاشتراكات اللي تمر بالمقطع (صعودها قبل أو عند بداية المقطع، ونزولها بعد أو عند نهايته)
    public static int CountSeatsOnSegment(List<Subscription> subscriptions, Dictionary<int, int> stopOrders, int segment)
    {
        int total = 0;
        foreach (Subscription subscription in subscriptions)
        {
            int pickupOrder = stopOrders[subscription.PickupStopId];
            int dropoffOrder = stopOrders[subscription.DropoffStopId];
            if (pickupOrder <= segment && dropoffOrder >= segment + 1)
                total = total + subscription.SeatsCount;
        }
        return total;
    }

    // يرجع جدول: رقم المحطة ← ترتيبها بالخط
    private async Task<Dictionary<int, int>> GetStopOrdersAsync(int lineId)
    {
        List<Linestop> lineStops = await _db.Linestops.Where(ls => ls.LineId == lineId).ToListAsync();

        Dictionary<int, int> stopOrders = new Dictionary<int, int>();
        foreach (Linestop lineStop in lineStops)
        {
            stopOrders[lineStop.StopId] = lineStop.StopOrder;
        }
        return stopOrders;
    }

    // يرجع الاشتراكات الفعّالة على نفس الخط اللي تتداخل تواريخها ويا تواريخ الطلب
    // (يتداخلون إذا كل واحد يبدي قبل ما يخلص الثاني)
    private async Task<List<Subscription>> GetOverlappingActiveAsync(Subscription request)
    {
        return await _db.Subscriptions
            .Where(s => s.LineId == request.LineId
                && s.SubscriptionStatus == "Active"
                && s.StartDate <= request.EndDate
                && s.EndDate >= request.StartDate)
            .ToListAsync();
    }

    // ================= الرفض =================

    // يرفض طلب معلق مع سبب إجباري ويبلغ الراكب
    public async Task<ServiceResult> RejectAsync(int userId, int subscriptionId, string? reason)
    {
        ServiceResult result = new ServiceResult();
        string cleanReason = (reason ?? "").Trim();
        if (cleanReason.Length < MinReasonLength || cleanReason.Length > MaxReasonLength)
        {
            result.AddError("Reason", "اكتب سبب الرفض (من 5 إلى 255 حرف)");
            return result;
        }

        Subscription? subscription = await FindPendingRequestAsync(userId, subscriptionId, result);
        if (subscription == null)
            return result;

        subscription.SubscriptionStatus = "Rejected";
        subscription.RejectionReason = cleanReason;
        AddNotification(subscription.PassengerId, subscription.LineId, "SubscriptionRejected",
            "تم رفض طلب الاشتراك", "السائق رفض طلبك. السبب: " + cleanReason);
        await _db.SaveChangesAsync();
        return result;
    }

    // ================= أدوات مساعدة =================

    // يرجع الطلب إذا: موجود، على خط يخص هذا السائق، السائق موافق عليه، الخط مفتوح، الطلب Pending، ومدته ما خلصت
    // وإلا يضيف الخطأ ويرجع null
    private async Task<Subscription?> FindPendingRequestAsync(int userId, int subscriptionId, ServiceResult result)
    {
        Subscription? subscription = await _db.Subscriptions
            .Include(s => s.Line).ThenInclude(l => l.Driver)
            .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId && s.Line.Driver.UserId == userId);

        string? error = CheckRequestState(subscription);
        if (error != null)
        {
            result.AddError("", error);
            return null;
        }
        return subscription;
    }

    // يفحص حالة الطلب ويرجع رسالة الخطأ، أو null إذا الطلب جاهز للموافقة أو الرفض
    private static string? CheckRequestState(Subscription? subscription)
    {
        if (subscription == null)
            return "الطلب غير موجود";
        if (subscription.Line.Driver.VerificationStatus != "Approved")
            return "حسابك مو موافق عليه";
        if (subscription.Line.LineStatus != "Open")
            return "الخط مو مفتوح";
        if (subscription.SubscriptionStatus != "Pending")
            return "هذا الطلب تمت مراجعته من قبل";
        if (subscription.EndDate < DateOnly.FromDateTime(DateTime.Today))
            return "مدة هذا الطلب خلصت";

        return null;
    }

    // يضيف إشعار للراكب (ينحفظ ويا الـ SaveChanges الجاي)
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
