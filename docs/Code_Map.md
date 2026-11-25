# خريطة الكود (Code Map)

دليل مبسط يشرح شنو يسوي كل كلاس، وكيف تمشي كل ميزة من المتصفح لحد قاعدة البيانات، ووين تروح إذا تريد تعدل شي، وشنو ممكن ينكسر.

> **ملاحظة:** الـ Views غير موجودة بعد (المرحلة 2)، فبالمسارات تحت مكتوب "View" بس كمكان راح ينعرض بيه الناتج.

---

## قبل ما تبدي: الفكرة العامة بـ 4 طبقات

```
المتصفح (الفورم)
   ↓  يرسل طلب HTTP
Controller          ← يستلم الطلب، ياخذ رقم المستخدم من الـ Cookie، ويرسل البيانات للخدمة
   ↓
Service             ← هنا كل المنطق والفالديشن الحقيقي
   ↓
AppDbContext        ← يحول أوامر LINQ لـ SQL
   ↓
MySQL (الجداول)
```

**قاعدتين ثابتتين بكل المشروع:**
1. الـ Controller **ما يفكر**: يستلم، ويرسل للخدمة، ويعرض النتيجة.
2. الخدمة ترجع `ServiceResult`. إذا بيه أخطاء، الـ Controller ينقلها للـ `ModelState` حتى تنعرض جنب الحقول.

---

## 1) الكلاسات: مسؤولية كل واحد

### 1.1 كلاسات مساعدة (تستخدمها كل الخدمات)

#### `ServiceResult`
**الملف:** `TransportLines/Services/ServiceResult.cs`
- **مسؤوليته:** صندوق يحمل أخطاء العملية. المفتاح اسم الحقل (مثل `"PhoneNumber"`)، والقيمة رسالة الخطأ. إذا الصندوق فارغ، معناها `Success = true`.
- **منو يستدعيه:** كل الخدمات تنشئه، وكل الـ Controllers تقرأه.
- **يستقبل:** `AddError(string fieldName, string message)`.
- **يرجع:** `Success` (bool)، و `Errors` (قاموس الأخطاء).

#### `LoginResult`
**الملف:** `TransportLines/Services/LoginResult.cs`
- **مسؤوليته:** نتيجة تسجيل الدخول: نجح لو لا، ورسالة الخطأ، وبيانات المستخدم.
- **منو يستدعيه:** `AccountService.LoginAsync` يرجعه، و `AccountController.Login` يقرأه.
- **يحمل:** `Success`، و `ErrorMessage`، و `UserId`، و `FullName`، و `RoleName`.

#### `UserHelper`
**الملف:** `TransportLines/Security/UserHelper.cs`
- **مسؤوليته:** يقرأ رقم المستخدم الداخل من الـ Cookie، حتى ما ناخذ الرقم من الرابط أبداً.
- **منو يستدعيه:** `AdminController`، و `DriverController`، و `PassengerController`.
- **يستقبل:** `GetUserId(ClaimsPrincipal user)`، وبالـ Controller نرسل `User`.
- **يرجع:** `int`، رقم المستخدم، أو `0` إذا ما موجود.

#### `SecurityHelper`
**الملف:** `TransportLines/Security/SecurityHelper.cs`
- **مسؤوليته:** كل إعدادات الأمان بمكان واحد:
  - الـ Cookie.
  - إن كل صفحة تحتاج دخول.
  - CSRF.
  - حد المحاولات.
  - رؤوس الحماية.
  - فحص الحساب مع كل طلب.
- **منو يستدعيه:** `Program.cs` وقت التشغيل، و `AccountController` يستخدم أسماء السياسات بـ `[EnableRateLimiting]`.

| الدالة | تستقبل | ترجع / تسوي |
|---|---|---|
| `ConfigureCookie` | `CookieAuthenticationOptions` | تضبط اسم الـ Cookie ومدته وحمايته |
| `ConfigureAuthorization` | `AuthorizationOptions` | كل صفحة تحتاج دخول |
| `ConfigureMvc` | `MvcOptions` | فلتر CSRF على كل POST |
| `ConfigureRateLimiter` | `RateLimiterOptions` | تسجل سياستي `"login"` و `"register"` |
| `GetLoginPartition` و `GetRegisterPartition` | `HttpContext` | حد لكل IP |
| `AddSecurityHeaders` | `HttpContext`، `RequestDelegate` | تضيف رؤوس الحماية لكل رد |
| `ValidateCookieAsync` | `CookieValidatePrincipalContext` | تطرد الحساب إذا انوقف أو انحذف |

### 1.2 الخدمات (Services)

#### `FileUploadService`
**الملف:** `TransportLines/Services/FileUploadService.cs`
- **مسؤوليته:** يفحص الصور المرفوعة (الحجم، والامتداد، وأول بايتات الملف)، ويحفظها باسم عشوائي بمجلد `App_Data/uploads`، ويرجعها بمسار آمن.
- **منو يستدعيه:** `AccountService` (تسجيل السائق) و `AdminController` (عرض الصور).

| الدالة | تستقبل | ترجع |
|---|---|---|
| `ValidateImage` | `IFormFile?` | `string?`: رسالة خطأ، أو `null` إذا سليمة |
| `SaveImageAsync` | `IFormFile`، واسم المجلد الفرعي | `string`: المسار النسبي، مثل `licenses/abc123.jpg` |
| `DeleteImage` | المسار النسبي | لا شي (يحذف الملف) |
| `GetSafeFullPath` | المسار النسبي | `string?`: المسار الكامل، أو `null` إذا برا المجلد أو مو موجود |
| `GetContentType` (static) | مسار الملف | `"image/png"` أو `"image/jpeg"` |

#### `AccountService`
**الملف:** `TransportLines/Services/AccountService.cs`
- **مسؤوليته:** تسجيل حساب جديد (راكب، أو سائق مع سيارته وصوره)، وتسجيل الدخول بـ bcrypt. وأدوات للكونترولر (الـ Claims، وحالة الحساب، وأنواع السيارات).
- **منو يستدعيه:** `AccountController`، و `SecurityHelper.ValidateCookieAsync`.
- **يعتمد على:** `AppDbContext`، و `FileUploadService`.

| الدالة (public) | تستقبل | ترجع |
|---|---|---|
| `RegisterAsync` | `RegisterViewModel` | `ServiceResult` |
| `LoginAsync` | رقم الهاتف، وكلمة المرور | `LoginResult` |
| `IsStrongPassword` (static) | كلمة المرور | `bool` |
| `CreatePrincipal` (static) | `LoginResult` | `ClaimsPrincipal` (هوية المستخدم للـ Cookie) |
| `IsUserActiveAsync` | `userId` | `bool` |
| `GetActiveVehicleTypesAsync` | — | `List<Vehicletype>` |

#### `AdminService`
**الملف:** `TransportLines/Services/AdminService.cs`
- **مسؤوليته:** يعرض السائقين المعلقين، ويوافق عليهم أو يرفضهم مع إشعار، ويرجع مسار صور وثائقهم.
- **منو يستدعيه:** `AdminController`.

| الدالة | تستقبل | ترجع |
|---|---|---|
| `GetPendingDriversAsync` | — | `List<Driver>` |
| `GetDriverDetailsAsync` | `driverId` | `Driver?` |
| `ApproveDriverAsync` | `driverId`، `adminUserId` | `ServiceResult` |
| `RejectDriverAsync` | `driverId`، `adminUserId`، `reason` | `ServiceResult` |
| `GetDriverPhotoPathAsync` | `driverId`، `photoType` | `string?` |

#### `DriverService`
**الملف:** `TransportLines/Services/DriverService.cs`
- **مسؤوليته:** ملف السائق وحالة موافقته.
- **منو يستدعيه:** `DriverController`.

| الدالة | تستقبل | ترجع |
|---|---|---|
| `GetDriverProfileAsync` | `userId` | `Driver?` |
| `IsApprovedDriverAsync` | `userId` | `bool` |
| `GetStatusMessage` (static) | `Driver` | `string` (رسالة عربية) |

#### `LineService`
**الملف:** `TransportLines/Services/LineService.cs`
- **مسؤوليته:** إنشاء خط (محطات، أيام، أسعار) بعد الفحص، وعرض خطوط السائق، وإغلاق خط.
- **منو يستدعيه:** `DriverController`، و `PassengerController` (بس لقائمة المحطات).

| الدالة | تستقبل | ترجع |
|---|---|---|
| `GetActiveStopsAsync` | — | `List<Stop>` |
| `CreateLineAsync` | `userId`، `CreateLineViewModel` | `ServiceResult` |
| `GetDriverLinesAsync` | `userId` | `List<Line>` |
| `GetDriverLineAsync` | `userId`، `lineId` | `Line?` (null إذا الخط مو إله) |
| `CloseLineAsync` | `userId`، `lineId` | `ServiceResult` |

#### `SubscriptionService`
**الملف:** `TransportLines/Services/SubscriptionService.cs`
- **مسؤوليته:** جهة الراكب: البحث عن خطوط، وطلب اشتراك (Pending)، واشتراكاتي، والإلغاء.
- **منو يستدعيه:** `PassengerController`.

| الدالة | تستقبل | ترجع |
|---|---|---|
| `SearchLinesAsync` | `userId`، `fromStopId?`، `toStopId?` | `List<Line>` |
| `GetOpenLineAsync` | `userId`، `lineId` | `Line?` |
| `RequestSubscriptionAsync` | `userId`، `RequestSubscriptionViewModel` | `ServiceResult` |
| `FindPrice` (static) | `Line`، من محطة، إلى محطة | `Lineprice?` |
| `CalculateEndDate` (static) | تاريخ البداية، وعدد الأشهر | `DateOnly` |
| `GetMySubscriptionsAsync` | `userId` | `List<Subscription>` |
| `CancelSubscriptionAsync` | `userId`، `subscriptionId`، `reason` | `ServiceResult` |

#### `SubscriptionApprovalService`
**الملف:** `TransportLines/Services/SubscriptionApprovalService.cs`
- **مسؤوليته:** جهة السائق: يوافق على الطلبات بعد قفل الخط وفحص المقاعد لكل مقطع، أو يرفضها.
- **منو يستدعيه:** `DriverController`.

| الدالة | تستقبل | ترجع |
|---|---|---|
| `GetLineSubscriptionsAsync` | `userId`، `lineId` | `List<Subscription>` |
| `ApproveAsync` | `userId`، `subscriptionId` | `ServiceResult` |
| `RejectAsync` | `userId`، `subscriptionId`، `reason` | `ServiceResult` |
| `CountSeatsOnSegment` (static) | قائمة الاشتراكات، وجدول ترتيب المحطات، ورقم المقطع | `int` (المقاعد المحجوزة) |

### 1.3 الـ Controllers

كل Controller يستقبل الخدمات اللي يحتاجها بالـ Constructor، وهذا يسمى Dependency Injection: `Program.cs` هو اللي يجهزها ويرسلها.

#### `HomeController`
**الملف:** `TransportLines/Controllers/HomeController.cs`
- **مسؤوليته:** الصفحة الرئيسية، والخصوصية، وصفحة الخطأ. كلها عامة (`[AllowAnonymous]`).
- **منو يستدعيه:** المتصفح مباشرة.
- **يرجع:** `View()`. صفحة الخطأ ترجع `View(ErrorViewModel)`.

#### `AccountController`
**الملف:** `TransportLines/Controllers/AccountController.cs`
- **مسؤوليته:** التسجيل، والدخول، والخروج، وصفحة "ما عندك صلاحية".
- **يعتمد على:** `AccountService`.

| الـ Action | يستقبل | يرجع |
|---|---|---|
| `Register` (GET) | — | `View(RegisterViewModel)`، و `ViewBag.VehicleTypes` |
| `Register` (POST) | `RegisterViewModel` | تحويل لـ `Login`، أو نفس الفورم ويا الأخطاء |
| `Login` (GET) | `returnUrl?` | `View(LoginViewModel)` |
| `Login` (POST) | `LoginViewModel`، `returnUrl?` | تحويل للصفحة المطلوبة أو الرئيسية، أو نفس الفورم |
| `Logout` (POST) | — | تحويل للرئيسية |
| `AccessDenied` | — | `View()` |

#### `AdminController` (للأدمن فقط)
**الملف:** `TransportLines/Controllers/AdminController.cs`
- **مسؤوليته:** مراجعة السائقين وعرض وثائقهم.
- **يعتمد على:** `AdminService`، و `FileUploadService`.

| الـ Action | يستقبل | يرجع |
|---|---|---|
| `PendingDrivers` | — | `View(List<Driver>)` |
| `DriverDetails` | `id` | `View(Driver)`، و `ViewBag.RejectForm`، أو 404 |
| `ApproveDriver` (POST) | `id` | تحويل لـ `PendingDrivers` ويا رسالة |
| `RejectDriver` (POST) | `RejectDriverViewModel` | تحويل ويا رسالة |
| `DriverPhoto` | `id`، `type` | الصورة نفسها (`PhysicalFile`)، أو 404 |

#### `DriverController` (للسائق فقط)
**الملف:** `TransportLines/Controllers/DriverController.cs`
- **مسؤوليته:** ملف السائق، وخطوطه، وطلبات الاشتراك على خطوطه.
- **يعتمد على:** `DriverService`، و `LineService`، و `SubscriptionApprovalService`.

| الـ Action | يستقبل | يرجع |
|---|---|---|
| `Profile` | — | `View(Driver)`، و `ViewBag.StatusMessage` |
| `MyLines` | — | `View(List<Line>)` |
| `LineDetails` | `id` | `View(Line)`، أو 404 |
| `CreateLine` (GET) | — | `View(CreateLineViewModel)`، و `ViewBag.Stops`، أو تحويل لـ `Profile` إذا مو موافق عليه |
| `CreateLine` (POST) | `CreateLineViewModel` | تحويل لـ `MyLines`، أو نفس الفورم |
| `CloseLine` (POST) | `id` | تحويل لـ `MyLines` ويا رسالة |
| `LineRequests` | `id` (رقم الخط) | `View(List<Subscription>)`، و `ViewBag.Line` |
| `ApproveRequest` (POST) | `id` (رقم الطلب)، `lineId` | تحويل لـ `LineRequests` ويا رسالة |
| `RejectRequest` (POST) | `RejectRequestViewModel` | تحويل لـ `LineRequests` ويا رسالة |

#### `PassengerController` (للراكب فقط)
**الملف:** `TransportLines/Controllers/PassengerController.cs`
- **مسؤوليته:** البحث، وطلب الاشتراك، واشتراكاتي، والإلغاء.
- **يعتمد على:** `SubscriptionService`، و `LineService`.

| الـ Action | يستقبل | يرجع |
|---|---|---|
| `SearchLines` | `fromStopId?`، `toStopId?` | `View(List<Line>)`، و `ViewBag.Stops` |
| `LineDetails` | `id` | `View(Line)`، أو 404 |
| `RequestSubscription` (GET) | `lineId` | `View(RequestSubscriptionViewModel)`، و `ViewBag.LineStops` و `ViewBag.Line` |
| `RequestSubscription` (POST) | `RequestSubscriptionViewModel` | تحويل لـ `MySubscriptions`، أو نفس الفورم |
| `MySubscriptions` | — | `View(List<Subscription>)` |
| `CancelSubscription` (POST) | `id`، `reason?` | تحويل لـ `MySubscriptions` ويا رسالة |

### 1.4 الـ ViewModels (شكل الفورمات)

الـ ViewModel هو كلاس يطابق حقول الفورم. ASP.NET يملي خصائصه تلقائياً من الطلب، وهذا يسمى Model Binding، ويفحص الـ Attributes مثل `[Required]` قبل ما يوصل للـ Action.

| الكلاس | الملف | مسؤوليته | منو يستخدمه |
|---|---|---|---|
| `RegisterViewModel` | `ViewModels/RegisterViewModel.cs` | فورم التسجيل: حقول مشتركة، وحقول السائق، وحقول السيارة، و4 صور | `AccountController.Register` ← `AccountService.RegisterAsync` |
| `LoginViewModel` | `ViewModels/LoginViewModel.cs` | الهاتف، وكلمة المرور، وتذكرني | `AccountController.Login` |
| `RejectDriverViewModel` | `ViewModels/RejectDriverViewModel.cs` | رقم السائق، وسبب الرفض | `AdminController.RejectDriver` |
| `CreateLineViewModel` | `ViewModels/CreateLineViewModel.cs` | بيانات الخط، والأيام، وقائمة `LineStopInput`، وقائمة `LinePriceInput` | `DriverController.CreateLine` ← `LineService.CreateLineAsync` |
| `LineStopInput` | نفس الملف | صف محطة: `StopId`، ووقت الوصول | داخل `CreateLineViewModel` |
| `LinePriceInput` | نفس الملف | صف سعر: من، وإلى، والسعر | داخل `CreateLineViewModel` |
| `RequestSubscriptionViewModel` | `ViewModels/RequestSubscriptionViewModel.cs` | الخط، والصعود، والنزول، والمقاعد، والبداية، والأشهر | `PassengerController.RequestSubscription` ← `SubscriptionService` |
| `RejectRequestViewModel` | `ViewModels/RejectRequestViewModel.cs` | رقم الطلب، ورقم الخط، والسبب | `DriverController.RejectRequest` |
| `ErrorViewModel` | `ViewModels/ErrorViewModel.cs` | رقم الطلب لصفحة الخطأ | `HomeController.Error` |

---

## 2) مسار كل ميزة من البداية للنهاية

### 2.0 شنو يصير مع **كل** طلب قبل ما يوصل للـ Controller

```
المتصفح
  ← SecurityHelper.AddSecurityHeaders        (يضيف رؤوس الحماية)
  ← UseHttpsRedirection                      (http ← https)
  ← UseRouting                               (يحدد أي Controller و Action)
  ← UseRateLimiter                           (إذا الـ Action عليه حد محاولات)
  ← UseAuthentication
       ← SecurityHelper.ValidateCookieAsync
            ← AccountService.IsUserActiveAsync ← AppDbContext ← جدول Users
  ← UseAuthorization                         (هل مسموح؟ دخول + الدور الصحيح)
  ← فلتر CSRF (بالـ POST فقط)
  ← الـ Controller
```

### 2.1 تسجيل راكب

```
المتصفح (فورم التسجيل، اختار Passenger)
  ← [حد المحاولات "register" + فحص CSRF]
  ← AccountController.Register (POST)            ← يفحص ModelState (الـ Attributes)
  ← AccountService.RegisterAsync
       ← CleanInput                              (ينظف المدخلات)
       ← ValidateBasicFields
            ← ValidateNameAndContact
            ← ValidatePassword ← IsStrongPassword
       ← CheckUniqueUserFieldsAsync ← AppDbContext ← جدول Users  (تكرار الهاتف والإيميل)
       ← SaveAccountAsync
            ← SavePassengerAsync
                 ← GetRoleIdAsync ← جدول Roles
                 ← CreateUser                    (BCrypt.HashPassword)
                 ← AppDbContext.SaveChangesAsync ← جدول Users
  ← AccountController.RedirectAfterRegister ← تحويل لـ Login ويا رسالة نجاح
```

### 2.2 تسجيل سائق (مع السيارة والصور)

```
المتصفح (فورم التسجيل، اختار Driver + صور)
  ← [حد المحاولات + CSRF + حد حجم الطلب 10 ميگا]
  ← AccountController.Register (POST)
  ← AccountService.RegisterAsync
       ← CleanInput ← ValidateBasicFields ← CheckUniqueUserFieldsAsync
       ← ValidateDriverAsync
            ← ValidateDriverFields               (الإجازة، والبطاقة)
            ← ValidateVehicleFieldsAsync
                 ← ValidateVehicleTypeAndSeatsAsync ← جدول VehicleTypes
            ← ValidateDriverPhotos ← AddPhotoError ← FileUploadService.ValidateImage
            ← CheckUniqueDriverFieldsAsync ← جداول Drivers و Vehicles
       ← SaveAccountAsync ← SaveDriverAsync
            ← CreateUser
            ← CreateDriverAsync  ← SavePhotoAsync ← FileUploadService.SaveImageAsync ← App_Data/uploads/licenses و ids
            ← CreateVehicleAsync ← SavePhotoAsync ← FileUploadService.SaveImageAsync ← App_Data/uploads/vehicles
            ← SaveChangesAsync (وحدة) ← جداول Users + Drivers + Vehicles
            ← (إذا فشل) DeletePhotos ← FileUploadService.DeleteImage
  ← تحويل لـ Login ويا رسالة "حسابك ينتظر موافقة الإدارة"
```

### 2.3 تسجيل الدخول

```
المتصفح (فورم الدخول)
  ← [حد المحاولات "login": 5 بالدقيقة + CSRF]
  ← AccountController.Login (POST)
  ← AccountService.LoginAsync ← AppDbContext ← جدولي Users + Roles
       ← BCrypt.Verify                           (أو DummyHash إذا الرقم مو موجود)
       ← UpdateLastLoginAsync ← جدول Users (LastLoginAt)
       ← Succeed أو Fail ← LoginResult
  ← AccountController.SignInUserAsync
       ← AccountService.CreatePrincipal          (الرقم، والاسم، والدور)
       ← HttpContext.SignInAsync                 (ينشئ Cookie اسمه TransportLines.Auth)
  ← AccountController.RedirectToLocal            (رابط داخلي فقط)
```

### 2.4 تسجيل الخروج

```
المتصفح (زر خروج، POST)
  ← AccountController.Logout ← HttpContext.SignOutAsync (يمسح الـ Cookie) ← تحويل للرئيسية
```

### 2.5 الأدمن يوافق على سائق أو يرفضه

```
المتصفح (الأدمن)
  ← AdminController.PendingDrivers ← AdminService.GetPendingDriversAsync ← جداول Drivers + Users + Vehicles + VehicleTypes
  ← AdminController.DriverDetails  ← AdminService.GetDriverDetailsAsync

الموافقة:
  ← AdminController.ApproveDriver (POST) ← UserHelper.GetUserId (رقم الأدمن من الـ Cookie)
  ← AdminService.ApproveDriverAsync
       ← FindPendingDriverAsync
            ← CheckAdminAsync ← IsAdminAsync ← جداول Users + Roles
            ← جدول Drivers (لازم Pending)
       ← فحص الإجازة سارية
       ← SetVerification ← AddNotification
       ← SaveChangesAsync ← جدولي Drivers + Notifications
  ← AdminController.SetResultMessage ← تحويل لـ PendingDrivers

الرفض:
  ← AdminController.RejectDriver (POST، RejectDriverViewModel)
  ← AdminService.RejectDriverAsync (يفحص السبب أولاً، ثم نفس الخطوات، والحالة تصير Rejected)
```

### 2.6 الأدمن يشوف صورة وثيقة

```
المتصفح: /Admin/DriverPhoto/7?type=license
  ← AdminController.DriverPhoto
  ← AdminService.GetDriverPhotoPathAsync ← جداول Drivers + Vehicles   (قائمة أنواع مسموحة فقط)
  ← FileUploadService.GetSafeFullPath                                (يمنع ../)
  ← PhysicalFile(...) + Cache-Control: no-store ← الصورة من App_Data/uploads
```

### 2.7 ملف السائق

```
المتصفح ← DriverController.Profile ← UserHelper.GetUserId
  ← DriverService.GetDriverProfileAsync ← جداول Drivers + Users + Vehicles + VehicleTypes
  ← DriverService.GetStatusMessage ← View
```

### 2.8 إنشاء خط

```
المتصفح (فورم الخط)
  GET:  DriverController.CreateLine ← DriverService.IsApprovedDriverAsync
                                    ← LoadStopsAsync ← LineService.GetActiveStopsAsync ← جدولي Stops + Areas
  POST: DriverController.CreateLine
  ← LineService.CreateLineAsync
       ← CleanInput
       ← GetApprovedDriverAsync ← جدولي Drivers + Vehicles
       ← GetActiveVehicle                        (أول سيارة فعّالة فحصها ساري)
       ← ValidateLineInfo                        (المقاعد ≤ مقاعد السيارة)
       ← ValidateDays
       ← ValidateStopsAsync ← GetStopIds ← CheckStopsExistAsync (جدول Stops) ← ValidateArrivalTimes
       ← ValidatePrices ← BuildStopOrders ← CheckOnePrice ← CheckFullRoutePrice
       ← SaveLineAsync
            ← BuildLine ← AddStops ← AddDays ← AddPrices
            ← SaveChangesAsync (وحدة) ← جداول Lines + LineStops + LineDays + LinePrices
  ← تحويل لـ MyLines
```

### 2.9 إغلاق خط

```
المتصفح (زر إغلاق، POST) ← DriverController.CloseLine
  ← LineService.CloseLineAsync ← جدول Lines (لازم يخص السائق ومو مغلق)
       ← HandleOpenSubscriptionsAsync ← جدول Subscriptions (المعلقة تصير Rejected)
            ← AddLineClosedNotification ← جدول Notifications
       ← SaveChangesAsync
  ← تحويل لـ MyLines
```

### 2.10 البحث عن خط (الراكب)

```
المتصفح: /Passenger/SearchLines?fromStopId=1&toStopId=3
  ← PassengerController.SearchLines
  ← SubscriptionService.SearchLinesAsync
       ← IsFemaleAsync ← جدول Users
       ← GetOpenLinesAsync ← LinesWithDetails ← جداول Lines + Drivers + Users + Vehicles + LineStops + Stops + LineDays + LinePrices
       ← FindPrice ← GetStopOrder                (لكل خط: الترتيب صحيح + أكو سعر)
  ← LoadAllStopsAsync ← LineService.GetActiveStopsAsync
  ← View
```

### 2.11 طلب اشتراك

```
المتصفح (فورم الطلب)
  GET:  PassengerController.RequestSubscription ← SubscriptionService.GetOpenLineAsync ← LoadLineStops
  POST: PassengerController.RequestSubscription
  ← SubscriptionService.RequestSubscriptionAsync
       ← GetActivePassengerAsync ← جدولي Users + Roles
       ← FindLineForRequestAsync ← GetOpenLineAsync  (النسائي للراكبات فقط)
       ← ValidateStopsAndPrice ← FindPrice
       ← ValidateSeatsAndDates
       ← CheckNoOpenSubscriptionAsync ← جدول Subscriptions
       ← SaveRequestAsync
            ← CalculateEndDate
            ← AddNotification (للسائق)
            ← SaveChangesAsync ← جدولي Subscriptions + Notifications
  ← تحويل لـ MySubscriptions
```

### 2.12 إلغاء اشتراك (الراكب)

```
المتصفح (زر إلغاء، POST) ← PassengerController.CancelSubscription
  ← SubscriptionService.CancelSubscriptionAsync
       ← فحص طول السبب
       ← FindOpenSubscriptionAsync ← جداول Subscriptions + Lines + Drivers  (بالرقم ورقم الراكب سوية)
       ← Status = Cancelled ← AddNotification (للسائق)
       ← SaveChangesAsync
  ← تحويل لـ MySubscriptions
```

### 2.13 السائق يوافق على طلب (أهم مسار)

```
المتصفح (زر موافقة، POST) ← DriverController.ApproveRequest
  ← SubscriptionApprovalService.ApproveAsync
       ← BeginTransactionAsync                         (بداية Transaction)
       ← LockLineAsync ← SELECT ... FOR UPDATE ← جدولي lines + subscriptions  (قفل)
       ← FindPendingRequestAsync ← CheckRequestState ← جداول Subscriptions + Lines + Drivers
       ← CheckSeatsAsync
            ← GetStopOrdersAsync ← جدول LineStops
            ← GetOverlappingActiveAsync ← جدول Subscriptions
            ← (لكل مقطع) CountSeatsOnSegment ← AddNoSeatsError (إذا ماكو مكان)
       ← ActivateSubscription ← AddNotification (للراكب)
       ← SaveChangesAsync ← CommitAsync               (نهاية Transaction)
  ← SetResultMessage ← تحويل لـ LineRequests
```

### 2.14 السائق يرفض طلب

```
المتصفح (فورم الرفض، POST) ← DriverController.RejectRequest (RejectRequestViewModel)
  ← SubscriptionApprovalService.RejectAsync
       ← فحص السبب ← FindPendingRequestAsync ← Status = Rejected ← AddNotification
       ← SaveChangesAsync ← جدولي Subscriptions + Notifications
  ← تحويل لـ LineRequests
```

---

## 3) جدول "إذا أريد أعدل... أروح لـ..."

### 3.1 الحسابات وكلمات المرور

| أريد أعدل... | أروح لـ... |
|---|---|
| شروط كلمة المرور (الطول، والحروف المطلوبة) | `Services/AccountService.cs`، دالة `IsStrongPassword`، و `ViewModels/RegisterViewModel.cs`، خاصية `Password` (`[StringLength]`)، ورسالة الخطأ بـ `AccountService.ValidatePassword`. وبعدها اختبار `Register_WeakPassword_IsRejected` |
| قوة تشفير bcrypt | `AccountService.cs`، الثابت `BcryptWorkFactor` |
| صيغة رقم الهاتف | `RegisterViewModel.PhoneNumber` و `LoginViewModel.PhoneNumber` (`[RegularExpression]`)، و `AccountService.ValidateNameAndContact` |
| شروط الاسم | `RegisterViewModel.FullName`، و `AccountService.ValidateNameAndContact` |
| **إضافة حقل جديد بفورم التسجيل** | 1. تأكد إن العمود موجود بالجدول. إذا مو موجود: **وقف واسأل** (قاعدة تغيير الداتابيس) 2. `RegisterViewModel.cs`: أضف الخاصية ويا الـ Attributes 3. `AccountService.CleanInput`: نظفه 4. دالة الفحص المناسبة: `ValidateBasicFields`، أو `ValidateDriverFields`، أو `ValidateVehicleFieldsAsync` 5. دالة الحفظ: `CreateUser`، أو `CreateDriverAsync`، أو `CreateVehicleAsync` 6. `TestHelper.ValidPassenger` أو `ValidDriver` 7. اختبار جديد بـ `AccountServiceRegisterTests` 8. الـ View (المرحلة 2) |
| رسالة "رقم الهاتف أو كلمة المرور غير صحيحة" | `AccountService.cs`، الثابت `WrongLoginMessage`، واختبارات `AccountServiceLoginTests` (تقارن نفس النص) |
| رسائل نجاح التسجيل | `AccountController.RedirectAfterRegister` |

### 3.2 الأمان

| أريد أعدل... | أروح لـ... |
|---|---|
| عدد محاولات الدخول أو مدتها | `Security/SecurityHelper.cs`، دالة `CreateLoginLimitOptions` |
| عدد محاولات التسجيل | `SecurityHelper.CreateRegisterLimitOptions` |
| مدة بقاء الدخول (8 ساعات) | `SecurityHelper.ConfigureCookie`، السطر `ExpireTimeSpan` |
| صفحات الدخول و"ما عندك صلاحية" | `SecurityHelper.ConfigureCookie`: `LoginPath` و `AccessDeniedPath` |
| رؤوس الحماية أو الـ CSP (مثلاً إذا أضفنا مكتبة من CDN) | `SecurityHelper.AddSecurityHeaders` |
| أخلي صفحة عامة بدون دخول | ضع `[AllowAnonymous]` على الـ Action أو الـ Controller |
| أقصر صفحة على دور معين | `[Authorize(Roles = "...")]` |

### 3.3 رفع الصور

| أريد أعدل... | أروح لـ... |
|---|---|
| أقصى حجم للصورة | `FileUploadService.MaxFileSize`، و `AccountController.MaxRegisterRequestSize` (حجم الطلب الكلي) |
| أنواع الصور المسموحة | `FileUploadService.ValidateImage` (الامتداد)، و `HasImageSignature` (البايتات)، و `GetContentType` |
| مجلد الحفظ | `Program.cs`، المتغير `uploadFolder` |
| أنواع صور الوثائق اللي يعرضها الأدمن | `AdminService.GetDriverPhotoPathAsync` |

### 3.4 الأدمن والسائق

| أريد أعدل... | أروح لـ... |
|---|---|
| طول سبب رفض السائق | `AdminService.MinReasonLength` و `MaxReasonLength`، و `RejectDriverViewModel.Reason` (`[StringLength]`) |
| شرط الإجازة السارية عند الموافقة | `AdminService.ApproveDriverAsync` |
| نص إشعار الموافقة أو الرفض للسائق | `AdminService.ApproveDriverAsync` و `BuildRejectMessage` |
| رسالة حالة حساب السائق | `DriverService.GetStatusMessage` |

### 3.5 الخطوط

| أريد أعدل... | أروح لـ... |
|---|---|
| أقل وأكثر عدد محطات | `LineService.MinStops` و `MaxStops` |
| أعلى سعر | `LineService.MaxPrice`، و `LinePriceInput.MonthlyPrice` (`[Range]`) |
| أشيل شرط "سعر الخط الكامل إجباري" | `LineService.ValidatePrices`: احذف سطر `CheckFullRoutePrice(model, result)`، واحذف اختبار `CreateLine_NoFullRoutePrice_Fails` |
| طريقة اختيار السيارة للخط | `LineService.GetActiveVehicle` |
| حدود أقل مدة اشتراك (1 إلى 12) | `LineService.ValidateLineInfo`، و `CreateLineViewModel.MinSubscriptionMonths` (`[Range]`) |
| شنو يصير للمشتركين عند الإغلاق | `LineService.HandleOpenSubscriptionsAsync` و `AddLineClosedNotification` |
| قائمة المحطات بالفورم (شكل النص) | `DriverController.LoadStopsAsync` و `PassengerController.LoadAllStopsAsync` |

### 3.6 الاشتراكات

| أريد أعدل... | أروح لـ... |
|---|---|
| أبعد تاريخ بداية (60 يوم) | `SubscriptionService.MaxDaysBeforeStart` |
| أكثر عدد أشهر (12) | `SubscriptionService.ValidateSeatsAndDates`، و `RequestSubscriptionViewModel.Months` (`[Range]`) |
| قاعدة الخط النسائي | `SubscriptionService.GetOpenLinesAsync` و `IsFemaleAsync` |
| طريقة حساب تاريخ النهاية | `SubscriptionService.CalculateEndDate`. **انتبه:** لازم تبقى مطابقة لقيد `chk_subscriptions_period` بالداتابيس |
| شروط البحث | `SubscriptionService.SearchLinesAsync` و `GetOpenLinesAsync` |
| طريقة حساب المقاعد لكل مقطع | `SubscriptionApprovalService.CheckSeatsAsync` و `CountSeatsOnSegment` و `GetOverlappingActiveAsync` |
| الشروط قبل موافقة السائق | `SubscriptionApprovalService.CheckRequestState` |
| طول سبب رفض الطلب | `SubscriptionApprovalService.MinReasonLength` و `MaxReasonLength`، و `RejectRequestViewModel.Reason` |
| نصوص إشعارات الاشتراكات | `SubscriptionService.SaveRequestAsync` و `CancelSubscriptionAsync`، و `SubscriptionApprovalService.ActivateSubscription` و `RejectAsync` |

### 3.7 البنية العامة

| أريد أعدل... | أروح لـ... |
|---|---|
| أضيف خدمة جديدة | 1. كلاس جديد بـ `Services/` يستقبل `AppDbContext` بالـ Constructor 2. `Program.cs`: `builder.Services.AddScoped<الخدمة>();` 3. أضفها لـ Constructor الـ Controller اللي يحتاجها 4. ملف اختبار جديد بـ `TransportLines.Tests/` |
| أضيف Controller جديد | ملف بـ `Controllers/` عليه `[Authorize(Roles = "...")]`، وكل POST عليه `[ValidateAntiForgeryToken]`، ورقم المستخدم من `UserHelper.GetUserId(User)` |
| سلسلة الاتصال | ما بالكود: `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."` |
| نسخة MySQL | `Program.cs` (`serverVersion`)، و `TransportLines.Tests/DatabaseConnectionTests.cs` (دالة `CreateContext`) |
| عمود أو جدول بالداتابيس | **وقف واسأل أولاً** (CLAUDE.md). بعد الموافقة: سكربت بـ `sql/`، وبعدها `dotnet tool run dotnet-ef dbcontext scaffold ... --force`، وبعدها build + test |
| بيانات الاختبار (محطات، أنواع سيارات) | `TestHelper.SeedLookupData` |

---

## 4) شنو ينكسر إذا عدلت كلاس معين؟

**قاعدة ذهبية:** بعد أي تعديل شغّل `dotnet build` و `dotnet test`. الـ build يكشف الأخطاء اللي بالأسماء، والاختبارات تكشف الأخطاء اللي بالمنطق. لكن أكو أشياء **ما يكشفها ولا واحد منهم**، ومكتوبة تحت بعلامة ⚠️.

### 4.1 الكلاسات المشتركة (تأثيرها على كل شي)

| إذا عدلت... | شنو ينكسر |
|---|---|
| `ServiceResult` (اسم `Errors` أو `AddError` أو `Success`) | **كل** الخدمات و**كل** الـ Controllers، وأغلب الاختبارات. الـ build يكشفها |
| مفاتيح الأخطاء النصية بـ `AddError("PhoneNumber", ...)` | ⚠️ إذا غيرت اسم خاصية بالـ ViewModel وما غيرت النص، الخطأ ما ينعرض جنب الحقل بالـ View، والـ build ما يكشفها. الاختبارات تكشفها لأنها تفحص `Errors.ContainsKey("...")` |
| `UserHelper.GetUserId` | كل صفحات الأدمن والسائق والراكب، لأنها كلها تعتمد عليه لمعرفة المستخدم |
| `AccountService.CreatePrincipal` (نوع الـ Claims) | ⚠️ إذا غيرت `ClaimTypes.NameIdentifier`، ينكسر `UserHelper` (يرجع 0، فكل الصفحات تعتبر المستخدم مو موجود). وإذا غيرت `ClaimTypes.Role`، تنكسر كل `[Authorize(Roles=...)]` |
| `SecurityHelper.ValidateCookieAsync` | إذا انكسرت، ممكن تطرد كل المستخدمين مع كل طلب، أو ما تطرد الحسابات الموقوفة |
| `SecurityHelper.AddSecurityHeaders` (الـ CSP) | ⚠️ إذا شددته، ممكن تنمنع ملفات CSS/JS بالواجهة (المرحلة 2). وإذا خففته، تقل الحماية من XSS |
| `Program.cs` (حذف `AddScoped<...>`) | ⚠️ الـ build ينجح، لكن الصفحة **تطيح وقت التشغيل** بخطأ "Unable to resolve service". الاختبارات ما تكشفها لأنها تنشئ الخدمات يدوياً |
| ترتيب الـ Middleware بـ `Program.cs` | ⚠️ مثلاً إذا حطيت `UseAuthorization` قبل `UseAuthentication`، تنرفض كل الصفحات المحمية. والاختبارات ما تكشفها |

### 4.2 التسجيل والدخول

| إذا عدلت... | شنو ينكسر |
|---|---|
| اسم خاصية بـ `RegisterViewModel` | `AccountService` (الـ build يكشفه)، و `TestHelper`، ومفاتيح `AddError` (⚠️ شوف فوق)، وأسماء الحقول بالـ View (المرحلة 2) |
| تسمية خاصية `Model` | ⚠️ صار هذا فعلاً بالمشروع: خاصية اسمها `Model` تتعارض ويا بارامتر `model` بالـ Controller، وتخرب ربط بيانات الفورم. لهذا اسمها `VehicleModel` |
| `AccountService.CleanInput` (مثلاً شلت `ToLower` من الإيميل) | فحص تكرار الإيميل يصير حساس للحروف، فنفس الإيميل ينسجل مرتين بحروف مختلفة. اختبار `Register_DuplicateEmailDifferentCase_IsRejected` يكشفها |
| `AccountService.IsStrongPassword` | الحسابات القديمة تبقى تشتغل، لأن الدخول ما يفحص قوة كلمة المرور، بس الجديدة تتأثر |
| `AccountService.LoginAsync` (ترتيب فحص الحالة) | إذا فحصت `AccountStatus` قبل كلمة المرور، تكشف لأي أحد إن الرقم مسجل وموقوف |
| أسماء الأدوار `"Passenger"` و `"Driver"` و `"Admin"` بالكود | ⚠️ لازم تطابق جدول `Roles` بالداتابيس. `GetRoleIdAsync` يطيح إذا الاسم مو موجود |
| `FileUploadService.SaveImageAsync` (شكل المسار) | ⚠️ الصور القديمة محفوظة بالداتابيس بالشكل القديم (`licenses/xxx.jpg`)، فـ `GetSafeFullPath` ما يلكاها |

### 4.3 الأدمن والسائق

| إذا عدلت... | شنو ينكسر |
|---|---|
| قيم الحالة `"Pending"` و `"Approved"` و `"Rejected"` | ⚠️ لازم تطابق قيد `chk_drivers_status` بالداتابيس، وإلا الحفظ يطيح. وتنكسر `DriverService.IsApprovedDriverAsync` و `LineService.GetApprovedDriverAsync` و `SubscriptionService` و `SubscriptionApprovalService`، لأنها كلها تقارن نفس النصوص |
| `AdminService.GetDriverPhotoPathAsync` | عرض الصور بـ `AdminController.DriverPhoto`. وإذا قبلت أي نص بدل القائمة الثابتة، ترجع ثغرة Path Traversal |
| `DriverService.IsApprovedDriverAsync` | `DriverController.CreateLine` (GET)، يعني السائق المعلق يكدر يفتح فورم الخط، لكن الحفظ يبقى مرفوض من `LineService` |

### 4.4 الخطوط

| إذا عدلت... | شنو ينكسر |
|---|---|
| `LineService.AddStops` (طريقة الترقيم 1، 2، 3) | ⚠️ **ميزات ثانية تعتمد عليه:** `SubscriptionService.FindPrice` (ترتيب الصعود والنزول بالبحث والطلب)، و `SubscriptionApprovalService.CheckSeatsAsync` (أرقام المقاطع). وقيد `chk_linestops_order` بالداتابيس يطلب الترتيب ≥ 1 |
| `LineService.AddPrices` (ربط `Linestop` و `LinestopNavigation`) | ⚠️ EF ما يعرف يحفظ الأسعار بعد المحطات، فيطيح الحفظ على MySQL بسبب الـ FK المركب. الداتابيس الوهمية ما تكشف هذا بالكامل |
| `LineService.ValidateLineInfo` (حد المقاعد) | إذا سمحت بمقاعد أكثر من السيارة، الداتابيس ترفض بالـ Trigger `trg_lines_bi` ويطيح الحفظ برسالة إنكليزية |
| `LineService.CheckOnePrice` (الصعود قبل النزول) | إذا شلته، الداتابيس ترفض بالـ Trigger `trg_lineprices_bi` |
| `LineService.GetDriverLineAsync` (شرط `Driver.UserId == userId`) | ⚠️ ثغرة خطيرة: أي سائق يشوف خطوط غيره، وصفحة `LineRequests` تنكشف. اختبار `DriverLines_OnlyOwnLinesAreVisible` يكشفها |
| `LineService.HandleOpenSubscriptionsAsync` | الطلبات المعلقة تبقى على خط مغلق، والسائق ما يكدر يوافق عليها لأن `CheckRequestState` يشترط خط Open |

### 4.5 الاشتراكات

| إذا عدلت... | شنو ينكسر |
|---|---|
| `SubscriptionService.FindPrice` | البحث (`SearchLinesAsync`) **و** طلب الاشتراك (`ValidateStopsAndPrice`) سوية، لأن الاثنين يستخدمونها |
| `SubscriptionService.CalculateEndDate` | ⚠️ إذا صارت المدة أقل من شهر، الداتابيس ترفض الحفظ بقيد `chk_subscriptions_period`. وتتأثر حسبة تداخل التواريخ بـ `SubscriptionApprovalService.GetOverlappingActiveAsync` |
| `SubscriptionService.CheckNoOpenSubscriptionAsync` | إذا شلته، الداتابيس ترفض بالمفتاح `uq_subscriptions_one_open`، ويطيح الحفظ بدل رسالة واضحة |
| `SubscriptionService.GetOpenLinesAsync` (شرط النسائي) | ⚠️ الخط النسائي يظهر للذكور ويكدرون يطلبونه، لأن `RequestSubscriptionAsync` يعتمد على نفس الدالة عن طريق `GetOpenLineAsync` |
| قيم الحالة `"Pending"` و `"Active"` و `"Rejected"` و `"Cancelled"` | ⚠️ لازم تطابق `chk_subscriptions_status`، والعمود المولّد `OpenKey` بالداتابيس يعتمد على `'Pending'` و `'Active'` بالضبط |
| أسماء الربط `Linestop` و `LinestopNavigation` بـ `Subscription` | ⚠️ **مربكة:** `Linestop` = النزول، و `LinestopNavigation` = الصعود. إذا عدت توليد الـ Models (scaffold --force)، ممكن تتغير الأسماء وتنكسر الـ `Include` بـ `GetMySubscriptionsAsync` و `GetLineSubscriptionsAsync` |

### 4.6 الموافقة وفحص المقاعد

| إذا عدلت... | شنو ينكسر |
|---|---|
| `SubscriptionApprovalService.LockLineAsync` (ترتيبه أو نص الـ SQL) | ⚠️ **أخطر نقطة بالمشروع:** إذا انحذف القفل أو صار بعد قراءة الطلب، موافقتين بنفس اللحظة ممكن يحجزون أكثر من المقاعد. ولا اختبار يكشف هذا، لأن الداتابيس الوهمية تتخطى القفل. وأسماء الجداول بالـ SQL (`lines` و `subscriptions`) لازم تطابق الداتابيس |
| `SubscriptionApprovalService.CountSeatsOnSegment` (شرط `<=` و `>=`) | الحسبة تصير غلط: إما ترفض طلبات صحيحة، أو تقبل طلبات زايدة. اختبارا `Approve_ChecksEachSegment` و `CountSeatsOnSegment_SumsOnlyCoveringSubscriptions` يكشفانها |
| `SubscriptionApprovalService.GetOverlappingActiveAsync` | إذا شلت شرط التواريخ، اشتراكات الأشهر الماضية تحجز مقاعد. وإذا شلت شرط `Active`، الطلبات المعلقة تحجز مقاعد (عكس قاعدة 3) |
| `SubscriptionApprovalService.ApproveAsync` (حذف `CommitAsync`) | ⚠️ ولا موافقة تنحفظ على MySQL، لأن الـ Transaction تنلغي تلقائياً. الداتابيس الوهمية ما تكشفها |

### 4.7 الـ Models المولّدة

| إذا عدلت... | شنو ينكسر |
|---|---|
| أي ملف بـ `Models/` يدوياً | ⚠️ التعديل **ينمسح** بأول scaffold --force. CLAUDE.md يقول: لا تعدل الـ Models يدوياً |
| إعادة الـ scaffold | راجع أسماء الربط (شوف 4.5)، وشغّل build + test |
