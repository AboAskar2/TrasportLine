using Microsoft.EntityFrameworkCore;
using TransportLines.Data;
using TransportLines.Models;
using TransportLines.Services;
using TransportLines.ViewModels;

namespace TransportLines.Tests;

// اختبارات على MySQL الحقيقية، قراءة فقط:
// - كل عملية فيها حفظ ننطيها مدخل يفشل بالفالديشن قبل سطر الحفظ، فكل الاستعلامات تشتغل على MySQL بدون ما ينحفظ شي
// - كل اختبار ياخذ صورة للداتابيس قبل وبعد، ويفشل إذا تغير أي شي
// - تشتغل بس إذا RUN_REAL_DB_TESTS = 1
public class RealDatabaseTests
{
    // ===== البيانات المرجعية =====

    // أنواع السيارات والمحطات (ويا مناطقها) تنقرا من MySQL
    [RealDbFact]
    public async Task Lookups_LoadFromMySql()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext db = RealDbHelper.CreateContext();

        List<Vehicletype> types = await NewAccountService(db).GetActiveVehicleTypesAsync();
        List<Stop> stops = await new LineService(db).GetActiveStopsAsync();

        Assert.NotEmpty(types);
        Assert.NotEmpty(stops);
        Assert.NotNull(stops[0].Area);
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // ===== الحسابات =====

    // دخول بكلمة مرور غلط لرقم موجود، ودخول برقم مو موجود: الاثنين يفشلون بنفس الرسالة وما يكتبون شي
    [RealDbFact]
    public async Task Login_FailedAttempts_DoNotWrite()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext db = RealDbHelper.CreateContext();
        User existing = await db.Users.FirstAsync();
        AccountService service = NewAccountService(db);

        LoginResult wrongPassword = await service.LoginAsync(existing.PhoneNumber, "Wrong12345");
        LoginResult unknownPhone = await service.LoginAsync("07990000000", "Wrong12345");

        Assert.False(wrongPassword.Success);
        Assert.False(unknownPhone.Success);
        Assert.Equal(wrongPassword.ErrorMessage, unknownPhone.ErrorMessage);
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // تسجيل برقم موجود: استعلام فحص التكرار يشتغل على MySQL ويرفض، وما ينحفظ شي
    [RealDbFact]
    public async Task Register_DuplicatePhone_IsCaughtByQuery()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext db = RealDbHelper.CreateContext();
        User existing = await db.Users.FirstAsync();
        RegisterViewModel model = TestHelper.ValidPassenger();
        model.PhoneNumber = existing.PhoneNumber;
        model.Email = "real-db-test@example.com";

        ServiceResult result = await NewAccountService(db).RegisterAsync(model);

        Assert.True(result.Errors.ContainsKey("PhoneNumber"));
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // ===== الأدمن والسائق =====

    // استعلامات الأدمن تشتغل، وراكب يحاول يوافق على سائق ينرفض بدون كتابة
    [RealDbFact]
    public async Task Admin_QueriesRun_AndNonAdminCannotApprove()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext db = RealDbHelper.CreateContext();
        RealSampleData data = await RealDbHelper.LoadSampleDataAsync(db);
        AdminService service = new AdminService(db);

        await service.GetPendingDriversAsync();
        Driver? driver = await service.GetDriverDetailsAsync(data.Line.DriverId);
        ServiceResult approve = await service.ApproveDriverAsync(data.Line.DriverId, data.PassengerUserId);

        Assert.NotNull(driver);
        Assert.NotNull(driver.User);
        Assert.False(approve.Success);
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // ملف السائق وخطوطه وتفاصيل الخط (ويا المحطات والأسعار والأيام) تنقرا من MySQL
    [RealDbFact]
    public async Task Driver_ProfileAndLine_LoadWithIncludes()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext db = RealDbHelper.CreateContext();
        RealSampleData data = await RealDbHelper.LoadSampleDataAsync(db);

        Driver? profile = await new DriverService(db).GetDriverProfileAsync(data.DriverUserId);
        bool isApproved = await new DriverService(db).IsApprovedDriverAsync(data.DriverUserId);
        Line? line = await new LineService(db).GetDriverLineAsync(data.DriverUserId, data.Line.LineId);

        Assert.NotNull(profile);
        Assert.NotEmpty(profile.Vehicles);
        Assert.True(isApproved);
        Assert.NotNull(line);
        Assert.True(line.Linestops.Count >= 2);
        Assert.All(line.Linestops, ls => Assert.NotNull(ls.Stop));
        Assert.NotEmpty(line.Lineprices);
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // ===== الخطوط =====

    // إنشاء خط بدون أيام: كل فحوصات السائق والسيارة والمحطات تشتغل على MySQL، ويرفض قبل الحفظ
    [RealDbFact]
    public async Task CreateLine_NoDays_ChecksRunWithoutSaving()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext db = RealDbHelper.CreateContext();
        RealSampleData data = await RealDbHelper.LoadSampleDataAsync(db);
        CreateLineViewModel model = await BuildLineFromSampleStopsAsync(db, data.Line.LineId);
        model.SelectedDays = new List<int>();

        ServiceResult result = await new LineService(db).CreateLineAsync(data.DriverUserId, model);

        // الخطأ الوحيد لازم يكون الأيام (يعني كل الفحوصات الثانية على MySQL نجحت)
        Assert.Single(result.Errors);
        Assert.True(result.Errors.ContainsKey("SelectedDays"));
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // راكب يحاول يغلق خط مو إله: ينرفض وما يتغير شي
    [RealDbFact]
    public async Task CloseLine_ByOtherUser_DoesNothing()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext db = RealDbHelper.CreateContext();
        RealSampleData data = await RealDbHelper.LoadSampleDataAsync(db);

        ServiceResult result = await new LineService(db).CloseLineAsync(data.PassengerUserId, data.Line.LineId);

        Assert.False(result.Success);
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // ===== الاشتراكات =====

    // البحث يلكى الخط التجريبي من أول محطة لآخر محطة، وما يلكاه بالعكس
    [RealDbFact]
    public async Task Search_FindsSampleLineInRightDirectionOnly()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext db = RealDbHelper.CreateContext();
        RealSampleData data = await RealDbHelper.LoadSampleDataAsync(db);
        List<Linestop> stops = await GetOrderedStopsAsync(db, data.Line.LineId);
        int firstStop = stops[0].StopId;
        int lastStop = stops[stops.Count - 1].StopId;
        SubscriptionService service = new SubscriptionService(db);

        List<Line> forward = await service.SearchLinesAsync(data.PassengerUserId, firstStop, lastStop);
        List<Line> backward = await service.SearchLinesAsync(data.PassengerUserId, lastStop, firstStop);

        Assert.Contains(forward, l => l.LineId == data.Line.LineId);
        Assert.DoesNotContain(backward, l => l.LineId == data.Line.LineId);
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // اشتراكاتي: محطة الصعود والنزول تنقرا صح من MySQL
    // (يتأكد من أسماء الـ scaffold المربكة: LinestopNavigation = الصعود، Linestop = النزول)
    [RealDbFact]
    public async Task MySubscriptions_PickupAndDropoffAreCorrect()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext db = RealDbHelper.CreateContext();
        RealSampleData data = await RealDbHelper.LoadSampleDataAsync(db);

        List<Subscription> subscriptions = await new SubscriptionService(db).GetMySubscriptionsAsync(data.PassengerUserId);

        Assert.NotEmpty(subscriptions);
        foreach (Subscription s in subscriptions)
        {
            Assert.Equal(s.PickupStopId, s.LinestopNavigation.StopId);
            Assert.Equal(s.DropoffStopId, s.Linestop.StopId);
            Assert.NotNull(s.LinestopNavigation.Stop);
            Assert.NotNull(s.Linestop.Stop);
        }
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // طلب اشتراك بصفر مقاعد: كل الفحوصات (الراكب، الخط، السعر، الطلب المفتوح) تشتغل على MySQL، ويرفض قبل الحفظ
    [RealDbFact]
    public async Task RequestSubscription_ZeroSeats_ChecksRunWithoutSaving()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext db = RealDbHelper.CreateContext();
        RealSampleData data = await RealDbHelper.LoadSampleDataAsync(db);
        List<Linestop> stops = await GetOrderedStopsAsync(db, data.Line.LineId);
        RequestSubscriptionViewModel model = new RequestSubscriptionViewModel
        {
            LineId = data.Line.LineId, PickupStopId = stops[0].StopId, DropoffStopId = stops[stops.Count - 1].StopId,
            SeatsCount = 0, StartDate = DateTime.Today.AddDays(1), Months = data.Line.MinSubscriptionMonths
        };

        ServiceResult result = await new SubscriptionService(db).RequestSubscriptionAsync(data.PassengerUserId, model);

        Assert.True(result.Errors.ContainsKey("SeatsCount"));
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // طلبات الخط: صاحب الخط يشوفها، والراكب ما يشوف شي
    [RealDbFact]
    public async Task LineSubscriptions_OnlyOwnerSeesThem()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext db = RealDbHelper.CreateContext();
        RealSampleData data = await RealDbHelper.LoadSampleDataAsync(db);
        SubscriptionApprovalService service = new SubscriptionApprovalService(db);

        List<Subscription> forOwner = await service.GetLineSubscriptionsAsync(data.DriverUserId, data.Line.LineId);
        List<Subscription> forOther = await service.GetLineSubscriptionsAsync(data.PassengerUserId, data.Line.LineId);

        Assert.NotEmpty(forOwner);
        Assert.Empty(forOther);
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // ===== فحص المقاعد والقفل =====

    // حساب المقاعد لكل مقطع على البيانات الحقيقية يطابق الحساب اليدوي:
    // الخط التجريبي (5 ← 6 ← 7) والاشتراكات الفعّالة: 5←7 (1 مقعد)، 5←6 (2 مقعد)، 6←7 (1 مقعد)
    // المقطع 1 (5-6) = 1 + 2 = 3 ، المقطع 2 (6-7) = 1 + 1 = 2
    [RealDbFact]
    public async Task SeatCount_OnSampleLine_MatchesHandCalculation()
    {
        using AppDbContext db = RealDbHelper.CreateContext();
        RealSampleData data = await RealDbHelper.LoadSampleDataAsync(db);
        Dictionary<int, int> stopOrders = new Dictionary<int, int>();
        foreach (Linestop ls in await GetOrderedStopsAsync(db, data.Line.LineId))
            stopOrders[ls.StopId] = ls.StopOrder;
        List<Subscription> active = await db.Subscriptions
            .Where(s => s.LineId == data.Line.LineId && s.SubscriptionStatus == "Active").ToListAsync();

        Assert.Equal(3, SubscriptionApprovalService.CountSeatsOnSegment(active, stopOrders, 1));
        Assert.Equal(2, SubscriptionApprovalService.CountSeatsOnSegment(active, stopOrders, 2));
    }

    // أمر القفل (SELECT ... FOR UPDATE) يشتغل على MySQL بدون أخطاء:
    // راكب يحاول يوافق على طلب: القفل يتنفذ، بعدها يرفض لأنه مو صاحب الخط، والـ Transaction تنلغي
    [RealDbFact]
    public async Task Approve_LockSqlRunsOnMySql()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext db = RealDbHelper.CreateContext();
        RealSampleData data = await RealDbHelper.LoadSampleDataAsync(db);
        Assert.True(data.PendingSubscriptionId != null, "ماكو طلب معلق بالبيانات التجريبية");

        ServiceResult result = await new SubscriptionApprovalService(db).ApproveAsync(data.PassengerUserId, data.PendingSubscriptionId!.Value);

        Assert.False(result.Success);
        Assert.Equal("الطلب غير موجود", result.Errors.Values.First());
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // القفل يشتغل فعلاً: اتصال أول يقفل صف الخط، فالموافقة من اتصال ثاني تنتظر،
    // وأول ما الاتصال الأول يلغي الـ Transaction، الموافقة تكمل
    [RealDbFact]
    public async Task Approve_WaitsWhileLineIsLocked()
    {
        string before = await RealDbHelper.TakeSnapshotAsync();
        using AppDbContext holder = RealDbHelper.CreateContext();
        RealSampleData data = await RealDbHelper.LoadSampleDataAsync(holder);
        Assert.True(data.PendingSubscriptionId != null, "ماكو طلب معلق بالبيانات التجريبية");

        // الاتصال الأول: يفتح Transaction ويقفل صف الخط (بدون ما يكتب شي)
        await using var lockTransaction = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT LineID FROM `lines` WHERE LineID = {data.Line.LineId} FOR UPDATE");

        // الاتصال الثاني: يحاول يوافق، فلازم ينتظر القفل
        using AppDbContext other = RealDbHelper.CreateContext();
        Task<ServiceResult> approveTask = new SubscriptionApprovalService(other).ApproveAsync(data.PassengerUserId, data.PendingSubscriptionId!.Value);
        Task firstFinished = await Task.WhenAny(approveTask, Task.Delay(2000));
        Assert.NotSame(approveTask, firstFinished);

        // نفك القفل: الموافقة تكمل (وترفض لأن الراكب مو صاحب الخط)
        await lockTransaction.RollbackAsync();
        ServiceResult result = await approveTask;
        Assert.False(result.Success);
        await RealDbHelper.AssertNothingChangedAsync(before);
    }

    // ================= أدوات مساعدة =================

    // ينشئ AccountService بمجلد صور مؤقت (ما ينحفظ بيه شي بهذي الاختبارات)
    private static AccountService NewAccountService(AppDbContext db)
    {
        return new AccountService(db, new FileUploadService(TestHelper.CreateTempFolder()));
    }

    // يرجع محطات الخط مرتبة حسب ترتيبها
    private static async Task<List<Linestop>> GetOrderedStopsAsync(AppDbContext db, int lineId)
    {
        return await db.Linestops.Where(ls => ls.LineId == lineId).OrderBy(ls => ls.StopOrder).ToListAsync();
    }

    // يبني فورم خط صحيح من محطات الخط التجريبي (نفس المحطات، وسعر للخط الكامل)
    private static async Task<CreateLineViewModel> BuildLineFromSampleStopsAsync(AppDbContext db, int lineId)
    {
        List<Linestop> stops = await GetOrderedStopsAsync(db, lineId);
        CreateLineViewModel model = new CreateLineViewModel();
        model.DepartureTime = new TimeSpan(7, 0, 0);
        model.TotalSeats = 1;
        model.MinSubscriptionMonths = 1;
        model.SelectedDays = new List<int> { 0 };
        foreach (Linestop ls in stops)
            model.Stops.Add(new LineStopInput { StopId = ls.StopId });
        model.Prices.Add(new LinePriceInput { FromStopId = stops[0].StopId, ToStopId = stops[stops.Count - 1].StopId, MonthlyPrice = 1000 });
        return model;
    }
}
