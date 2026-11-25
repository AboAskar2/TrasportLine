using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.Services;
using TransportLines.ViewModels;

namespace TransportLines.Tests;

// اختبارات خدمة الخطوط: إنشاء خط صحيح، رفض البيانات الغلط، خطوط السائق، وإغلاق الخط
public class LineServiceTests
{
    private readonly AppDbContext _db;
    private readonly LineService _lineService;

    // كل اختبار ياخذ داتابيس وهمية جديدة
    public LineServiceTests()
    {
        _db = TestHelper.CreateDatabase();
        _lineService = new LineService(_db);
    }

    // ينشئ سائق موافق عليه ويسوي له خط صحيح، ويرجع الخط
    private async Task<Line> CreateValidLineAsync(Driver driver)
    {
        await _lineService.CreateLineAsync(driver.UserId, TestHelper.ValidLine());
        return await _db.Lines.SingleAsync(l => l.DriverId == driver.DriverId);
    }

    // ينشئ خط بالفورم المعطى ويرجع النتيجة (للاختبارات اللي تتوقع خطأ)
    private async Task<ServiceResult> CreateWithAsync(CreateLineViewModel model)
    {
        Driver driver = await TestHelper.AddApprovedDriverAsync(_db);
        return await _lineService.CreateLineAsync(driver.UserId, model);
    }

    // ===== الإنشاء الصحيح =====

    // خط صحيح ينحفظ ويا محطاته بالترتيب وأيامه وأسعاره، وحالته Open
    [Fact]
    public async Task CreateLine_Valid_SavesEverything()
    {
        Driver driver = await TestHelper.AddApprovedDriverAsync(_db);

        ServiceResult result = await _lineService.CreateLineAsync(driver.UserId, TestHelper.ValidLine());

        Assert.True(result.Success);
        Line line = await _db.Lines.Include(l => l.Linestops).Include(l => l.Linedays).Include(l => l.Lineprices).SingleAsync();
        Assert.Equal("Open", line.LineStatus);
        Assert.Equal(driver.Vehicles.First().VehicleId, line.VehicleId);
        Assert.Equal(3, line.Linestops.Count);
        Assert.Equal(3, line.Linestops.Single(s => s.StopId == 3).StopOrder);
        Assert.Equal(3, line.Linedays.Count);
        Assert.Equal(2, line.Lineprices.Count);
    }

    // صفوف المحطات والأسعار الفارغة بالفورم تنتجاهل والخط ينحفظ
    [Fact]
    public async Task CreateLine_EmptyRows_AreIgnored()
    {
        CreateLineViewModel model = TestHelper.ValidLine();
        model.Stops.Add(new LineStopInput());
        model.Prices.Add(new LinePriceInput());

        ServiceResult result = await CreateWithAsync(model);

        Assert.True(result.Success);
    }

    // ===== السائق والسيارة =====

    // سائق مو موافق عليه (Pending) ما يكدر ينشئ خط
    [Fact]
    public async Task CreateLine_PendingDriver_Fails()
    {
        Driver driver = await TestHelper.AddApprovedDriverAsync(_db);
        driver.VerificationStatus = "Pending";
        await _db.SaveChangesAsync();

        ServiceResult result = await _lineService.CreateLineAsync(driver.UserId, TestHelper.ValidLine());

        Assert.False(result.Success);
        Assert.Equal(0, await _db.Lines.CountAsync());
    }

    // سيارة فحصها منتهي: ما يكدر ينشئ خط
    [Fact]
    public async Task CreateLine_ExpiredInspection_Fails()
    {
        Driver driver = await TestHelper.AddApprovedDriverAsync(_db);
        driver.Vehicles.First().InspectionExpiryDate = DateOnly.FromDateTime(DateTime.Today);
        await _db.SaveChangesAsync();

        ServiceResult result = await _lineService.CreateLineAsync(driver.UserId, TestHelper.ValidLine());

        Assert.False(result.Success);
    }

    // مقاعد أكثر من مقاعد السيارة (12) ترفض
    [Fact]
    public async Task CreateLine_MoreSeatsThanVehicle_Fails()
    {
        CreateLineViewModel model = TestHelper.ValidLine();
        model.TotalSeats = 13;

        ServiceResult result = await CreateWithAsync(model);

        Assert.True(result.Errors.ContainsKey("TotalSeats"));
    }

    // ===== الأيام =====

    // أيام فارغة أو رقم يوم غلط أو يوم مكرر يرفض
    [Theory]
    [InlineData(new int[0])]
    [InlineData(new int[] { 7 })]
    [InlineData(new int[] { -1 })]
    [InlineData(new int[] { 1, 1 })]
    public async Task CreateLine_InvalidDays_Fails(int[] days)
    {
        CreateLineViewModel model = TestHelper.ValidLine();
        model.SelectedDays = new List<int>(days);

        ServiceResult result = await CreateWithAsync(model);

        Assert.True(result.Errors.ContainsKey("SelectedDays"));
    }

    // ===== المحطات =====

    // محطة وحدة بس ترفض (لازم 2 على الأقل)
    [Fact]
    public async Task CreateLine_OneStop_Fails()
    {
        CreateLineViewModel model = TestHelper.ValidLine();
        model.Stops.RemoveRange(1, 2);

        ServiceResult result = await CreateWithAsync(model);

        Assert.True(result.Errors.ContainsKey("Stops"));
    }

    // محطة مكررة، أو موقوفة (5)، أو مو موجودة (99) ترفض
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(99)]
    public async Task CreateLine_BadStop_Fails(int badStopId)
    {
        CreateLineViewModel model = TestHelper.ValidLine();
        model.Stops[1].StopId = badStopId;

        ServiceResult result = await CreateWithAsync(model);

        Assert.True(result.Errors.ContainsKey("Stops"));
    }

    // وقت وصول قبل وقت الانطلاق أو مو بالترتيب يرفض
    [Fact]
    public async Task CreateLine_ArrivalTimesNotInOrder_Fails()
    {
        CreateLineViewModel model = TestHelper.ValidLine();
        model.Stops[2].EstimatedArrivalTime = new TimeSpan(7, 5, 0);

        ServiceResult result = await CreateWithAsync(model);

        Assert.True(result.Errors.ContainsKey("Stops"));
    }

    // ===== الأسعار =====

    // سعر بالعكس (النزول قبل الصعود) يرفض
    [Fact]
    public async Task CreateLine_ReversedPrice_Fails()
    {
        CreateLineViewModel model = TestHelper.ValidLine();
        model.Prices.Add(new LinePriceInput { FromStopId = 3, ToStopId = 2, MonthlyPrice = 20000 });

        ServiceResult result = await CreateWithAsync(model);

        Assert.True(result.Errors.ContainsKey("Prices"));
    }

    // سعر صفر أو سالب أو بيه كسور أو كبير جداً يرفض
    [Theory]
    [InlineData(0)]
    [InlineData(-5000)]
    [InlineData(25000.5)]
    [InlineData(20000000)]
    public async Task CreateLine_InvalidPriceValue_Fails(double price)
    {
        CreateLineViewModel model = TestHelper.ValidLine();
        model.Prices[1].MonthlyPrice = (decimal)price;

        ServiceResult result = await CreateWithAsync(model);

        Assert.True(result.Errors.ContainsKey("Prices"));
    }

    // سعر لمحطة مو على الخط (4) يرفض
    [Fact]
    public async Task CreateLine_PriceForStopNotOnLine_Fails()
    {
        CreateLineViewModel model = TestHelper.ValidLine();
        model.Prices.Add(new LinePriceInput { FromStopId = 1, ToStopId = 4, MonthlyPrice = 60000 });

        ServiceResult result = await CreateWithAsync(model);

        Assert.True(result.Errors.ContainsKey("Prices"));
    }

    // نفس زوج المحطات مكرر بالأسعار يرفض
    [Fact]
    public async Task CreateLine_DuplicatePricePair_Fails()
    {
        CreateLineViewModel model = TestHelper.ValidLine();
        model.Prices.Add(new LinePriceInput { FromStopId = 1, ToStopId = 2, MonthlyPrice = 35000 });

        ServiceResult result = await CreateWithAsync(model);

        Assert.True(result.Errors.ContainsKey("Prices"));
    }

    // بدون سعر للخط الكامل (1 ← 3) يرفض
    [Fact]
    public async Task CreateLine_NoFullRoutePrice_Fails()
    {
        CreateLineViewModel model = TestHelper.ValidLine();
        model.Prices.RemoveAt(0);

        ServiceResult result = await CreateWithAsync(model);

        Assert.True(result.Errors.ContainsKey("Prices"));
    }

    // ===== خطوط السائق =====

    // كل سائق يشوف خطوطه هو بس، وما يكدر يفتح خط سائق ثاني
    [Fact]
    public async Task DriverLines_OnlyOwnLinesAreVisible()
    {
        Driver driverA = await TestHelper.AddApprovedDriverAsync(_db, 1);
        Driver driverB = await TestHelper.AddApprovedDriverAsync(_db, 2);
        Line lineA = await CreateValidLineAsync(driverA);

        Assert.Single(await _lineService.GetDriverLinesAsync(driverA.UserId));
        Assert.Empty(await _lineService.GetDriverLinesAsync(driverB.UserId));
        Assert.NotNull(await _lineService.GetDriverLineAsync(driverA.UserId, lineA.LineId));
        Assert.Null(await _lineService.GetDriverLineAsync(driverB.UserId, lineA.LineId));
    }

    // ===== إغلاق الخط =====

    // يضيف اشتراك وهمي بالحالة المطلوبة على الخط
    private async Task<Subscription> AddSubscriptionAsync(Line line, string status)
    {
        await new AccountService(_db, new FileUploadService(TestHelper.CreateTempFolder())).RegisterAsync(TestHelper.ValidPassenger());
        User passenger = await _db.Users.SingleAsync(u => u.RoleId == 1);

        Subscription subscription = new Subscription
        {
            LineId = line.LineId, PassengerId = passenger.UserId, PickupStopId = 1, DropoffStopId = 3,
            SeatsCount = 1, MonthlyPrice = 50000, SubscriptionStatus = status, CreatedAt = DateTime.Now,
            StartDate = DateOnly.FromDateTime(DateTime.Today), EndDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1))
        };
        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync();
        return subscription;
    }

    // إغلاق الخط: الحالة Closed، الطلب المعلق ينرفض، والمشترك ياخذ إشعار
    [Fact]
    public async Task CloseLine_RejectsPendingAndNotifies()
    {
        Driver driver = await TestHelper.AddApprovedDriverAsync(_db);
        Line line = await CreateValidLineAsync(driver);
        Subscription pending = await AddSubscriptionAsync(line, "Pending");

        ServiceResult result = await _lineService.CloseLineAsync(driver.UserId, line.LineId);

        Assert.True(result.Success);
        Assert.Equal("Closed", line.LineStatus);
        Assert.Equal("Rejected", pending.SubscriptionStatus);
        Assert.Equal("LineClosed", (await _db.Notifications.SingleAsync()).Type);
    }

    // إغلاق الخط: الاشتراك الفعّال يبقى فعّال لحد نهاية مدته
    [Fact]
    public async Task CloseLine_KeepsActiveSubscription()
    {
        Driver driver = await TestHelper.AddApprovedDriverAsync(_db);
        Line line = await CreateValidLineAsync(driver);
        Subscription active = await AddSubscriptionAsync(line, "Active");

        await _lineService.CloseLineAsync(driver.UserId, line.LineId);

        Assert.Equal("Active", active.SubscriptionStatus);
        Assert.Equal(1, await _db.Notifications.CountAsync());
    }

    // سائق ما يكدر يغلق خط سائق ثاني، وما يكدر يغلق خط مغلق
    [Fact]
    public async Task CloseLine_OtherDriverOrAlreadyClosed_Fails()
    {
        Driver driverA = await TestHelper.AddApprovedDriverAsync(_db, 1);
        Driver driverB = await TestHelper.AddApprovedDriverAsync(_db, 2);
        Line line = await CreateValidLineAsync(driverA);

        ServiceResult byOther = await _lineService.CloseLineAsync(driverB.UserId, line.LineId);
        Assert.False(byOther.Success);
        Assert.Equal("Open", line.LineStatus);

        await _lineService.CloseLineAsync(driverA.UserId, line.LineId);
        ServiceResult again = await _lineService.CloseLineAsync(driverA.UserId, line.LineId);
        Assert.False(again.Success);
    }
}
