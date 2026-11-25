using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.Services;
using TransportLines.ViewModels;

namespace TransportLines.Tests;

// اختبارات خدمة الاشتراكات (جهة الراكب): البحث، طلب الاشتراك، اشتراكاتي، والإلغاء
public class SubscriptionServiceTests
{
    private readonly AppDbContext _db;
    private readonly LineService _lineService;
    private readonly SubscriptionService _service;

    // كل اختبار ياخذ داتابيس وهمية جديدة
    public SubscriptionServiceTests()
    {
        _db = TestHelper.CreateDatabase();
        _lineService = new LineService(_db);
        _service = new SubscriptionService(_db);
    }

    // يسوي سائق موافق عليه وخط صحيح (محطات 1، 2، 3) ويرجع الخط
    private async Task<Line> CreateLineAsync(bool womenOnly = false)
    {
        Driver driver = await TestHelper.AddApprovedDriverAsync(_db);
        CreateLineViewModel model = TestHelper.ValidLine();
        model.IsWomenOnly = womenOnly;
        await _lineService.CreateLineAsync(driver.UserId, model);
        return await _db.Lines.SingleAsync();
    }

    // فورم طلب صحيح: من 1 إلى 3، مقعد واحد، يبدي باچر، شهر واحد
    private static RequestSubscriptionViewModel ValidRequest(int lineId)
    {
        RequestSubscriptionViewModel model = new RequestSubscriptionViewModel();
        model.LineId = lineId;
        model.PickupStopId = 1;
        model.DropoffStopId = 3;
        model.SeatsCount = 1;
        model.StartDate = DateTime.Today.AddDays(1);
        model.Months = 1;
        return model;
    }

    // ===== البحث =====

    // بدون محطات يرجع كل الخطوط المفتوحة، وبمحطات يرجع بس الخطوط اللي تمر بيهم بالترتيب وإلها سعر
    [Fact]
    public async Task Search_FiltersByStopsOrderAndPrice()
    {
        await CreateLineAsync();
        User passenger = await TestHelper.AddPassengerAsync(_db);

        Assert.Single(await _service.SearchLinesAsync(passenger.UserId, null, null));
        Assert.Single(await _service.SearchLinesAsync(passenger.UserId, 1, 3));
        Assert.Empty(await _service.SearchLinesAsync(passenger.UserId, 3, 1));
        Assert.Empty(await _service.SearchLinesAsync(passenger.UserId, 2, 3));
        Assert.Empty(await _service.SearchLinesAsync(passenger.UserId, 1, 4));
    }

    // الخط النسائي ما يظهر للراكب الذكر، ويظهر للراكبة
    [Fact]
    public async Task Search_WomenOnlyLine_VisibleOnlyToFemale()
    {
        await CreateLineAsync(womenOnly: true);
        User male = await TestHelper.AddPassengerAsync(_db, 1, "Male");
        User female = await TestHelper.AddPassengerAsync(_db, 2, "Female");

        Assert.Empty(await _service.SearchLinesAsync(male.UserId, null, null));
        Assert.Single(await _service.SearchLinesAsync(female.UserId, null, null));
    }

    // الخط المغلق ما يظهر بالبحث
    [Fact]
    public async Task Search_ClosedLine_IsHidden()
    {
        Line line = await CreateLineAsync();
        line.LineStatus = "Closed";
        await _db.SaveChangesAsync();
        User passenger = await TestHelper.AddPassengerAsync(_db);

        Assert.Empty(await _service.SearchLinesAsync(passenger.UserId, null, null));
    }

    // ===== طلب الاشتراك الصحيح =====

    // طلب صحيح: حالته Pending، السعر نسخة من سعر الخط، تاريخ النهاية صحيح، والسائق ياخذ إشعار
    [Fact]
    public async Task Request_Valid_SavesPendingAndNotifiesDriver()
    {
        Line line = await CreateLineAsync();
        User passenger = await TestHelper.AddPassengerAsync(_db);

        ServiceResult result = await _service.RequestSubscriptionAsync(passenger.UserId, ValidRequest(line.LineId));

        Assert.True(result.Success);
        Subscription subscription = await _db.Subscriptions.SingleAsync();
        Assert.Equal("Pending", subscription.SubscriptionStatus);
        Assert.Equal(50000, subscription.MonthlyPrice);
        Assert.Equal(SubscriptionService.CalculateEndDate(subscription.StartDate, 1), subscription.EndDate);
        Notification notification = await _db.Notifications.SingleAsync();
        Assert.Equal(line.Driver.UserId, notification.UserId);
    }

    // تاريخ النهاية = البداية + الأشهر - يوم
    [Fact]
    public void CalculateEndDate_IsOneMonthMinusOneDay()
    {
        Assert.Equal(new DateOnly(2026, 12, 31), SubscriptionService.CalculateEndDate(new DateOnly(2026, 12, 1), 1));
        Assert.Equal(new DateOnly(2027, 2, 28), SubscriptionService.CalculateEndDate(new DateOnly(2026, 12, 1), 3));
    }

    // ===== رفض الطلب =====

    // سائق (مو راكب) ما يكدر يطلب اشتراك
    [Fact]
    public async Task Request_ByDriver_Fails()
    {
        Line line = await CreateLineAsync();

        ServiceResult result = await _service.RequestSubscriptionAsync(line.Driver.UserId, ValidRequest(line.LineId));

        Assert.False(result.Success);
    }

    // طلب على خط مغلق أو مو موجود يرفض
    [Fact]
    public async Task Request_ClosedOrUnknownLine_Fails()
    {
        Line line = await CreateLineAsync();
        User passenger = await TestHelper.AddPassengerAsync(_db);
        Assert.False((await _service.RequestSubscriptionAsync(passenger.UserId, ValidRequest(999))).Success);

        line.LineStatus = "Closed";
        await _db.SaveChangesAsync();
        Assert.False((await _service.RequestSubscriptionAsync(passenger.UserId, ValidRequest(line.LineId))).Success);
    }

    // محطات بالعكس، أو زوج بدون سعر، أو محطة مو على الخط يرفض
    [Theory]
    [InlineData(3, 1)]
    [InlineData(2, 3)]
    [InlineData(1, 4)]
    [InlineData(1, 1)]
    public async Task Request_InvalidStops_Fails(int pickup, int dropoff)
    {
        Line line = await CreateLineAsync();
        User passenger = await TestHelper.AddPassengerAsync(_db);
        RequestSubscriptionViewModel model = ValidRequest(line.LineId);
        model.PickupStopId = pickup;
        model.DropoffStopId = dropoff;

        ServiceResult result = await _service.RequestSubscriptionAsync(passenger.UserId, model);

        Assert.True(result.Errors.ContainsKey("DropoffStopId"));
    }

    // مقاعد أكثر من مقاعد الخط (10) أو صفر يرفض
    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Request_InvalidSeats_Fails(int seats)
    {
        Line line = await CreateLineAsync();
        User passenger = await TestHelper.AddPassengerAsync(_db);
        RequestSubscriptionViewModel model = ValidRequest(line.LineId);
        model.SeatsCount = seats;

        ServiceResult result = await _service.RequestSubscriptionAsync(passenger.UserId, model);

        Assert.True(result.Errors.ContainsKey("SeatsCount"));
    }

    // تاريخ بداية بالماضي أو بعيد أكثر من 60 يوم يرفض
    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    public async Task Request_InvalidStartDate_Fails(int daysFromToday)
    {
        Line line = await CreateLineAsync();
        User passenger = await TestHelper.AddPassengerAsync(_db);
        RequestSubscriptionViewModel model = ValidRequest(line.LineId);
        model.StartDate = DateTime.Today.AddDays(daysFromToday);

        ServiceResult result = await _service.RequestSubscriptionAsync(passenger.UserId, model);

        Assert.True(result.Errors.ContainsKey("StartDate"));
    }

    // عدد أشهر أقل من أقل مدة للخط يرفض
    [Fact]
    public async Task Request_LessThanMinMonths_Fails()
    {
        Line line = await CreateLineAsync();
        line.MinSubscriptionMonths = 3;
        await _db.SaveChangesAsync();
        User passenger = await TestHelper.AddPassengerAsync(_db);

        ServiceResult result = await _service.RequestSubscriptionAsync(passenger.UserId, ValidRequest(line.LineId));

        Assert.True(result.Errors.ContainsKey("Months"));
    }

    // راكب ذكر ما يكدر يطلب على خط نسائي
    [Fact]
    public async Task Request_MaleOnWomenOnlyLine_Fails()
    {
        Line line = await CreateLineAsync(womenOnly: true);
        User male = await TestHelper.AddPassengerAsync(_db, 1, "Male");

        ServiceResult result = await _service.RequestSubscriptionAsync(male.UserId, ValidRequest(line.LineId));

        Assert.False(result.Success);
    }

    // طلب ثاني على نفس الخط يرفض، وبعد الإلغاء يسمح بطلب جديد
    [Fact]
    public async Task Request_SecondOpenRequest_FailsUntilCancelled()
    {
        Line line = await CreateLineAsync();
        User passenger = await TestHelper.AddPassengerAsync(_db);
        await _service.RequestSubscriptionAsync(passenger.UserId, ValidRequest(line.LineId));

        Assert.False((await _service.RequestSubscriptionAsync(passenger.UserId, ValidRequest(line.LineId))).Success);

        Subscription first = await _db.Subscriptions.SingleAsync();
        await _service.CancelSubscriptionAsync(passenger.UserId, first.SubscriptionId, null);
        Assert.True((await _service.RequestSubscriptionAsync(passenger.UserId, ValidRequest(line.LineId))).Success);
    }

    // ===== اشتراكاتي والإلغاء =====

    // كل راكب يشوف اشتراكاته هو بس
    [Fact]
    public async Task MySubscriptions_OnlyOwn()
    {
        Line line = await CreateLineAsync();
        User passengerA = await TestHelper.AddPassengerAsync(_db, 1);
        User passengerB = await TestHelper.AddPassengerAsync(_db, 2);
        await _service.RequestSubscriptionAsync(passengerA.UserId, ValidRequest(line.LineId));

        Assert.Single(await _service.GetMySubscriptionsAsync(passengerA.UserId));
        Assert.Empty(await _service.GetMySubscriptionsAsync(passengerB.UserId));
    }

    // إلغاء صحيح: الحالة Cancelled، ينحفظ الوقت والسبب، والسائق ياخذ إشعار
    [Fact]
    public async Task Cancel_Own_SetsCancelledAndNotifies()
    {
        Line line = await CreateLineAsync();
        User passenger = await TestHelper.AddPassengerAsync(_db);
        await _service.RequestSubscriptionAsync(passenger.UserId, ValidRequest(line.LineId));
        Subscription subscription = await _db.Subscriptions.SingleAsync();

        ServiceResult result = await _service.CancelSubscriptionAsync(passenger.UserId, subscription.SubscriptionId, " ما احتاجه ");

        Assert.True(result.Success);
        Assert.Equal("Cancelled", subscription.SubscriptionStatus);
        Assert.Equal("ما احتاجه", subscription.CancelReason);
        Assert.NotNull(subscription.CancelledAt);
        Assert.Equal(2, await _db.Notifications.CountAsync());
    }

    // راكب ما يكدر يلغي اشتراك غيره، وما يكدر يلغي اشتراك ملغي
    [Fact]
    public async Task Cancel_OthersOrAlreadyCancelled_Fails()
    {
        Line line = await CreateLineAsync();
        User owner = await TestHelper.AddPassengerAsync(_db, 1);
        User other = await TestHelper.AddPassengerAsync(_db, 2);
        await _service.RequestSubscriptionAsync(owner.UserId, ValidRequest(line.LineId));
        Subscription subscription = await _db.Subscriptions.SingleAsync();

        Assert.False((await _service.CancelSubscriptionAsync(other.UserId, subscription.SubscriptionId, null)).Success);
        Assert.Equal("Pending", subscription.SubscriptionStatus);

        await _service.CancelSubscriptionAsync(owner.UserId, subscription.SubscriptionId, null);
        Assert.False((await _service.CancelSubscriptionAsync(owner.UserId, subscription.SubscriptionId, null)).Success);
    }

    // سبب إلغاء أطول من 255 حرف يرفض
    [Fact]
    public async Task Cancel_TooLongReason_Fails()
    {
        Line line = await CreateLineAsync();
        User passenger = await TestHelper.AddPassengerAsync(_db);
        await _service.RequestSubscriptionAsync(passenger.UserId, ValidRequest(line.LineId));
        Subscription subscription = await _db.Subscriptions.SingleAsync();

        ServiceResult result = await _service.CancelSubscriptionAsync(passenger.UserId, subscription.SubscriptionId, new string('a', 256));

        Assert.True(result.Errors.ContainsKey("Reason"));
    }
}
