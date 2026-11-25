# تقرير التنفيذ: نظام خطوط النقل الخاصة (الاشتراكات الشهرية)

**تاريخ التقرير:** 2026-11-22
**المرحلة الحالية:** المرحلة 1 (الباك اند). الخطوات من 1 إلى 7 منفذة. المرحلة 2 (الواجهات) لم تبدأ بعد.

**الحالة العامة:**

| البند | القيمة |
|---|---|
| بناء المشروع (`dotnet build`) | ناجح، 0 أخطاء، 0 تحذيرات |
| الاختبارات الآلية (`dotnet test`) | 149 اختبار، كلها ناجحة |
| الواجهات (Views) | غير منفذة بعد (المرحلة 2) |
| تعديلات على قاعدة البيانات | لا يوجد. القاعدة استُخدمت للقراءة فقط (توليد الـ Models واختبار الاتصال) |

> **ملاحظة مهمة:** كل الـ Controllers جاهزة لكنها ترجع Views غير موجودة بعد. لذلك فتح أي صفحة بالمتصفح، غير الصفحة الرئيسية، يعطي حالياً خطأ "View not found"، وهذا متوقع لحد المرحلة 2.

---

## 1) التقنيات والحزم

### 1.1 التقنيات الأساسية

| التقنية | الإصدار | الاستخدام |
|---|---|---|
| C# / ASP.NET Core MVC | .NET 8 (LTS) | إطار العمل للموقع |
| .NET SDK | 11.0.100 | أداة البناء (تدعم بناء مشاريع .NET 8) |
| .NET Runtime | 8.0.34 | تشغيل المشروع |
| MySQL Server | 8.0.45 | قاعدة البيانات `line_subscription` (موجودة مسبقاً) |
| EF Core | 8.0.13 | التعامل مع قاعدة البيانات بـ LINQ |

### 1.2 الحزم المنصبة (NuGet)

| الحزمة | الإصدار | المشروع | شنو تسوي | ليش اخترناها |
|---|---|---|---|---|
| `Pomelo.EntityFrameworkCore.MySql` | 8.0.3 | TransportLines | مزود EF Core لـ MySQL | المزود المعتمد بالاتفاق مع المشرف، ويدعم EF Core 8 |
| `Microsoft.EntityFrameworkCore.Design` | 8.0.13 | TransportLines | مطلوبة لأمر توليد الـ Models (scaffold) | اخترنا نفس إصدار EF Core اللي يجيبه Pomelo حتى ما يصير تعارض |
| `BCrypt.Net-Next` | 4.0.3 | TransportLines | تشفير كلمات المرور (Hash) | bcrypt بطيء عن قصد، فيصعب على المخترق تخمين كلمات المرور (قاعدة 8 بـ CLAUDE.md) |
| `Microsoft.Extensions.Configuration.UserSecrets` | 8.0.1 | TransportLines.Tests | يقرأ سلسلة الاتصال من user-secrets داخل الاختبارات | حتى اختبار الاتصال يستخدم نفس الإعداد بدون ما نكتب كلمة المرور بالكود |
| `Microsoft.EntityFrameworkCore.InMemory` | 8.0.13 | TransportLines.Tests | قاعدة بيانات وهمية بالذاكرة | حتى الاختبارات ما تلمس قاعدة البيانات الحقيقية أبداً |
| `xunit` + `xunit.runner.visualstudio` | 2.5.3 | TransportLines.Tests | إطار الاختبارات | جاي مع قالب `dotnet new xunit` |

### 1.3 الأدوات

| الأداة | الإصدار | ملاحظة |
|---|---|---|
| `dotnet-ef` | 8.0.13 | منصبة كأداة **محلية** للمشروع (ملف `dotnet-tools.json`)، لأن الأداة العامة بالجهاز إصدارها 10 ولا تطابق EF Core 8 |

### 1.4 هيكل الـ Solution

```
Trasportline3/
├── TransportLines.slnx          ← ملف الـ Solution
├── dotnet-tools.json            ← أداة dotnet-ef المحلية
├── CHANGES.md                   ← سجل كل التغييرات
├── docs/Implementation_Report.md← هذا التقرير
├── TransportLines/              ← مشروع الويب (MVC)
│   ├── Controllers/  Data/  Models/  Security/  Services/  ViewModels/  Views/
│   └── App_Data/uploads/        ← الصور المرفوعة (يتولد وقت التشغيل، مستثنى من git)
└── TransportLines.Tests/        ← مشروع الاختبارات (xUnit)
```

**ليش مشروع الاختبارات منفصل وجنب مشروع الويب؟** إذا كان داخل مشروع الويب، كان راح ينبني وياه ويصير تعارض بالملفات.

---

## 2) الاتصال بقاعدة البيانات والـ Models

### 2.1 سلسلة الاتصال (User Secrets)

- **الملف:** `TransportLines/Program.cs`، والإعداد `UserSecretsId` بملف `TransportLines/TransportLines.csproj`.
- **شنو تسوي:**
  - سلسلة الاتصال (السيرفر، القاعدة `line_subscription`، المستخدم `transport_app`، كلمة المرور) محفوظة بـ **user-secrets**، يعني بملف خاص بحساب ويندوز خارج مجلد المشروع.
  - `Program.cs` يقرأها بـ `GetConnectionString("DefaultConnection")`. إذا ما لقاها، يوقف البرنامج برسالة عربية واضحة.
- **شنو تحتاج:**
  - أمر `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."`، والطالب كتبه بنفسه بالـ terminal.
  - تشغيل البرنامج ببيئة `Development`، لأن user-secrets تنقرأ بهذي البيئة فقط.
- **ليش:** حتى كلمة المرور ما تنكتب بالكود ولا بـ `appsettings.json` ولا ترتفع على git (قاعدة أمان بـ CLAUDE.md). والمستخدم `transport_app` محدود الصلاحيات، مو `root`.
- **كيف اختبرناها:** `DatabaseConnectionTests` (اختباران): الأول يتأكد إن الاتصال ينجح، والثاني يقرأ جدول `Roles`. الاثنين قراءة فقط.

### 2.2 تسجيل الـ DbContext

- **الملف:** `TransportLines/Program.cs`
- **شنو تسوي:** يسجل `AppDbContext` بـ `UseMySql(connectionString, serverVersion)`، ونسخة MySQL مكتوبة يدوياً `new MySqlServerVersion(new Version(8, 0, 45))`.
- **ليش كتبنا النسخة يدوياً:** بدل `ServerVersion.AutoDetect`، اللي يفتح اتصال إضافي عند كل تشغيل بس حتى يعرف النسخة.

### 2.3 توليد الـ Models (Database-First)

- **الملفات:** `TransportLines/Models/*.cs` (15 كلاس) و `TransportLines/Data/AppDbContext.cs`.
- **الأمر المستخدم:**
  ```
  dotnet tool run dotnet-ef dbcontext scaffold Name=ConnectionStrings:DefaultConnection Pomelo.EntityFrameworkCore.MySql --context AppDbContext --context-dir Data --output-dir Models --data-annotations --no-onconfiguring --force
  ```
- **شنو تسوي:** تقرأ شكل الجداول من قاعدة البيانات الموجودة، وتولّد كلاس لكل جدول. **ما غيّرت أي شي بالقاعدة.**
- **الخيارات:**
  - `--data-annotations`: الإعدادات (طول النص، المفاتيح، الأسماء) تنكتب كـ Attributes فوق الخصائص، فتصير أسهل بالقراءة.
  - `--no-onconfiguring`: سلسلة الاتصال ما تنكتب داخل `AppDbContext`.
- **ليش Database-First:** قاعدة البيانات موجودة ومختبرة مسبقاً (سكربت `02_schema_tests.sql`)، وCLAUDE.md يمنع الـ migrations.
- **الكلاسات المولّدة:** `Role`، `User`، `Driver`، `Vehicletype`، `Vehicle`، `Area`، `Stop`، `Line`، `Lineday`، `Linestop`، `Lineprice`، `Subscription`، `Rating`، `Notification`، و`TestResult`.
- **ملاحظات:**
  1. جدول `test_results` موجود بالقاعدة، وعلى الأغلب سكربت الاختبارات هو اللي أنشأه، فتولّد 15 كلاس بدل 14.
  2. أسماء الكلاسات طلعت مثل `Lineday` و `Linestop` (حروف صغيرة)، لأن MySQL على ويندوز يحفظ أسماء الجداول بحروف صغيرة.
  3. أسماء الربط بجدول `Subscription` مربكة: `Linestop` يعني **محطة النزول**، و `LinestopNavigation` يعني **محطة الصعود**. وبجدول `Lineprice`: `Linestop` يعني محطة البداية، و `LinestopNavigation` يعني محطة النهاية.
  4. ملف `ErrorViewModel.cs`، اللي جاي من قالب MVC، انتقل من `Models/` إلى `ViewModels/`، حتى يبقى مجلد `Models/` للكلاسات المولّدة فقط.

---

## 3) الأمان

كل نقطة بهذا القسم منفذة وموجودة بالكود.

### 3.1 تشفير كلمات المرور (bcrypt)

- **الكلاس:** `AccountService` بملف `TransportLines/Services/AccountService.cs`
- **أهم الدوال:** `CreateUser` (ينشئ الهاش)، `LoginAsync` (يتحقق من الهاش)، `IsStrongPassword`.
- **شنو تسوي:**
  - كلمة المرور تتحول لهاش بـ `BCrypt.HashPassword(password, 11)` قبل الحفظ بعمود `Users.PasswordHash`، وما تنحفظ كنص أبداً.
  - عند الدخول نستخدم `BCrypt.Verify`.
- **شنو تحتاج:** حزمة `BCrypt.Net-Next`، وجدول `Users`.
- **ليش:** قاعدة 8 بـ CLAUDE.md. ورقم 11 (Work Factor) يخلي كل محاولة تخمين بطيئة على المخترق.
- **كيف اختبرناها:**
  - `Register_ValidPassenger_SavesUserWithHashedPassword`: يتأكد إن الهاش يختلف عن كلمة المرور، وإن `Verify` ينجح.
  - `Login_PasswordIsCaseSensitive`: يتأكد إن كلمة المرور حساسة للحروف الكبيرة والصغيرة.
- **مثال:** الراكب يسجل بكلمة المرور `Pass1234`، فبالقاعدة ينحفظ نص طويل يبدأ بـ `$2a$11$...`. حتى لو أحد سرق الجدول، ما يعرف كلمة المرور.

### 3.2 منع كشف الحسابات المسجلة (رسالة موحدة + وقت ثابت)

- **الكلاس:** `AccountService`، الدالة `LoginAsync`، والثوابت `DummyHash` و `WrongLoginMessage`.
- **شنو تسوي:**
  - الرقم غلط أو كلمة المرور غلط يعطون **نفس الرسالة**: "رقم الهاتف أو كلمة المرور غير صحيحة".
  - إذا الرقم غير موجود، الكود يفحص كلمة المرور ضد هاش وهمي، حتى يكون وقت الرد نفسه.
  - الحساب المحذوف (`IsDeleted`) ياخذ نفس الرسالة العامة.
  - الحساب الموقوف ياخذ رسالة "حسابك موقوف"، لكن **بعد** التأكد من كلمة المرور، حتى ما تنكشف حالة الحساب لأي شخص.
- **ليش:** حتى المخترق ما يكدر يعرف هل الرقم مسجل، لا من الرسالة ولا من سرعة الرد.
- **كيف اختبرناها:**
  - `Login_WrongPassword_Fails` و `Login_UnknownPhone_FailsWithSameMessage`: نفس الرسالة بالحالتين.
  - `Login_DeletedAccount_Fails` و `Login_SuspendedAccount_Fails`.

### 3.3 تسجيل الدخول بالـ Cookie

- **الكلاس:** `SecurityHelper`، الدالة `ConfigureCookie`، بملف `TransportLines/Security/SecurityHelper.cs`. و `AccountService.CreatePrincipal`، و `AccountController.SignInUserAsync`.
- **شنو تسوي:**
  - بعد الدخول الناجح ينحفظ Cookie اسمه `TransportLines.Auth`، بيه رقم المستخدم واسمه ودوره (Claims).
  - إعدادات الـ Cookie:
    - **HttpOnly:** الجافاسكربت ما يكدر يقرأه.
    - **Secure:** ينرسل على https فقط.
    - **SameSite=Strict:** ما ينرسل من مواقع ثانية.
    - **مدة الانتهاء:** 8 ساعات بدون نشاط (Sliding).
  - خيار "تذكرني" يخلي الـ Cookie يبقى بعد غلق المتصفح.
- **شنو تحتاج:** `AddAuthentication().AddCookie(...)` بـ `Program.cs`، و `UseAuthentication()`.
- **ليش:** طريقة ASP.NET Core القياسية والبسيطة. اخترناها بدل ASP.NET Identity لأن Identity يحتاج جداول خاصة بيه، وقاعدة البيانات عدنا موجودة وما نغيرها.
- **كيف اختبرناها:**
  - `CreatePrincipal_ContainsIdNameAndRole`.
  - فحص يدوي بـ curl: الصفحة المحمية تحوّل لـ `/Account/Login` (302).

### 3.4 الصلاحيات (كل صفحة تحتاج دخول افتراضياً)

- **الكلاسات:** `SecurityHelper.ConfigureAuthorization`، و `[Authorize(Roles = ...)]` على الـ Controllers.
- **شنو تسوي:**
  - **سياسة افتراضية (FallbackPolicy):** كل صفحة تحتاج تسجيل دخول، إلا اللي عليها `[AllowAnonymous]`: الصفحة الرئيسية، والتسجيل، والدخول، وصفحة "ما عندك صلاحية".
  - `AdminController` للأدمن فقط، و `DriverController` للسائق فقط، و `PassengerController` للراكب فقط.
  - الخدمات نفسها تعيد الفحص. مثلاً `AdminService.CheckAdminAsync` يتأكد إن المستخدم أدمن فعّال حتى لو وصل للخدمة.
- **ليش:** إذا نسينا نحط `[Authorize]` على صفحة جديدة، تبقى محمية تلقائياً. وفحص الخدمة طبقة ثانية.
- **كيف اختبرناها:**
  - `Approve_ByNonAdmin_Fails` و `Approve_BySuspendedAdmin_Fails`.
  - فحص يدوي بـ curl: صفحة محمية بدون دخول ترجع 302.

### 3.5 حماية ملكية البيانات (ما أحد يشوف بيانات غيره)

- **الكلاس:** `UserHelper` بملف `TransportLines/Security/UserHelper.cs`، الدالة `GetUserId`.
- **شنو تسوي:**
  - كل الـ Controllers تاخذ رقم المستخدم من الـ Cookie، مو من الرابط أو الفورم.
  - الخدمات تبحث بالرقم المطلوب **ورقم صاحبه سوية**. مثلاً `GetDriverLineAsync(userId, lineId)` ترجع الخط بس إذا السائق هو صاحبه.
- **ليش:** حتى لو أحد غيّر الرقم بالرابط (مثلاً `/Driver/LineDetails/5`)، ما يشوف إلا بياناته (قاعدة أمان بـ CLAUDE.md).
- **كيف اختبرناها:**
  - `DriverLines_OnlyOwnLinesAreVisible`، و `CloseLine_OtherDriverOrAlreadyClosed_Fails`.
  - `Cancel_OthersOrAlreadyCancelled_Fails`، و `Approve_ByOtherDriver_Fails`.
  - `GetLineSubscriptions_OnlyForLineOwner`، و `MySubscriptions_OnlyOwn`.
- **مثال:** السائق "أحمد" عنده خط رقم 5، والسائق "علي" يكتب بالرابط `LineDetails/5`. الخدمة ترجع `null`، والصفحة ترجع 404.

### 3.6 الحماية من CSRF

- **الكلاسات:** `SecurityHelper.ConfigureMvc` (فلتر عام `AutoValidateAntiforgeryTokenAttribute`)، و `[ValidateAntiForgeryToken]` على كل POST.
- **شنو تسوي:** أي فورم POST بدون Anti-Forgery Token صحيح ينرفض.
- **ليش:** حتى موقع خبيث ما يكدر يرسل فورم باسم المستخدم. والفلتر العام يحمي حتى لو نسينا الـ Attribute.
- **كيف اختبرناها:** فحص يدوي بـ curl: POST على `/Account/Login` بدون Token يرجع **400**.

### 3.7 تحديد عدد المحاولات (Rate Limiting)

- **الكلاس:** `SecurityHelper`، والدوال `ConfigureRateLimiter`، و `GetLoginPartition`، و `GetRegisterPartition`، و `CreateLoginLimitOptions`، و `CreateRegisterLimitOptions`.
- **شنو تسوي:**
  - **الدخول:** 5 محاولات بالدقيقة لكل عنوان IP.
  - **التسجيل:** 5 محاولات كل 10 دقائق لكل IP.
  - بعد الحد يرجع السيرفر **429 (طلبات كثيرة)**.
  - يتطبق على Actions الدخول والتسجيل بـ `[EnableRateLimiting(...)]`.
- **شنو تحتاج:** `AddRateLimiter` و `UseRateLimiter` بـ `Program.cs`. ما يحتاج أي جدول.
- **ليش:** ضد تخمين كلمات المرور وإنشاء حسابات وهمية بالجملة. قاعدة البيانات ما بيها عمود لعدد المحاولات الفاشلة، فاخترنا حل ما يغيّر القاعدة.
- **كيف اختبرناها:** فحص يدوي بـ curl: بعد 5 محاولات دخول بالدقيقة رجعت **429**.

### 3.8 رؤوس الحماية (Security Headers)

- **الكلاس:** `SecurityHelper.AddSecurityHeaders`
- **شنو تسوي:** يضيف لكل رد من السيرفر هذي الرؤوس:

| الرأس | القيمة | الفائدة |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | يمنع المتصفح يشغّل ملف بنوع غير نوعه |
| `X-Frame-Options` | `DENY` | يمنع عرض الموقع داخل iframe (ضد Clickjacking) |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | ما يرسل الرابط الكامل للمواقع الثانية |
| `Content-Security-Policy` | `default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'` | يسمح بالسكربتات من موقعنا فقط (ضد XSS) |

- **كيف اختبرناها:** فحص يدوي بـ curl: الرؤوس الأربعة موجودة بالرد.

### 3.9 طرد الحساب الموقوف فوراً

- **الكلاسات:** `SecurityHelper.ValidateCookieAsync` و `AccountService.IsUserActiveAsync`.
- **شنو تسوي:** مع كل طلب، يفحص بالقاعدة إن صاحب الـ Cookie بعده موجود وفعّال ومو محذوف. إذا لا، يرفض الـ Cookie ويسجل خروج.
- **ليش:** بدونها، الأدمن إذا أوقف حساب، يبقى صاحبه داخل لحد ما تنتهي الـ Cookie (8 ساعات).
- **كيف اختبرناها:** `IsUserActive_SuspendedOrBanned_ReturnsFalse`، و `IsUserActive_DeletedUser_ReturnsFalse`، و `IsUserActive_UnknownUser_ReturnsFalse`.

### 3.10 منع التحويل لمواقع خارجية (Open Redirect)

- **الكلاس:** `AccountController.RedirectToLocal`
- **شنو تسوي:** بعد الدخول يتحول المستخدم لـ `returnUrl` **بس إذا** هو رابط داخل الموقع (`Url.IsLocalUrl`). غير هيج يروح للصفحة الرئيسية.
- **مثال:** رابط خبيث مثل `/Account/Login?returnUrl=https://evil.com` يوصّل المستخدم للصفحة الرئيسية، مو للموقع الخبيث.

### 3.11 حماية رفع الصور

- **الكلاس:** `FileUploadService` بملف `TransportLines/Services/FileUploadService.cs`
- **أهم الدوال:** `ValidateImage`، و `SaveImageAsync`، و `DeleteImage`، و `HasImageSignature`.
- **شنو تسوي:**
  1. الحجم أقل من **2 ميگابايت**.
  2. الامتداد `.jpg` أو `.jpeg` أو `.png` فقط.
  3. يقرأ **أول 4 بايتات** من الملف ويقارنها بتوقيع JPG (`FF D8 FF`) أو PNG (`89 50 4E 47`). هيج الملف اللي اسمه `.jpg` وهو مو صورة ينرفض.
  4. يحفظ الملف باسم عشوائي (`Guid`)، ويتجاهل اسم الملف اللي جاي من المستخدم.
  5. يحفظ بمجلد `App_Data/uploads/` **خارج** `wwwroot`، فما ينفتح برابط مباشر.
  6. إذا فشل الحفظ بالقاعدة، يحذف الصور اللي انرفعت.
  7. طلب التسجيل محدود بـ 10 ميگا (`RequestSizeLimit`).
- **شنو تحتاج:**
  - تسجيل `FileUploadService` بـ `Program.cs`، والمسار `App_Data/uploads`.
  - إضافة `TransportLines/App_Data/` لملف `.gitignore`، حتى الوثائق الشخصية ما ترتفع على git.
- **ليش خارج wwwroot:** صور الإجازة والبطاقة الوطنية بيانات شخصية. CLAUDE.md يقترح `wwwroot/uploads`، لكن اخترنا `App_Data` لأنه أأمن، ونعرض الصور للأدمن عن طريق Action محمي.
- **كيف اختبرناها:** `FileUploadServiceTests`، وتغطي:
  - صور حقيقية تنقبل.
  - ملف فارغ، أو كبير، أو بامتداد `.exe`/`.php`/`.html`/`.jpg.exe`، ينرفض.
  - ملف نصي اسمه `photo.jpg` ينرفض.
  - الحفظ يغير الاسم، والحذف يشتغل.
- **مثال:** شخص يرفع ملف نصي اسمه `license.jpg`. الامتداد مقبول، لكن أول بايتات الملف مو بايتات صورة، فينرفض برسالة "الملف مو صورة حقيقية".

### 3.12 الحماية من Path Traversal

- **الكلاسات:** `FileUploadService.GetSafeFullPath`، و `AdminService.GetDriverPhotoPathAsync`.
- **شنو تسوي:**
  - `GetDriverPhotoPathAsync` يقبل نوع الصورة من قائمة ثابتة فقط: `license`، `id`، `registration`، `vehicle`.
  - `GetSafeFullPath` يحول المسار لمسار كامل، ويتأكد إنه داخل مجلد الرفع وإن الملف موجود.
  - صورة الوثيقة تنرجع ويا `Cache-Control: no-store`، حتى المتصفح ما يحفظها.
- **ليش:** حتى محد يكدر يقرأ ملفات السيرفر (مثل `appsettings.json`) عن طريق رابط الصورة.
- **كيف اختبرناها:**
  - `GetSafeFullPath_OutsideOrMissing_ReturnsNull`، بالمدخلات: `../../appsettings.json`، و `..\..\Program.cs`، و `C:\Windows\win.ini`، وملف غير موجود، وقيمة فارغة، و null.
  - `GetDriverPhotoPath_OnlyAllowedTypes`.

### 3.13 الحماية من SQL Injection

- **أين:** كل الخدمات.
- **شنو تسوي:**
  - كل الاستعلامات مكتوبة بـ **LINQ**، و EF Core يحولها لاستعلامات بـ Parameters تلقائياً.
  - الـ SQL الوحيد المكتوب يدوياً هو أمر القفل بـ `SubscriptionApprovalService.LockLineAsync`، وهو مكتوب بـ `ExecuteSqlInterpolatedAsync`. هذا يحول `{subscriptionId}` لـ Parameter، مو نص ملصوق.
- **ليش:** قاعدة الأمان الأولى بـ CLAUDE.md: ممنوع بناء SQL بلصق النصوص.
- **كيف اختبرناها:**
  - `Register_InvalidPhone_IsRejected`، بمدخل `' OR 1=1 --`.
  - `Login_InjectionOrEmpty_Fails`، بمدخلات `' OR '1'='1` و `07701234567' --`.
  - الدخول يفشل والبرنامج ما يطيح.

### 3.14 الحماية من XSS

- **أين:** `AccountService.ValidateNameAndContact`، و `LineService.ValidateLineInfo`، و Razor (بالمرحلة 2).
- **شنو تسوي:**
  - الاسم يقبل حروف ومسافات فقط.
  - أسماء الخطوط والسيارات تقبل حروف وأرقام ومسافات وشرطة فقط.
  - Razor يرمّز أي نص يعرضه تلقائياً، وراح نلتزم بعدم استخدام `Html.Raw` على مدخلات المستخدم.
- **كيف اختبرناها:** `Register_InvalidName_IsRejected`، بمدخل `<script>alert(1)</script>`.

---

## 4) الخدمات والـ Controllers

**مبدأ التصميم:** الـ Controller "خفيف". يستلم الفورم، ويرسله للخدمة (Service)، ويعرض النتيجة. كل المنطق والفالديشن الحقيقي يكون بالخدمة.

**كلاسات مساعدة مشتركة:**
- `ServiceResult` (`Services/ServiceResult.cs`): قائمة أخطاء، كل خطأ عبارة عن اسم الحقل والرسالة. إذا القائمة فارغة، معناها العملية نجحت. الكونترولر ينقل الأخطاء للـ `ModelState` حتى ينعرض كل خطأ جنب حقله.
- `LoginResult` (`Services/LoginResult.cs`): نتيجة الدخول وبيانات المستخدم.

**الاعتماد على الحفظ الواحد:** بأكثر من مكان، نحفظ عدة جداول بـ `SaveChangesAsync` **واحد**: تسجيل السائق، وإنشاء الخط، والإغلاق. EF Core ينفذ الـ SaveChanges الواحد كـ Transaction واحدة: يا ينحفظ الكل، يا ولا شي.

### 4.1 التسجيل وتسجيل الدخول

- **الكلاسات والملفات:**
  - `AccountService` بملف `TransportLines/Services/AccountService.cs`
  - `AccountController` بملف `TransportLines/Controllers/AccountController.cs`
  - `RegisterViewModel` و `LoginViewModel` بمجلد `TransportLines/ViewModels/`
- **أهم الدوال بالخدمة:**

| الدالة | الوظيفة |
|---|---|
| `RegisterAsync` | الدالة الرئيسية للتسجيل: تنظف المدخلات، وتتحقق، وتحفظ |
| `CleanInput` | تشيل المسافات الزايدة، والإيميل يصير حروف صغيرة، ورقم الإجازة واللوحة حروف كبيرة |
| `ValidateBasicFields` و `ValidateNameAndContact` و `ValidatePassword` | تحقق الحقول المشتركة |
| `CheckUniqueUserFieldsAsync` | رقم الهاتف والإيميل غير مكررين |
| `ValidateDriverAsync` | تجمع فحوصات السائق: `ValidateDriverFields`، و `ValidateVehicleFieldsAsync`، و `ValidateVehicleTypeAndSeatsAsync`، و `ValidateDriverPhotos`، و `CheckUniqueDriverFieldsAsync` |
| `SavePassengerAsync` | يحفظ الراكب |
| `SaveDriverAsync` | يحفظ السائق: `CreateDriverAsync` و `CreateVehicleAsync`، ثم User + Driver + Vehicle بنفس الـ SaveChanges |
| `LoginAsync` | تسجيل الدخول |
| `GetActiveVehicleTypesAsync` | أنواع السيارات الفعّالة لقائمة الاختيار |

- **Actions الكونترولر:**

| الـ Action | الوظيفة |
|---|---|
| `Register` (GET/POST) | فورم التسجيل وحفظه |
| `Login` (GET/POST) | فورم الدخول والتحقق منه |
| `Logout` (POST فقط) | تسجيل الخروج |
| `AccessDenied` | صفحة "ما عندك صلاحية" |

- **شنو تسوي:**
  - الفورم بيه اختيار: راكب أو سائق، **وما بيه خيار Admin**. والسيرفر يرفض أي قيمة غير `Passenger` و `Driver`، حتى لو أحد عدّل الفورم.
  - إذا الحساب سائق، لازم يرسل بيانات الإجازة والبطاقة والسيارة و3 صور إجبارية. السائق ينحفظ بحالة `Pending`.
  - رقم الدور (RoleID) ينقرأ من جدول `Roles` بالاسم، ومو مكتوب كرقم بالكود.
  - عند خطأ بالحفظ (مثلاً رقم انسجل بنفس اللحظة من شخص ثاني)، الخدمة تلتقط `DbUpdateException` وترجع رسالة واضحة بدل ما يطيح البرنامج.
  - عند الخطأ يرجع الفورم ويا بيانات المستخدم، عدا كلمات المرور.
  - بعد النجاح تطلع رسالة نجاح، والسائق ياخذ رسالة إن حسابه ينتظر موافقة الإدارة.
- **شنو تحتاج:**
  - **الجداول:** `Users`، و `Roles`، و `Drivers`، و `Vehicles`، و `VehicleTypes`.
  - **الكلاسات الثانية:** `FileUploadService`، و `SecurityHelper` (حد المحاولات).
  - **الحزم:** `BCrypt.Net-Next`.
- **ليش:** قواعد التسجيل بـ CLAUDE.md: لا Admin من الفورم، والسائق ينحفظ مع سيارته بعملية واحدة، وحالته Pending.
- **كيف اختبرناها:** `AccountServiceRegisterTests` (32 حالة)، و `AccountServiceLoginTests` (10 حالات)، و `AccountServiceHelperTests` (7 حالات).
- **سيناريو توضيحي:**
  > سارة تفتح صفحة التسجيل، تختار "راكب"، تكتب اسمها ورقمها `07701234567` وكلمة المرور `Pass1234`.
  > 1. الـ ViewModel يتأكد من الصيغ.
  > 2. `AccountService` يتأكد إن الرقم غير مسجل، وإن كلمة المرور بيها حرف ورقم.
  > 3. كلمة المرور تتحول لهاش bcrypt، وينحفظ المستخدم بدور Passenger وحالة Active.
  > 4. تتحول سارة لصفحة الدخول برسالة "تم إنشاء حسابك بنجاح".
  >
  > لو كتبت كلمة المرور خطأ عند الدخول 6 مرات بنفس الدقيقة، المحاولة السادسة ترجع 429.

### 4.2 مراجعة السائقين (الأدمن)

- **الكلاسات والملفات:**
  - `AdminService` بملف `TransportLines/Services/AdminService.cs`
  - `AdminController` بملف `TransportLines/Controllers/AdminController.cs`
  - `RejectDriverViewModel` بملف `TransportLines/ViewModels/RejectDriverViewModel.cs`
- **أهم الدوال بالخدمة:**

| الدالة | الوظيفة |
|---|---|
| `GetPendingDriversAsync` | السائقين اللي ينتظرون، الأقدم أولاً |
| `GetDriverDetailsAsync` | تفاصيل سائق مع المستخدم والسيارة |
| `ApproveDriverAsync` | الموافقة |
| `RejectDriverAsync` | الرفض مع سبب |
| `FindPendingDriverAsync` و `CheckAdminAsync` و `IsAdminAsync` | فحوصات مشتركة |
| `SetVerification` | يغير الحالة ويسجل منو راجع ومتى |
| `AddNotification` | يضيف إشعار للسائق |
| `GetDriverPhotoPathAsync` | مسار صورة الوثيقة |

- **Actions الكونترولر:** `PendingDrivers`، و `DriverDetails`، و `ApproveDriver` (POST)، و `RejectDriver` (POST)، و `DriverPhoto`.
- **شنو تسوي:**
  - **الموافقة:** السائق لازم يكون `Pending`، وإجازته **لازم تبقى سارية وقت الموافقة**. ينسجل `VerifiedByUserId` و `VerifiedAt`، وينضاف إشعار نوعه `DriverApproved`.
  - **الرفض:** السبب إجباري (من 5 إلى 255 حرف) وينحفظ بـ `RejectionReason`. ينضاف إشعار نوعه `DriverRejected` بيه السبب.
  - رقم الأدمن ينقرأ من الـ Cookie، والخدمة تتأكد إنه أدمن فعّال.
- **شنو تحتاج:**
  - **الجداول:** `Drivers`، و `Users`، و `Roles`، و `Vehicles`، و `Notifications`.
  - **الكلاسات الثانية:** `FileUploadService`، و `UserHelper`.
- **ليش:** الخطوة 3 بالخطة: السائق ما ينشر خط قبل موافقة الإدارة.
- **ملاحظة:** عمود `Notifications.Type` نص حر بالقاعدة، فاستخدمنا القيم `DriverApproved` و `DriverRejected` بدون تغيير القاعدة.
- **كيف اختبرناها:** `AdminServiceTests` (15 حالة).
- **سيناريو توضيحي:**
  > السائق كريم سجّل قبل يومين. الأدمن يفتح قائمة السائقين المعلقين، ويفتح تفاصيل كريم، ويشوف صورة إجازته عن طريق `DriverPhoto?type=license`.
  > - إذا الصورة غير واضحة، يضغط رفض ويكتب "صورة الإجازة غير واضحة"، فتصير حالة كريم Rejected ويوصله إشعار بالسبب.
  > - لو حاول كريم نفسه يستدعي الموافقة على حسابه، الخدمة ترفض لأنه مو أدمن.

### 4.3 ملف السائق

- **الكلاسات والملفات:** `DriverService` بملف `TransportLines/Services/DriverService.cs`، و `DriverController.Profile`.
- **أهم الدوال:**

| الدالة | الوظيفة |
|---|---|
| `GetDriverProfileAsync` | يرجع ملف السائق مع سيارته |
| `IsApprovedDriverAsync` | يرجع هل السائق موافق عليه. تستخدمها صفحة إنشاء الخط |
| `GetStatusMessage` | رسالة عربية بحالة الحساب، وإذا مرفوض يعرض سبب الرفض |

- **كيف اختبرناها:** `DriverServiceTests` (4 حالات).

### 4.4 إدارة الخطوط (السائق)

- **الكلاسات والملفات:**
  - `LineService` بملف `TransportLines/Services/LineService.cs`
  - `DriverController` بملف `TransportLines/Controllers/DriverController.cs`
  - `CreateLineViewModel` بملف `TransportLines/ViewModels/CreateLineViewModel.cs`، وبيه الكلاسات `LineStopInput` و `LinePriceInput`
- **أهم الدوال بالخدمة:**

| الدالة | الوظيفة |
|---|---|
| `CreateLineAsync` | الدالة الرئيسية لإنشاء خط |
| `CleanInput` | تحذف صفوف المحطات والأسعار الفارغة |
| `GetApprovedDriverAsync` و `GetActiveVehicle` | السائق موافق عليه وعنده سيارة فعّالة فحصها ساري |
| `ValidateLineInfo` | الاسم، والوقت، والمقاعد، وأقل مدة، والملاحظات |
| `ValidateDays` | الأيام |
| `ValidateStopsAsync` و `CheckStopsExistAsync` و `ValidateArrivalTimes` | المحطات |
| `ValidatePrices` و `CheckOnePrice` و `CheckFullRoutePrice` | الأسعار |
| `SaveLineAsync` | الحفظ: `BuildLine` و `AddStops` و `AddDays` و `AddPrices` بنفس الـ SaveChanges |
| `GetDriverLinesAsync` و `GetDriverLineAsync` | خطوط السائق وتفاصيل خط، لصاحب الخط فقط |
| `CloseLineAsync` و `HandleOpenSubscriptionsAsync` | إغلاق الخط |
| `GetActiveStopsAsync` | المحطات الفعّالة لقوائم الاختيار |

- **Actions الكونترولر:** `MyLines`، و `LineDetails`، و `CreateLine` (GET/POST)، و `CloseLine` (POST).
- **شنو تسوي:**
  - **الإنشاء:**
    - السيارة تنختار **تلقائياً** من سيارات السائق، وهي أول سيارة فعّالة فحصها ساري. ما تنرسل من الفورم، حتى السائق ما يكدر يستخدم سيارة غيره.
    - المحطات تنحفظ بالترتيب: الصف الأول بالفورم هو المحطة رقم 1.
    - الأيام أرقام من 0 (الأحد) إلى 6 (السبت).
    - الحالة الأولى للخط `Open`.
  - **الإغلاق:**
    - الحالة تصير `Closed`.
    - الطلبات المعلقة تنرفض تلقائياً بسبب "السائق أغلق الخط".
    - الاشتراكات الفعّالة **تبقى** لحد نهاية مدتها.
    - كل مشترك ياخذ إشعار نوعه `LineClosed`.
  - **الخط ثابت بعد النشر:** ماكو دالة تعديل. للتغيير، السائق يغلق الخط ويسوي خط جديد (قاعدة 1).
- **شنو تحتاج:**
  - **الجداول:** `Lines`، و `LineStops`، و `LineDays`، و `LinePrices`، و `Stops`، و `Areas`، و `Vehicles`، و `Drivers`، و `Subscriptions`، و `Notifications`.
  - **الكلاسات الثانية:** `DriverService`، و `UserHelper`.
- **ليش:**
  - الخطوة 4 بالخطة، وقاعدة 1 بـ CLAUDE.md.
  - الفحوصات تطابق Triggers قاعدة البيانات (`trg_lines_bi` و `trg_lineprices_bi`)، حتى المستخدم ياخذ رسالة واضحة بدل خطأ من القاعدة.
- **قاعدتان أضفناهما (اقتراح من التنفيذ):**
  1. سعر الخط الكامل (من أول محطة لآخر محطة) إجباري.
  2. الإغلاق يرفض الطلبات المعلقة.
- **كيف اختبرناها:** `LineServiceTests` (26 حالة).
- **سيناريو توضيحي:**
  > السائق أحمد (موافق عليه، وسيارته 12 مقعد) ينشئ خط "الكرادة - الجامعة" ببيانات:
  > - الانطلاق الساعة 7:00، و10 مقاعد.
  > - المحطات: ساحة الحرية ← شارع 62 ← الجامعة التكنولوجية.
  > - الأيام: الأحد والاثنين والثلاثاء.
  > - الأسعار: 50,000 دينار للخط الكامل، و30,000 دينار من ساحة الحرية لشارع 62.
  >
  > الخدمة تتأكد من كل شي، وتحفظ الخط مع محطاته الثلاث وأيامه وأسعاره بعملية واحدة.
  >
  > لو كتب 13 مقعد، يطلع له خطأ "عدد المقاعد لازم يكون من 1 إلى 12 (مقاعد سيارتك)". ولو كتب سعر من الجامعة لساحة الحرية (بالعكس)، يطلع له خطأ "محطة النزول لازم تكون بعد محطة الصعود".

### 4.5 البحث وطلب الاشتراك (الراكب)

- **الكلاسات والملفات:**
  - `SubscriptionService` بملف `TransportLines/Services/SubscriptionService.cs`
  - `PassengerController` بملف `TransportLines/Controllers/PassengerController.cs`
  - `RequestSubscriptionViewModel` بملف `TransportLines/ViewModels/RequestSubscriptionViewModel.cs`
- **أهم الدوال بالخدمة:**

| الدالة | الوظيفة |
|---|---|
| `SearchLinesAsync` و `GetOpenLinesAsync` و `LinesWithDetails` | البحث |
| `GetOpenLineAsync` | تفاصيل خط مفتوح |
| `RequestSubscriptionAsync` | الدالة الرئيسية لطلب الاشتراك |
| `GetActivePassengerAsync` و `FindLineForRequestAsync` | فحص الراكب والخط |
| `ValidateStopsAndPrice` و `FindPrice` و `GetStopOrder` | المحطات والسعر |
| `ValidateSeatsAndDates` | المقاعد والتواريخ والأشهر |
| `CheckNoOpenSubscriptionAsync` | طلب مفتوح واحد لكل خط |
| `SaveRequestAsync` و `CalculateEndDate` | الحفظ وحساب تاريخ النهاية |
| `GetMySubscriptionsAsync` | اشتراكاتي |
| `CancelSubscriptionAsync` و `FindOpenSubscriptionAsync` | الإلغاء |

- **Actions الكونترولر:** `SearchLines`، و `LineDetails`، و `RequestSubscription` (GET/POST)، و `MySubscriptions`، و `CancelSubscription` (POST).
- **شنو تسوي:**
  - **البحث:**
    - يرجع الخطوط المفتوحة اللي سائقها موافق عليه وحسابه فعّال.
    - إذا اختار الراكب محطتين، يرجع فقط الخطوط اللي تمر بيهم **بالترتيب الصحيح** وإلها سعر بينهم.
    - **الخطوط النسائية تظهر للراكبات فقط.**
  - **الطلب:**
    - ينحفظ بحالة `Pending`، و**ما يحجز مقاعد**.
    - السعر ينحفظ **نسخة** وقت الطلب بعمود `Subscriptions.MonthlyPrice`.
    - تاريخ النهاية = البداية + عدد الأشهر - يوم واحد. هذا يطابق قيد `chk_subscriptions_period` بالقاعدة.
    - السائق ياخذ إشعار نوعه `SubscriptionRequest`.
  - **قائمة المحطات بفورم الطلب** فيها محطات الخط نفسه فقط.
  - **الإلغاء:** الراكب يلغي طلبه المعلق أو اشتراكه الفعّال، وفقط اشتراكه هو. ينحفظ `CancelledAt` و `CancelReason`، والسائق ياخذ إشعار نوعه `SubscriptionCancelled`.
- **شنو تحتاج:**
  - **الجداول:** `Lines`، و `LineStops`، و `LinePrices`، و `LineDays`، و `Subscriptions`، و `Users`، و `Drivers`، و `Vehicles`، و `Notifications`.
  - **الكلاسات الثانية:** `LineService` (قائمة المحطات للبحث)، و `UserHelper`.
- **ليش:** الخطوة 5 بالخطة، والقواعد 2 و 3 و 5 و 6 بـ CLAUDE.md. حفظ السعر كنسخة يحمي الراكب لو تغيّر السعر لاحقاً.
- **كيف اختبرناها:** `SubscriptionServiceTests` (22 حالة).
- **سيناريو توضيحي:**
  > سارة تبحث من "ساحة الحرية" إلى "الجامعة التكنولوجية"، فيطلع لها خط أحمد بسعر 50,000. تطلب مقعد واحد يبدي باچر لمدة شهر، فينحفظ الطلب Pending بسعر 50,000، ويوصل لأحمد إشعار "طلب اشتراك جديد".
  > - لو حاولت تطلب مرة ثانية على نفس الخط، ترجع لها رسالة "عندك طلب أو اشتراك مفتوح على هذا الخط من قبل".
  > - لو كان الخط "للنساء فقط" وحاول راكب ذكر يطلبه، ما يطلع له بالبحث، وطلبه المباشر ينرفض.

### 4.6 موافقة السائق على الطلبات وفحص المقاعد لكل مقطع

- **الكلاسات والملفات:**
  - `SubscriptionApprovalService` بملف `TransportLines/Services/SubscriptionApprovalService.cs`
  - `DriverController`
  - `RejectRequestViewModel` بملف `TransportLines/ViewModels/RejectRequestViewModel.cs`
- **أهم الدوال بالخدمة:**

| الدالة | الوظيفة |
|---|---|
| `ApproveAsync` | الدالة الرئيسية للموافقة |
| `LockLineAsync` | قفل صف الخط |
| `FindPendingRequestAsync` و `CheckRequestState` | فحص حالة الطلب |
| `CheckSeatsAsync` و `CountSeatsOnSegment` و `GetStopOrdersAsync` و `GetOverlappingActiveAsync` و `AddNoSeatsError` | فحص المقاعد لكل مقطع |
| `ActivateSubscription` | تفعيل الطلب |
| `RejectAsync` | الرفض |
| `GetLineSubscriptionsAsync` | طلبات الخط لصاحبه |

- **Actions الكونترولر:** `LineRequests`، و `ApproveRequest` (POST)، و `RejectRequest` (POST).
- **شنو تسوي (الموافقة خطوة بخطوة):**
  1. تبدأ **Transaction**.
  2. **أول أمر:** قفل صف الخط بـ `SELECT ... FOR UPDATE` (عن طريق JOIN مع رقم الطلب). أي موافقة ثانية على نفس الخط تنتظر هنا لحد ما تخلص الأولى.
  3. التحقق: الطلب موجود وعلى خط هذا السائق، والسائق موافق عليه، والخط `Open`، والطلب `Pending`، ومدته ما خلصت.
  4. **فحص المقاعد لكل مقطع:** المقطع رقم k يعني من المحطة k إلى المحطة k+1. لكل مقطع يمر بيه الطلب، نجمع مقاعد الاشتراكات **الفعّالة** اللي:
     - تمر بنفس المقطع: صعودها عند بداية المقطع أو قبلها، ونزولها عند نهايته أو بعدها.
     - **وتتداخل تواريخها** ويا تواريخ الطلب.

     إذا المجموع + مقاعد الطلب أكثر من مقاعد الخط، الموافقة ترفض برسالة تذكر المقطع والمقاعد المتبقية.
  5. إذا أكو مكان، الحالة تصير `Active`، وينسجل `ApprovedAt`، والراكب ياخذ إشعار `SubscriptionApproved`. بعدها `SaveChanges` و `Commit`.
  - **الرفض:** السبب إجباري (من 5 إلى 255 حرف)، والحالة تصير `Rejected`، والراكب ياخذ إشعار `SubscriptionRejected`.
- **شنو تحتاج:**
  - **الجداول:** `Subscriptions`، و `Lines`، و `LineStops`، و `Drivers`، و `Notifications`.
  - **الإعداد:** Transaction (`BeginTransactionAsync`).
- **ليش:**
  - قاعدة 3: المقاعد تنفحص وقت الموافقة داخل Transaction تقفل صف الخط.
  - قاعدة 4: السعة محسوبة لكل مقطع، مو للخط كله.
  - **ليش القفل أول أمر:** MySQL (REPEATABLE READ) ياخذ نسخة ثابتة من البيانات عند أول قراءة عادية داخل الـ Transaction. إذا قرأنا الطلب قبل القفل، ممكن نشوف بيانات قديمة حتى بعد ما ناخذ القفل.
- **كيف اختبرناها:** `SubscriptionApprovalServiceTests` (14 حالة) على خط بيه 3 مقاعد ومحطات 1←2←3.
- **سيناريو توضيحي:**
  > خط بيه **3 مقاعد** ومحطات 1 ← 2 ← 3، يعني مقطعين: (1-2) و (2-3).
  >
  > | الطلب | من ← إلى | المقاعد | مقاعد المقطع 1-2 بعده | مقاعد المقطع 2-3 بعده | النتيجة |
  > |---|---|---|---|---|---|
  > | A | 1 ← 3 | 2 | 2 | 2 | **مقبول** |
  > | B | 1 ← 2 | 2 | 2+2 = 4 > 3 | — | **مرفوض** |
  > | C | 2 ← 3 | 1 | 2 | 2+1 = 3 | **مقبول** |
  > | D | 2 ← 3 | 1 | — | 3+1 = 4 > 3 | **مرفوض** |
  >
  > لو ضغط السائق "موافقة" على B و C بنفس اللحظة من جهازين، الأمر الثاني ينتظر القفل. بعدها يشوف المقاعد المحدّثة، فما يصير حجز أكثر من المقاعد.

---

## 5) الفالديشن (التحقق من المدخلات)

### 5.1 الطبقتان

| الطبقة | المكان | الدور |
|---|---|---|
| الأولى | Data Annotations على الـ ViewModels (`[Required]`، `[StringLength]`، `[RegularExpression]`، `[Range]`، `[Compare]`، `[EmailAddress]`) | فحص الصيغ برسائل عربية. راح تشغّل أيضاً الفالديشن بالمتصفح (jQuery unobtrusive) لما نبني الـ Views |
| الثانية | الخدمات (Services) | الفحص **الحقيقي** اللي ما ينتجاوز: يعيد فحص الصيغ، ويفحص القواعد اللي تحتاج قاعدة البيانات (التكرار، الملكية، المقاعد، الحالات) |

**ليش طبقتين:** الطبقة الأولى ممكن أحد يتجاوزها بتعديل الطلب، والثانية مستحيل تنتجاوز لأنها بالسيرفر داخل الخدمة.

### 5.2 قواعد التحقق المنفذة

**فورم التسجيل** (`RegisterViewModel` + `AccountService`):

| الحقل | القاعدة |
|---|---|
| نوع الحساب | `Passenger` أو `Driver` فقط (Admin مرفوض) |
| الاسم | حروف ومسافات فقط، من 3 إلى 100 |
| رقم الهاتف | يبدأ بـ `07` وبعده 9 أرقام، وما يتكرر |
| الإيميل | اختياري، صيغة صحيحة، وما يتكرر (بغض النظر عن حجم الحروف) |
| كلمة المرور | من 8 إلى 64 حرف إنكليزي أو رقم أو رمز، وبيها حرف واحد ورقم واحد على الأقل، ويطابقها التأكيد |
| الجنس | `Male` أو `Female` |
| رقم الإجازة (سائق) | حروف إنكليزية أو أرقام أو شرطة، من 3 إلى 30، وما يتكرر |
| انتهاء الإجازة (سائق) | تاريخ بالمستقبل |
| البطاقة الوطنية (سائق) | أرقام فقط، من 8 إلى 20، وما تتكرر |
| نوع السيارة | موجود وفعّال بجدول `VehicleTypes` |
| عدد المقاعد | من 1 إلى `MaxSeats` لنوع السيارة |
| رقم اللوحة | حروف وأرقام ومسافات وشرطة، من 2 إلى 20، وما يتكرر |
| سنة الصنع | اختيارية، من 1980 إلى السنة القادمة |
| انتهاء الفحص | تاريخ بالمستقبل |
| الصور | الإجازة والبطاقة والسنوية إجبارية، وصورة السيارة اختيارية. كلها تمر بفحوصات `FileUploadService` |

**فورم الدخول** (`LoginViewModel`): رقم الهاتف بنفس الصيغة، وكلمة المرور مطلوبة وأقصاها 64 حرف.

**فورم إنشاء الخط** (`CreateLineViewModel` + `LineService`):

| الحقل | القاعدة |
|---|---|
| الاسم | اختياري، حروف وأرقام ومسافات وشرطة، من 3 إلى 100 |
| وقت الانطلاق | مطلوب، وداخل اليوم |
| عدد المقاعد | من 1 إلى مقاعد سيارة السائق |
| أقل مدة اشتراك | من 1 إلى 12 شهر |
| الملاحظات | أقصاها 255 حرف |
| الأيام | يوم واحد على الأقل، وكل يوم من 0 إلى 6، بدون تكرار |
| المحطات | من 2 إلى 20، موجودة وفعّالة، بدون تكرار، وأوقات الوصول بالترتيب وبعد وقت الانطلاق |
| الأسعار | بين محطتين من الخط، والصعود قبل النزول. رقم صحيح أكبر من صفر وأقل من 10,000,000. بدون تكرار، وسعر الخط الكامل إجباري |

**فورم طلب الاشتراك** (`RequestSubscriptionViewModel` + `SubscriptionService`):

| الحقل | القاعدة |
|---|---|
| المستخدم | راكب فعّال |
| الخط | مفتوح، وسائقه موافق عليه. والنسائي للراكبات فقط |
| المحطات | على الخط، والنزول بعد الصعود، وإلها سعر محدد |
| المقاعد | من 1 إلى مقاعد الخط |
| تاريخ البداية | من اليوم إلى 60 يوم قدام |
| عدد الأشهر | من `MinSubscriptionMonths` للخط إلى 12 |
| التكرار | طلب أو اشتراك مفتوح واحد فقط لكل خط |

**فورمات الرفض والإلغاء:**
- سبب رفض السائق أو رفض طلب الاشتراك: إجباري، من 5 إلى 255 حرف.
- سبب الإلغاء من الراكب: اختياري، أقصاه 255 حرف.

---

## 6) الاختبارات

### 6.1 طريقة الاختبار

- **الإطار:** xUnit، بمشروع منفصل `TransportLines.Tests`.
- **قاعدة البيانات بالاختبارات:**
  - كل اختبار ياخذ **قاعدة بيانات وهمية جديدة بالذاكرة** (EF Core InMemory) باسم عشوائي، فالاختبارات ما تأثر على بعض، و**ما تلمس القاعدة الحقيقية**.
  - الاستثناء الوحيد: `DatabaseConnectionTests`، اللي يتصل بالقاعدة الحقيقية للقراءة فقط.
- **الملف المساعد** `TestHelper.cs` (`TransportLines.Tests/TestHelper.cs`):

| الدالة | الوظيفة |
|---|---|
| `CreateDatabase` | ينشئ قاعدة وهمية بيها 3 أدوار، ونوعين سيارات (واحد موقوف)، ومنطقة، و5 محطات (وحدة موقوفة) |
| `CreateJpg` و `CreatePng` و `CreateFile` | صور وملفات وهمية |
| `ValidPassenger` و `ValidDriver` و `ValidLine` | فورمات صحيحة جاهزة |
| `AddAdmin` و `AddApprovedDriverAsync` و `AddPassengerAsync` | إنشاء مستخدمين للاختبار |

- **أسلوب الاختبارات:**
  - كل اختبار يبدأ من فورم صحيح ويغيّر حقل واحد فقط، حتى نتأكد إن هذا الحقل بالذات هو سبب الرفض.
  - استخدمنا `[Theory]` و `[InlineData]` حتى نجرب عدة قيم خاطئة بنفس الاختبار.

### 6.2 نتائج الاختبارات

| كلاس الاختبار | عدد الحالات | شنو يغطي |
|---|---|---|
| `DatabaseConnectionTests` | 2 | الاتصال بالقاعدة الحقيقية، وقراءة جدول Roles |
| `FileUploadServiceTests` | 17 | قبول الصور الحقيقية، ورفض الكبيرة والخطيرة والمزيفة، والاسم العشوائي، والحذف، والمسار الآمن، ومحاولات Path Traversal |
| `AccountServiceRegisterTests` | 32 | تسجيل الراكب والسائق، ورفض Admin والتكرار والصيغ الخاطئة وSQL Injection و`<script>`، والصور الناقصة والمزيفة، والتواريخ والمقاعد ونوع السيارة |
| `AccountServiceLoginTests` | 10 | الدخول الصحيح، والرسالة الموحدة، وSQL Injection، والقيم الفارغة، والحساب الموقوف والمحذوف، وحساسية الحروف |
| `AccountServiceHelperTests` | 7 | الـ Claims، وفحص حالة الحساب، وأنواع السيارات الفعّالة |
| `AdminServiceTests` | 15 | قائمة المعلقين، والموافقة والرفض مع الإشعارات، ومنع غير الأدمن والأدمن الموقوف، والإجازة المنتهية، وسبب الرفض، وأنواع الصور المسموحة |
| `DriverServiceTests` | 4 | الملف الشخصي، وحالة الموافقة، ورسائل الحالة |
| `LineServiceTests` | 26 | إنشاء الخط وكل حالات الرفض (السائق، السيارة، الأيام، المحطات، الأسعار)، والملكية، والإغلاق |
| `SubscriptionServiceTests` | 22 | البحث والفلترة والخط النسائي، والطلب وكل حالات الرفض، وتاريخ النهاية، واشتراكاتي، والإلغاء |
| `SubscriptionApprovalServiceTests` | 14 | الموافقة والرفض، وفحص المقاعد لكل مقطع، والتواريخ المتداخلة، والطلب المعلق ما يحجز، والإلغاء يحرر مقاعد |
| **المجموع** | **149** | **كلها ناجحة** |

### 6.3 فحص يدوي على السيرفر (curl)

شغّلنا الموقع على `https://localhost:7299` وتأكدنا من:

| الفحص | النتيجة |
|---|---|
| رؤوس الحماية بالصفحة الرئيسية | موجودة: `nosniff`، `DENY`، `Referrer-Policy`، `Content-Security-Policy` |
| فتح صفحة محمية بدون دخول | 302 ← `/Account/Login` |
| POST على الدخول بدون Anti-Forgery Token | 400 |
| أكثر من 5 محاولات دخول بالدقيقة | 429 |

### 6.4 ما لم يُختبر بعد (صراحةً)

- أمر القفل `SELECT ... FOR UPDATE` ما يشتغل على القاعدة الوهمية، فـ**لم يُجرّب على MySQL الحقيقية بعد**.
- الحفظ الفعلي على MySQL (خصوصاً أسعار الخط المرتبطة بالمحطات بمفتاح مركب، والـ Triggers) **لم يُجرّب بعد**.
- الـ Controllers ما إلها اختبارات وحدة (Unit Tests). تنختبر من الواجهة بالمرحلة 3.
- **لا يوجد اختبار بالمتصفح** لأن الـ Views غير موجودة.

هذي كلها مخطط إلها بالمرحلة 3، وأي تشغيل على القاعدة الحقيقية يكون بعد موافقة الطالب.

---

## 7) ما لم ينفذ بعد

الأشياء التالية **غير منفذة** بالكود حالياً:

### 7.1 من المرحلة 1 (الخطوة 7 بالخطة)
- **الانتهاء التلقائي للاشتراكات:** تحويل `Active` إلى `Expired` بعد `EndDate`.
- **رسائل السائق لكل مشتركي الخط** (عطلة، عطل بالسيارة)، بإشعارات نوعها `LineMessage` (قاعدة 7).
- **التقييمات** (جدول `Ratings`، من 1 إلى 5).
- **صفحة الإشعارات وتعليمها كمقروءة:** الإشعارات **تنحفظ** حالياً بالقاعدة، لكن ماكو خدمة أو صفحة لعرضها.
- **إيقاف الخط مؤقتاً** (حالة `Suspended`) ثم إعادة فتحه.

### 7.2 المرحلة 2 (الواجهات)
- كل الـ Views: التسجيل (مع إظهار حقول السائق وإخفائها)، والدخول، وصفحات الأدمن والسائق والراكب.
- الفالديشن بالمتصفح (jQuery unobtrusive). الـ Annotations جاهزة، لكنها ما تشتغل بدون Views.
- التصميم (Responsive و RTL)، ورسائل النجاح والخطأ (`TempData`)، وتأكيد الأفعال الخطيرة (إلغاء، رفض، إغلاق).

### 7.3 المرحلة 3 (الربط والاختبار)
- تشغيل وتجربة كل الميزات على MySQL الحقيقية، بضمنها القفل والـ Triggers.
- قائمة اختبار يدوية بالمتصفح.

### 7.4 أشياء أخرى غير منفذة
- **سكربت إنشاء حساب الأدمن** (`sql/04_seed_admin.sql`): مقترح وينتظر موافقة الطالب. حالياً ماكو طريقة لإنشاء أدمن من البرنامج، وهذا مقصود.
- **نسخ ملفات SQL** (`01_create_schema.sql` و `02_schema_tests.sql`) لمجلد `sql/` داخل المشروع: لسه موجودة على سطح المكتب.
- **إدارة المستخدمين من الأدمن** (إيقاف أو حظر حساب، وإضافة محطات ومناطق وأنواع سيارات من الواجهة).
- **عرض السائق لصور وثائقه** (حالياً الأدمن فقط يشوفها).
- **صورة الملف الشخصي** (`ProfilePhotoURL`) و**توثيق رقم الهاتف** (`IsPhoneVerified`): الأعمدة موجودة لكن غير مستخدمة.
- **أكثر من سيارة للسائق:** التسجيل ينشئ سيارة واحدة، والخط ياخذ أول سيارة فعّالة.

---

## 8) جدول الكلاسات ووظائفها

| الكلاس | الملف | الوظيفة |
|---|---|---|
| `Program` (ملف البداية) | `TransportLines/Program.cs` | يسجل الخدمات (الداتابيس، الأمان، الـ MVC) ويرتب الـ Middleware ويشغّل الموقع |
| `AppDbContext` | `TransportLines/Data/AppDbContext.cs` | مولّد: يمثل قاعدة البيانات وجداولها (DbSet لكل جدول) |
| كلاسات `Models` (15) | `TransportLines/Models/*.cs` | مولّدة: كل كلاس يمثل جدول |
| `SecurityHelper` | `TransportLines/Security/SecurityHelper.cs` | إعدادات الـ Cookie، والصلاحيات الافتراضية، وفلتر CSRF، وحد المحاولات، ورؤوس الحماية، وفحص الحساب مع كل طلب |
| `UserHelper` | `TransportLines/Security/UserHelper.cs` | يقرأ رقم المستخدم الداخل من الـ Cookie |
| `ServiceResult` | `TransportLines/Services/ServiceResult.cs` | نتيجة الخدمات: قائمة أخطاء (الحقل والرسالة) |
| `LoginResult` | `TransportLines/Services/LoginResult.cs` | نتيجة الدخول وبيانات المستخدم |
| `FileUploadService` | `TransportLines/Services/FileUploadService.cs` | فحص الصور وحفظها باسم عشوائي خارج wwwroot، وقراءتها بمسار آمن |
| `AccountService` | `TransportLines/Services/AccountService.cs` | التسجيل (راكب أو سائق مع سيارته)، والدخول بـ bcrypt، والـ Claims، وفحص حالة الحساب |
| `AdminService` | `TransportLines/Services/AdminService.cs` | مراجعة السائقين (موافقة أو رفض مع إشعار) ومسارات صورهم |
| `DriverService` | `TransportLines/Services/DriverService.cs` | ملف السائق وحالة الموافقة |
| `LineService` | `TransportLines/Services/LineService.cs` | إنشاء الخط (محطات، أيام، أسعار)، وخطوط السائق، وإغلاق الخط |
| `SubscriptionService` | `TransportLines/Services/SubscriptionService.cs` | البحث عن الخطوط، وطلب الاشتراك، واشتراكاتي، والإلغاء |
| `SubscriptionApprovalService` | `TransportLines/Services/SubscriptionApprovalService.cs` | موافقة السائق على الطلبات مع قفل الخط وفحص المقاعد لكل مقطع، والرفض |
| `HomeController` | `TransportLines/Controllers/HomeController.cs` | الصفحة الرئيسية والخصوصية والخطأ (عامة) |
| `AccountController` | `TransportLines/Controllers/AccountController.cs` | التسجيل، والدخول، والخروج، وصفحة "ما عندك صلاحية" |
| `AdminController` | `TransportLines/Controllers/AdminController.cs` | صفحات الأدمن: السائقين المعلقين، والتفاصيل، والموافقة، والرفض، والصور |
| `DriverController` | `TransportLines/Controllers/DriverController.cs` | صفحات السائق: الملف، والخطوط، وإنشاء خط، والإغلاق، والطلبات، والموافقة، والرفض |
| `PassengerController` | `TransportLines/Controllers/PassengerController.cs` | صفحات الراكب: البحث، والتفاصيل، وطلب اشتراك، واشتراكاتي، والإلغاء |
| `RegisterViewModel` | `TransportLines/ViewModels/RegisterViewModel.cs` | فورم التسجيل (حقول مشتركة وحقول السائق والسيارة والصور) |
| `LoginViewModel` | `TransportLines/ViewModels/LoginViewModel.cs` | فورم الدخول |
| `RejectDriverViewModel` | `TransportLines/ViewModels/RejectDriverViewModel.cs` | فورم رفض السائق |
| `CreateLineViewModel` و `LineStopInput` و `LinePriceInput` | `TransportLines/ViewModels/CreateLineViewModel.cs` | فورم إنشاء الخط وصفوف المحطات والأسعار |
| `RequestSubscriptionViewModel` | `TransportLines/ViewModels/RequestSubscriptionViewModel.cs` | فورم طلب الاشتراك |
| `RejectRequestViewModel` | `TransportLines/ViewModels/RejectRequestViewModel.cs` | فورم رفض طلب الاشتراك |
| `ErrorViewModel` | `TransportLines/ViewModels/ErrorViewModel.cs` | بيانات صفحة الخطأ (من قالب MVC) |
| `TestHelper` | `TransportLines.Tests/TestHelper.cs` | أدوات الاختبار: قاعدة وهمية، وبيانات جاهزة، وصور وهمية |
| كلاسات الاختبار (10) | `TransportLines.Tests/*Tests.cs` | 149 حالة اختبار، كلها ناجحة |
