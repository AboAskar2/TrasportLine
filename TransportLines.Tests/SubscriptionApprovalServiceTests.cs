using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.Services;
using TransportLines.ViewModels;

namespace TransportLines.Tests;

// اختبارات موافقة السائق ورفضه لطلبات الاشتراك، وأهمها فحص المقاعد لكل مقطع
// الخط بالاختبارات: محطات 1 ← 2 ← 3 (مقطعين: 1-2 و 2-3)، وبيه 3 مقاعد بس حتى الحسابات تكون سهلة
public class SubscriptionApprovalServiceTests
{
    private readonly AppDbContext _db;
    private readonly SubscriptionService _subscriptionService;
    private readonly SubscriptionApprovalService _approvalService;
    private Driver _driver = null!;
    private Line _line = null!;
    private int _nextPassenger = 1;

    // كل اختبار ياخذ داتابيس وهمية جديدة
    public SubscriptionApprovalServiceTests()
    {
        _db = TestHelper.CreateDatabase();
        _subscriptionService = new SubscriptionService(_db);
        _approvalService = new SubscriptionApprovalService(_db);
    }

    // يسوي سائق موافق عليه وخط بـ 3 مقاعد وأسعار لكل الأزواج (1-2، 2-3، 1-3)
    private async Task CreateLineAsync()
    {
        _driver = await TestHelper.AddApprovedDriverAsync(_db);
        CreateLineViewModel model = TestHelper.ValidLine();
        model.TotalSeats = 3;
        model.Prices.Add(new LinePriceInput { FromStopId = 2, ToStopId = 3, MonthlyPrice = 25000 });
        await new LineService(_db).CreateLineAsync(_driver.UserId, model);
        _line = await _db.Lines.SingleAsync();
    }

    // يسوي راكب جديد ويطلب اشتراك بالمحطات والمقاعد المطلوبة، ويرجع الطلب
    private async Task<Subscription> RequestAsync(int pickup, int dropoff, int seats, int startAfterDays = 1)
    {
        User passenger = await TestHelper.AddPassengerAsync(_db, _nextPassenger);
        _nextPassenger++;

        RequestSubscriptionViewModel model = new RequestSubscriptionViewModel
        {
            LineId = _line.LineId, PickupStopId = pickup, DropoffStopId = dropoff,
            SeatsCount = seats, StartDate = DateTime.Today.AddDays(startAfterDays), Months = 1
        };
        await _subscriptionService.RequestSubscriptionAsync(passenger.UserId, model);
        return await _db.Subscriptions.SingleAsync(s => s.PassengerId == passenger.UserId);
    }

    // يوافق على طلب باسم سائق الخط ويرجع النتيجة
    private async Task<ServiceResult> ApproveAsync(Subscription subscription)
    {
        return await _approvalService.ApproveAsync(_driver.UserId, subscription.SubscriptionId);
    }

    // ===== الموافقة العادية =====

    // موافقة صحيحة: الحالة Active، وقت الموافقة ينحفظ، والراكب ياخذ إشعار
    [Fact]
    public async Task Approve_Valid_SetsActiveAndNotifiesPassenger()
    {
        await CreateLineAsync();
        Subscription request = await RequestAsync(1, 3, 1);

        ServiceResult result = await ApproveAsync(request);

        Assert.True(result.Success);
        Assert.Equal("Active", request.SubscriptionStatus);
        Assert.NotNull(request.ApprovedAt);
        Assert.True(await _db.Notifications.AnyAsync(n => n.UserId == request.PassengerId && n.Type == "SubscriptionApproved"));
    }

    // سائق ثاني ما يكدر يوافق على طلب مو على خطه
    [Fact]
    public async Task Approve_ByOtherDriver_Fails()
    {
        await CreateLineAsync();
        Subscription request = await RequestAsync(1, 3, 1);
        Driver other = await TestHelper.AddApprovedDriverAsync(_db, 2);

        ServiceResult result = await _approvalService.ApproveAsync(other.UserId, request.SubscriptionId);

        Assert.False(result.Success);
        Assert.Equal("Pending", request.SubscriptionStatus);
    }

    // ما نكدر نوافق على طلب مرتين
    [Fact]
    public async Task Approve_Twice_Fails()
    {
        await CreateLineAsync();
        Subscription request = await RequestAsync(1, 3, 1);
        await ApproveAsync(request);

        Assert.False((await ApproveAsync(request)).Success);
    }

    // خط مغلق أو سائق انسحبت موافقته: الموافقة ترفض
    [Fact]
    public async Task Approve_ClosedLineOrUnapprovedDriver_Fails()
    {
        await CreateLineAsync();
        Subscription request = await RequestAsync(1, 3, 1);

        _driver.VerificationStatus = "Rejected";
        await _db.SaveChangesAsync();
        Assert.False((await ApproveAsync(request)).Success);

        _driver.VerificationStatus = "Approved";
        _line.LineStatus = "Closed";
        await _db.SaveChangesAsync();
        Assert.False((await ApproveAsync(request)).Success);
    }

    // طلب خلصت مدته قبل ما السائق يوافق: الموافقة ترفض
    [Fact]
    public async Task Approve_ExpiredRequest_Fails()
    {
        await CreateLineAsync();
        Subscription request = await RequestAsync(1, 3, 1);
        request.StartDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-2));
        request.EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        await _db.SaveChangesAsync();

        Assert.False((await ApproveAsync(request)).Success);
    }

    // ===== فحص المقاعد لكل مقطع (قاعدة 4) =====

    // الخط 3 مقاعد: A (1←3، مقعدين) فعّال. B (1←2، مقعدين) يرفض لأن المقطع 1-2 يصير 4.
    // C (2←3، مقعد) ينقبل لأن المقطع 2-3 يصير 3. D (2←3، مقعد) يرفض لأن المقطع 2-3 يصير 4.
    [Fact]
    public async Task Approve_ChecksEachSegment()
    {
        await CreateLineAsync();
        Subscription a = await RequestAsync(1, 3, 2);
        Subscription b = await RequestAsync(1, 2, 2);
        Subscription c = await RequestAsync(2, 3, 1);
        Subscription d = await RequestAsync(2, 3, 1);

        Assert.True((await ApproveAsync(a)).Success);
        Assert.False((await ApproveAsync(b)).Success);
        Assert.True((await ApproveAsync(c)).Success);
        Assert.False((await ApproveAsync(d)).Success);
        Assert.Equal("Pending", b.SubscriptionStatus);
    }

    // مقاطع ما تتداخل: A (1←2، 3 مقاعد) و B (2←3، 3 مقاعد) ينقبلون الاثنين
    // لأن A ينزل بالمحطة 2 قبل ما B يصعد
    [Fact]
    public async Task Approve_NonOverlappingSegments_BothAccepted()
    {
        await CreateLineAsync();
        Subscription a = await RequestAsync(1, 2, 3);
        Subscription b = await RequestAsync(2, 3, 3);

        Assert.True((await ApproveAsync(a)).Success);
        Assert.True((await ApproveAsync(b)).Success);
    }

    // تواريخ ما تتداخل: A (3 مقاعد) يبدي باچر لشهر، B (3 مقاعد) يبدي بعد 35 يوم، ينقبلون الاثنين
    [Fact]
    public async Task Approve_NonOverlappingDates_BothAccepted()
    {
        await CreateLineAsync();
        Subscription a = await RequestAsync(1, 3, 3, startAfterDays: 1);
        Subscription b = await RequestAsync(1, 3, 3, startAfterDays: 35);

        Assert.True((await ApproveAsync(a)).Success);
        Assert.True((await ApproveAsync(b)).Success);
    }

    // الطلب المعلق ما يحجز مقاعد: A (3 مقاعد) معلق، B (3 مقاعد) ينقبل، وبعدها A يرفض
    [Fact]
    public async Task Approve_PendingRequestsDoNotHoldSeats()
    {
        await CreateLineAsync();
        Subscription a = await RequestAsync(1, 3, 3);
        Subscription b = await RequestAsync(1, 3, 3);

        Assert.True((await ApproveAsync(b)).Success);
        Assert.False((await ApproveAsync(a)).Success);
    }

    // الاشتراك الملغي ما يحسب: بعد إلغاء A يصير أكو مكان لـ B
    [Fact]
    public async Task Approve_CancelledSubscriptionFreesSeats()
    {
        await CreateLineAsync();
        Subscription a = await RequestAsync(1, 3, 3);
        Subscription b = await RequestAsync(1, 3, 3);
        await ApproveAsync(a);
        Assert.False((await ApproveAsync(b)).Success);

        await _subscriptionService.CancelSubscriptionAsync(a.PassengerId, a.SubscriptionId, null);
        Assert.True((await ApproveAsync(b)).Success);
    }

    // اختبار مباشر لحساب مقاعد المقطع
    [Fact]
    public void CountSeatsOnSegment_SumsOnlyCoveringSubscriptions()
    {
        Dictionary<int, int> orders = new Dictionary<int, int> { { 10, 1 }, { 20, 2 }, { 30, 3 } };
        List<Subscription> subscriptions = new List<Subscription>
        {
            new Subscription { PickupStopId = 10, DropoffStopId = 30, SeatsCount = 2 },
            new Subscription { PickupStopId = 10, DropoffStopId = 20, SeatsCount = 1 },
            new Subscription { PickupStopId = 20, DropoffStopId = 30, SeatsCount = 4 }
        };

        Assert.Equal(3, SubscriptionApprovalService.CountSeatsOnSegment(subscriptions, orders, 1));
        Assert.Equal(6, SubscriptionApprovalService.CountSeatsOnSegment(subscriptions, orders, 2));
    }

    // ===== الرفض =====

    // رفض صحيح: الحالة Rejected والسبب ينحفظ، والراكب ياخذ إشعار
    [Fact]
    public async Task Reject_WithReason_SetsRejectedAndNotifies()
    {
        await CreateLineAsync();
        Subscription request = await RequestAsync(1, 3, 1);

        ServiceResult result = await _approvalService.RejectAsync(_driver.UserId, request.SubscriptionId, "الخط ممتلئ هذا الشهر");

        Assert.True(result.Success);
        Assert.Equal("Rejected", request.SubscriptionStatus);
        Assert.Equal("الخط ممتلئ هذا الشهر", request.RejectionReason);
        Assert.True(await _db.Notifications.AnyAsync(n => n.UserId == request.PassengerId && n.Type == "SubscriptionRejected"));
    }

    // رفض بدون سبب أو بسبب قصير، أو من سائق ثاني، يفشل
    [Fact]
    public async Task Reject_InvalidReasonOrOtherDriver_Fails()
    {
        await CreateLineAsync();
        Subscription request = await RequestAsync(1, 3, 1);
        Driver other = await TestHelper.AddApprovedDriverAsync(_db, 2);

        Assert.True((await _approvalService.RejectAsync(_driver.UserId, request.SubscriptionId, "لا")).Errors.ContainsKey("Reason"));
        Assert.False((await _approvalService.RejectAsync(other.UserId, request.SubscriptionId, "سبب الرفض هنا")).Success);
        Assert.Equal("Pending", request.SubscriptionStatus);
    }

    // ===== عرض طلبات الخط =====

    // السائق يشوف طلبات خطه، وسائق ثاني ما يشوف شي
    [Fact]
    public async Task GetLineSubscriptions_OnlyForLineOwner()
    {
        await CreateLineAsync();
        await RequestAsync(1, 3, 1);
        Driver other = await TestHelper.AddApprovedDriverAsync(_db, 2);

        Assert.Single(await _approvalService.GetLineSubscriptionsAsync(_driver.UserId, _line.LineId));
        Assert.Empty(await _approvalService.GetLineSubscriptionsAsync(other.UserId, _line.LineId));
    }
}
