# سجل التغييرات (CHANGES)

كل تعديل أو تنصيب بالمشروع يتسجل هنا: التاريخ، شنو انسوى، ليش، والملفات أو الأوامر المتأثرة.

---

## 2026-11-21 — المرحلة 1 / الخطوة 1: إنشاء المشروع والاتصال بالداتابيس

### 1. إنشاء الـ Solution ومشروع MVC
- **شنو سويت:** أنشأت solution باسم `TransportLines` ومشروع ASP.NET Core MVC بنفس الاسم، بإطار .NET 8 (LTS).
- **ليش:** حسب الاتفاق مع المشرف (C# + ASP.NET Core MVC + .NET 8).
- **ملاحظة:** الجهاز بيه SDK 11 بس، لكنه يكدر يبني مشاريع .NET 8، و runtime 8.0.34 منصّب، فما احتجنا ننصب شي إضافي.
- **الأوامر:**
  ```
  dotnet new sln -n TransportLines
  dotnet new mvc -n TransportLines -o TransportLines -f net8.0
  dotnet sln TransportLines.slnx add TransportLines/TransportLines.csproj
  ```
- **الملفات:** `TransportLines.slnx`، مجلد `TransportLines/` كامل (قالب MVC الافتراضي).

### 2. ملف .gitignore
- **شنو سويت:** أضفت `.gitignore` القياسي لمشاريع .NET.
- **ليش:** حتى مجلدات `bin/` و `obj/` والملفات المؤقتة ما تنرفع على git.
- **الأمر:** `dotnet new gitignore`
- **الملفات:** `.gitignore`

### 3. أداة dotnet-ef (محلية للمشروع)
- **شنو سويت:** نصبت `dotnet-ef` نسخة 8.0.13 كأداة محلية (tool manifest).
- **ليش:** الأداة العامة بالجهاز نسختها 10، والمشروع يستخدم EF Core 8، فثبّتنا نسخة مطابقة داخل المشروع حتى ما يصير تعارض، وأي جهاز ثاني يسوي `dotnet tool restore` ياخذ نفس النسخة.
- **الأوامر:**
  ```
  dotnet new tool-manifest
  dotnet tool install dotnet-ef --version 8.0.11
  dotnet tool update dotnet-ef --version 8.0.13
  ```
- **الملفات:** `dotnet-tools.json`

### 4. حزم NuGet
- **شنو سويت:** أضفت حزمتين للمشروع:
  - `Pomelo.EntityFrameworkCore.MySql` 8.0.3 — مزوّد EF Core للـ MySQL (يجيب معاه EF Core 8.0.13).
  - `Microsoft.EntityFrameworkCore.Design` 8.0.13 — مطلوبة لأمر الـ scaffold (توليد الـ Models).
- **ليش:** Database-First مع MySQL حسب CLAUDE.md. خليت نسخة Design = 8.0.13 حتى تطابق نسخة EF Core اللي جابها Pomelo.
- **الأوامر:**
  ```
  dotnet add package Pomelo.EntityFrameworkCore.MySql --version 8.0.3
  dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.13
  ```
- **الملفات:** `TransportLines/TransportLines.csproj`

### 5. User Secrets لسلسلة الاتصال
- **شنو سويت:** فعّلت user-secrets للمشروع.
- **ليش:** حتى كلمة مرور المستخدم `transport_app` ما تنكتب بالكود ولا بـ `appsettings.json` ولا تنرفع على git (قاعدة أمان بـ CLAUDE.md). تنحفظ بملف خاص بحساب ويندوز خارج مجلد المشروع.
- **الأمر:** `dotnet user-secrets init`
- **الملفات:** `TransportLines/TransportLines.csproj` (انضاف `UserSecretsId`)

### 6. توليد الـ Models من الداتابيس (Scaffold)
- **شنو سويت:** ولّدت كلاسات الـ Models والـ AppDbContext من الداتابيس الموجودة `line_subscription` بالمستخدم `transport_app`.
- **ليش:** المشروع Database-First (الداتابيس موجودة، ما نستخدم migrations).
- **الخيارات:** `--data-annotations` (الإعدادات تنكتب كـ Attributes فوق الخصائص)، `--no-onconfiguring` (سلسلة الاتصال ما تنكتب داخل الـ DbContext).
- **ملاحظة:** هذا الأمر يقرا شكل الجداول بس، وما غيّر أي شي بالداتابيس.
- **ملاحظة 2:** تولّد 15 كلاس: الـ 14 جدول + جدول `test_results` (موجود بالداتابيس، الظاهر من سكربت الاختبارات 02). أسماء الكلاسات طلعت مثل `Lineday` و `Linestop` لأن MySQL على ويندوز يحفظ أسماء الجداول بحروف صغيرة.
- **الأمر:**
  ```
  dotnet tool run dotnet-ef dbcontext scaffold Name=ConnectionStrings:DefaultConnection Pomelo.EntityFrameworkCore.MySql --context AppDbContext --context-dir Data --output-dir Models --data-annotations --no-onconfiguring --force
  ```
- **الملفات:** `TransportLines/Models/*.cs` (15 ملف)، `TransportLines/Data/AppDbContext.cs`

### 7. تسجيل AppDbContext بـ Program.cs
- **شنو سويت:** خليت `Program.cs` يقرا سلسلة الاتصال من user-secrets، وسجلت `AppDbContext` بنسخة MySQL 8.0.45، وضفت تعليق عربي فوق كل خطوة.
- **ليش:** حتى الـ Controllers والـ Services تستلم الـ DbContext جاهز. كتبت نسخة MySQL يدوياً حتى البرنامج ما يفتح اتصال إضافي عند التشغيل بس حتى يعرفها.
- **الملفات:** `TransportLines/Program.cs`

### 8. نقل ErrorViewModel إلى مجلد ViewModels
- **شنو سويت:** نقلت `ErrorViewModel.cs` (جاي من قالب MVC) من `Models/` إلى `ViewModels/` وصححت الـ namespace.
- **ليش:** حسب CLAUDE.md، مجلد `Models/` بس للكلاسات المولّدة من الداتابيس.
- **الملفات:** `TransportLines/ViewModels/ErrorViewModel.cs`، `TransportLines/Views/_ViewImports.cshtml`، `TransportLines/Controllers/HomeController.cs`

### 9. مشروع الاختبارات (xUnit)
- **شنو سويت:** أنشأت مشروع `TransportLines.Tests` وضفت اختبارين يتأكدون من الاتصال بالداتابيس وقراءة جدول Roles. الاختبارين يقرون بس وما يكتبون بالداتابيس.
- **ليش:** حتى كل خطوة يكون إلها اختبار (CLAUDE.md). خليت مشروع الاختبار جنب مشروع الويب مو جواه، لأن لو صار جواه ينبني وياه ويصير تعارض.
- **الأوامر:**
  ```
  dotnet new xunit -n TransportLines.Tests -o TransportLines.Tests -f net8.0
  dotnet sln TransportLines.slnx add TransportLines.Tests/TransportLines.Tests.csproj
  dotnet add reference ../TransportLines/TransportLines.csproj
  dotnet add package Microsoft.Extensions.Configuration.UserSecrets --version 8.0.1
  ```
- **الملفات:** `TransportLines.Tests/TransportLines.Tests.csproj` (فيه نفس `UserSecretsId` مال مشروع الويب)، `TransportLines.Tests/DatabaseConnectionTests.cs`
- **النتيجة:** `dotnet test` نجح، 2 من 2.

### 10. تصحيح قواعد الكود (تعليقات عربية + تبسيط)
- **شنو سويت:** ضفت تعليق عربي فوق كل كلاس وكل دالة وفوق كل خطوة مهمة بالملفات اللي كتبتها. شلت الـ `??` والـ lambda المعقدة وخليت مكانها `if` عادية، وشلت `_logger` من HomeController لأنه ما مستخدم.
- **ليش:** طلبك بقواعد الكود (كود بسيط بمستوى طالب جامعة).
- **الملفات:** `Program.cs`، `Controllers/HomeController.cs`، `ViewModels/ErrorViewModel.cs`، `TransportLines.Tests/DatabaseConnectionTests.cs`

---

## 2026-11-21 — المرحلة 1 / الخطوة 2: خدمة الحسابات (تسجيل + دخول) والفالديشن والأمان

### 11. حزم جديدة
- **شنو سويت:** نصبت `BCrypt.Net-Next` 4.0.3 بمشروع الويب، و `Microsoft.EntityFrameworkCore.InMemory` 8.0.13 بمشروع الاختبارات.
- **ليش:** bcrypt لتشفير كلمات المرور (قاعدة 8 بـ CLAUDE.md). InMemory حتى الاختبارات تشتغل على داتابيس وهمية بالذاكرة وما تلمس الداتابيس الحقيقية.
- **الأوامر:**
  ```
  dotnet add package BCrypt.Net-Next --version 4.0.3
  dotnet add package Microsoft.EntityFrameworkCore.InMemory --version 8.0.13
  ```
- **الملفات:** `TransportLines/TransportLines.csproj`، `TransportLines.Tests/TransportLines.Tests.csproj` (وانضاف بيه `FrameworkReference Microsoft.AspNetCore.App` حتى الاختبارات تستخدم `IFormFile`)

### 12. ViewModels للتسجيل والدخول
- **شنو سويت:** أنشأت `RegisterViewModel` (حقول الراكب + حقول السائق والسيارة والصور) و `LoginViewModel`، بيهم Data Annotations برسائل عربية.
- **ليش:** هذي الطبقة الأولى من الفالديشن، وهي نفسها راح تشغّل الفالديشن بالمتصفح (jQuery unobtrusive) لما نبني الواجهة.
- **الملفات:** `TransportLines/ViewModels/RegisterViewModel.cs`، `TransportLines/ViewModels/LoginViewModel.cs`

### 13. كلاسات النتائج
- **شنو سويت:** أنشأت `ServiceResult` (قائمة أخطاء: اسم الحقل + الرسالة) و `LoginResult` (نتيجة الدخول وبيانات المستخدم).
- **ليش:** حتى الخدمة ترجع الأخطاء والكونترولر يعرض كل خطأ جنب حقله.
- **الملفات:** `TransportLines/Services/ServiceResult.cs`، `TransportLines/Services/LoginResult.cs`

### 14. خدمة رفع الصور FileUploadService
- **شنو سويت:** خدمة تفحص كل صورة مرفوعة وتحفظها بشكل آمن:
  - الحجم أقل من 2 ميگا، والامتداد jpg أو jpeg أو png بس.
  - تفحص أول بايتات الملف (توقيع JPG أو PNG)، حتى تنرفض الملفات اللي اسمها .jpg وهي مو صورة.
  - تحفظ الملف باسم عشوائي (Guid) بمجلد `App_Data/uploads` خارج `wwwroot`، حتى محد يكدر يفتح صور الإجازات والبطاقات برابط مباشر.
  - تحذف الصور إذا فشل الحفظ بالداتابيس.
- **ليش:** قاعدة الرفع بـ CLAUDE.md (صور بس، حجم محدود، اسم عشوائي، خارج المجلدات التنفيذية).
- **الملفات:** `TransportLines/Services/FileUploadService.cs`، و `.gitignore` (انضاف `TransportLines/App_Data/` حتى الصور الشخصية ما تنرفع على git)

### 15. خدمة الحسابات AccountService
- **شنو سويت:**
  - **التسجيل:** فالديشن بالسيرفر ما ينتجاوز:
    - نوع الحساب Passenger أو Driver بس (Admin مرفوض).
    - الاسم حروف بس، والهاتف 07 وبعده 9 أرقام، والإيميل يتحول لحروف صغيرة.
    - كلمة المرور 8 إلى 64 حرف إنكليزي وبيها حرف ورقم.
    - رقم الهاتف والإيميل ما يتكررون.
    - للسائق: الإجازة سارية، رقم البطاقة أرقام بس، ورقم الإجازة والبطاقة واللوحة ما يتكررون.
    - نوع السيارة موجود وفعّال، وعدد المقاعد ما يتجاوز الحد الأعلى لنوعها.
    - الفحص ساري، والصور الثلاث إجبارية.
  - **الحفظ:** كلمة المرور تنحفظ هاش bcrypt. السائق ينحفظ User + Driver (Pending) + Vehicle بنفس الـ SaveChanges (Transaction وحدة).
  - **الدخول:** رسالة خطأ وحدة للرقم الغلط والباسورد الغلط، حتى ما ينكشف إذا الرقم مسجل لو لا. يفحص هاش وهمي إذا الرقم مو موجود حتى وقت الرد يبقى نفسه. الحساب المحذوف ما يدخل، والموقوف ياخذ رسالة "حسابك موقوف". ويحدّث `LastLoginAt`.
  - كل الاستعلامات LINQ (parameterized)، ماكو SQL مكتوب بالنص، فـ SQL Injection ما يشتغل.
- **ليش:** CLAUDE.md (قواعد التسجيل + قواعد الأمان + فالديشن بطبقتين).
- **الملفات:** `TransportLines/Services/AccountService.cs`

### 16. تسجيل الخدمات بـ Program.cs
- **شنو سويت:** سجلت `FileUploadService` (Singleton، مساره `App_Data/uploads`) و `AccountService` (Scoped).
- **الملفات:** `TransportLines/Program.cs`

### 17. اختبارات xUnit
- **شنو سويت:** 52 اختبار جديد (المجموع 54):
  - `FileUploadServiceTests`: صور صحيحة تنقبل. ملف فارغ، أو حجم كبير، أو امتدادات خطيرة (exe/php/html)، أو ملف نصي اسمه .jpg، كلها تنرفض. الاسم العشوائي والحذف يشتغلون.
  - `AccountServiceRegisterTests`: تسجيل راكب وسائق صحيح. رفض Admin، ورفض الهاتف والإيميل المكرر، وأرقام هاتف غلط (منها محاولة SQL Injection)، وكلمات مرور ضعيفة، واسم بيه `<script>`. رفض الصور الناقصة والمزيفة، والتواريخ المنتهية، والمقاعد الزايدة، ونوع السيارة الموقوف، واللوحة المكررة.
  - `AccountServiceLoginTests`: دخول صحيح. باسورد غلط، أو رقم مو مسجل (نفس الرسالة)، أو SQL Injection، أو قيم فارغة، أو حساب موقوف أو محذوف، كلها ترفض. وكلمة المرور حساسة للحروف الكبيرة والصغيرة.
- **ملاحظة:** Windows Defender حذف ملف اختبار لأن بيه نص يشبه هجوم PHP، فبدلته بنص عادي يختبر نفس الشي.
- **الملفات:** `TransportLines.Tests/TestHelper.cs`، `FileUploadServiceTests.cs`، `AccountServiceRegisterTests.cs`، `AccountServiceLoginTests.cs`
- **النتيجة:** `dotnet test` نجح، 54 من 54، بدون تحذيرات.

---

## 2026-11-21 — المرحلة 1 / الخطوة 3: AccountController والأمان العام للموقع

### 18. دوال جديدة بـ AccountService
- **شنو سويت:** ضفت ثلاث دوال:
  - `CreatePrincipal`: تبني الـ Claims (الرقم، الاسم، الدور) اللي تنحفظ بالـ Cookie.
  - `IsUserActiveAsync`: تفحص إن الحساب موجود وفعّال ومو محذوف.
  - `GetActiveVehicleTypesAsync`: ترجع أنواع السيارات الفعّالة لقائمة الاختيار بالفورم.
- **ليش:** حتى الكونترولر يبقى خفيف وكل المنطق بالخدمة.
- **الملفات:** `TransportLines/Services/AccountService.cs`

### 19. تغيير اسم حقل Model إلى VehicleModel
- **شنو سويت:** غيرت اسم خاصية موديل السيارة بـ `RegisterViewModel` من `Model` إلى `VehicleModel`.
- **ليش:** البناء طلع تحذير MVC1004، لأن اسم الخاصية `Model` نفس اسم البارامتر `model` بالكونترولر، وهذا يخرب ربط بيانات الفورم (Model Binding)، وكان راح يلخبط بالـ View (`Model.Model`). عمود الداتابيس `Vehicles.Model` ما تغير.
- **الملفات:** `ViewModels/RegisterViewModel.cs`، `Services/AccountService.cs`، `TransportLines.Tests/TestHelper.cs`

### 20. SecurityHelper (أدوات الأمان العامة)
- **شنو سويت:** كلاس بمجلد جديد `Security/` بيه:
  - **إعدادات الـ Cookie:** HttpOnly، وSecure، وSameSite=Strict، وتنتهي بعد 8 ساعات بدون نشاط.
  - **تحديد المحاولات (Rate Limiting) لكل IP:** الدخول 5 بالدقيقة، والتسجيل 5 كل 10 دقائق، وبعدها 429.
  - **سياسة افتراضية:** كل صفحة تحتاج دخول إلا اللي عليها `[AllowAnonymous]`.
  - **فلتر عام:** كل فورم POST لازم يحمل Anti-Forgery Token (ضد CSRF).
  - **رؤوس حماية:** nosniff، وX-Frame-Options، وReferrer-Policy، وContent-Security-Policy.
  - **فحص الـ Cookie مع كل طلب:** إذا الحساب انوقف أو انحذف بعد الدخول، يطلع فوراً.
- **ليش:** قواعد الأمان بـ CLAUDE.md، وضد تخمين كلمات المرور. ماكو عمود للمحاولات الفاشلة بالداتابيس، فاستخدمت Rate Limiting بدون ما نغير الداتابيس. حطيت الإعدادات بدوال بأسماء واضحة بدل lambda طويلة بـ Program.cs.
- **الملفات:** `TransportLines/Security/SecurityHelper.cs`

### 21. Program.cs
- **شنو سويت:** ربطت إعدادات SecurityHelper بـ Program.cs:
  - Cookie Authentication، وAuthorization ويا السياسة الافتراضية، وRate Limiter، وفلتر CSRF، ورؤوس الحماية.
  - رتبت الـ middleware بهذا الترتيب: Routing ← RateLimiter ← Authentication ← Authorization.
- **الملفات:** `TransportLines/Program.cs`

### 22. AccountController
- **شنو سويت:** كونترولر بيه هذي الـ Actions:
  - `Register` GET/POST: فالديشن بطبقتين، وكل خطأ ينعرض جنب حقله، والفورم يحتفظ بالبيانات عدا كلمات المرور. حجم الطلب أقصاه 10 ميگا.
  - `Login` GET/POST: يمنع Open Redirect بـ `Url.IsLocalUrl`.
  - `Logout`: POST فقط.
  - `AccessDenied`.
  - كل POST عليه `[ValidateAntiForgeryToken]`.
- **ملاحظة:** الـ Views مال هذي الصفحات تنبني بالمرحلة 2، فهسه فتح الصفحة بالمتصفح يطلع خطأ "View not found"، وهذا متوقع.
- **الملفات:** `TransportLines/Controllers/AccountController.cs`، و `Controllers/HomeController.cs` (انضاف `[AllowAnonymous]` لأن صفحاته عامة)

### 23. الاختبارات
- **شنو سويت:** ضفت `AccountServiceHelperTests` بيه 7 اختبارات:
  - الـ Claims صحيحة.
  - الحساب الفعّال يرجع true.
  - الحساب الموقوف أو المحظور أو المحذوف أو الغير موجود يرجع false.
  - قائمة أنواع السيارات ترجع الفعّالة بس.
- **النتيجة:** `dotnet test` نجح، 61 من 61، بدون تحذيرات.
- **فحص يدوي على السيرفر الحقيقي (curl):**
  - رؤوس الحماية موجودة.
  - الصفحة المحمية تحوّل لـ /Account/Login (302).
  - POST بدون Anti-Forgery Token يرجع 400.
  - بعد 5 محاولات دخول بالدقيقة يرجع 429.
- **ملاحظة تشغيل:** لازم `ASPNETCORE_ENVIRONMENT=Development` حتى user-secrets تنقرا.

---

## 2026-11-21 — المرحلة 1 / الخطوة 4: مراجعة السائقين (الأدمن) وملف السائق

### 24. FileUploadService: قراءة الصور بشكل آمن
- **شنو سويت:** ضفت دالتين:
  - `GetSafeFullPath`: ترجع المسار الكامل للصورة بس إذا هي داخل مجلد الرفع وموجودة.
  - `GetContentType`: ترجع نوع المحتوى (image/png أو image/jpeg).
- **ليش:** حماية من Path Traversal (مثلاً `../../appsettings.json`)، حتى محد يكدر يقرا ملفات السيرفر عن طريق رابط الصورة.
- **الملفات:** `TransportLines/Services/FileUploadService.cs`

### 25. UserHelper
- **شنو سويت:** دالة `GetUserId` تقرا رقم المستخدم الداخل من الـ Cookie.
- **ليش:** حتى الكونترولرات تاخذ رقم المستخدم من الـ Cookie مو من الرابط، فمحد يغير الرقم ويشوف بيانات غيره.
- **الملفات:** `TransportLines/Security/UserHelper.cs`

### 26. AdminService
- **شنو سويت:**
  - **قائمة السائقين:** ترجع السائقين اللي حالتهم Pending، الأقدم أولاً، ويا تفاصيل كل سائق.
  - **الموافقة:**
    - السائق لازم يكون Pending، والإجازة لازم تكون بعدها سارية.
    - ينسجل `VerifiedByUserId` و `VerifiedAt`.
    - ينضاف إشعار للسائق بنوع `DriverApproved`.
  - **الرفض:**
    - السبب إجباري (من 5 إلى 255 حرف)، وينحفظ بـ `RejectionReason`.
    - ينضاف إشعار للسائق بنوع `DriverRejected` بيه السبب.
  - **فحص الصلاحية بالخدمة نفسها:** المستخدم لازم يكون أدمن فعّال، حتى لو الكونترولر محمي.
  - **مسار صور السائق:** يقبل بس هذي الأنواع: license، id، registration، vehicle.
- **ليش:** الخطوة 3 بالـ roadmap (موافقة الأدمن على السائقين) وقاعدة 7 (الإشعارات).
- **ملاحظة:** ما غيرت شي بالداتابيس. عمود `Notifications.Type` نص حر، فاستخدمت `DriverApproved` و `DriverRejected`.
- **الملفات:** `TransportLines/Services/AdminService.cs`

### 27. DriverService
- **شنو سويت:**
  - `GetDriverProfileAsync`: ملف السائق ويا سيارته.
  - `IsApprovedDriverAsync`: راح نحتاجها حتى السائق ما ينشر خط إلا إذا Approved.
  - `GetStatusMessage`: رسالة تشرح حالة الحساب بالعربي.
- **الملفات:** `TransportLines/Services/DriverService.cs`

### 28. RejectDriverViewModel
- **شنو سويت:** فورم الرفض (رقم السائق والسبب)، ويا فالديشن.
- **الملفات:** `TransportLines/ViewModels/RejectDriverViewModel.cs`

### 29. AdminController و DriverController
- **شنو سويت:**
  - `AdminController [Authorize(Roles = "Admin")]`: بيه PendingDrivers، وDriverDetails، وApproveDriver (POST)، وRejectDriver (POST)، وDriverPhoto.
    - DriverPhoto يعرض الصورة للأدمن بس، ويا `Cache-Control: no-store` حتى المتصفح ما يحفظها.
  - `DriverController [Authorize(Roles = "Driver")]`: بيه Profile، اللي ياخذ رقم السائق من الـ Cookie مو من الرابط.
- **الملفات:** `TransportLines/Controllers/AdminController.cs`، `TransportLines/Controllers/DriverController.cs`، و `Program.cs` (انسجلت AdminService و DriverService)

### 30. الاختبارات
- **شنو سويت:** 26 اختبار جديد:
  - `AdminServiceTests`:
    - الموافقة والرفض الصحيح، مع الإشعار.
    - رفض الموافقة: إذا الموافقة مكررة، أو السائق مو موجود، أو اللي يوافق مو أدمن، أو الأدمن موقوف، أو الإجازة منتهية.
    - رفض الرفض: إذا السبب فارغ أو قصير أو طويل، أو السائق موافق عليه.
    - أنواع الصور: المسموحة بس ترجع مسار.
  - `DriverServiceTests`: الملف الشخصي، وراكب يرجع null، وحالة الموافقة، ورسائل الحالة.
  - `FileUploadServiceTests`: المسار الآمن، ومحاولات Path Traversal كلها ترجع null.
- **الملفات:** `TransportLines.Tests/AdminServiceTests.cs`، `DriverServiceTests.cs`، `FileUploadServiceTests.cs`، `TestHelper.cs` (انضافت `AddAdmin`)
- **النتيجة:** `dotnet test` نجح، 87 من 87، بدون تحذيرات.

---

## 2026-11-21 — المرحلة 1 / الخطوة 5: الخطوط (إنشاء، عرض، إغلاق)

### 31. CreateLineViewModel
- **شنو سويت:** فورم إنشاء الخط، بيه:
  - الاسم، ووقت الانطلاق، والمقاعد، وأقل مدة اشتراك، والملاحظات، و"للنساء فقط".
  - الأيام (أرقام من 0 لـ 6).
  - قائمة المحطات بالترتيب (`LineStopInput`).
  - قائمة الأسعار (`LinePriceInput`).
- **الملفات:** `TransportLines/ViewModels/CreateLineViewModel.cs`

### 32. LineService
- **شنو سويت:**
  - **الإنشاء:**
    - السائق لازم يكون Approved، وعنده سيارة فعّالة فحصها ساري. السيارة تنختار تلقائياً من سيارات السائق، مو من الفورم.
    - المقاعد ما تتجاوز مقاعد السيارة.
    - أقل مدة اشتراك من 1 لـ 12 شهر.
    - يوم واحد على الأقل، وكل يوم بين 0 و 6، وبدون تكرار.
    - من 2 لـ 20 محطة، كلها موجودة وفعّالة وبدون تكرار، وأوقات الوصول بالترتيب.
    - كل سعر يكون بين محطتين من الخط (الصعود قبل النزول)، رقم صحيح أكبر من صفر، وبدون تكرار.
    - لازم يكون أكو سعر للخط الكامل (من أول محطة لآخر محطة).
  - **الحفظ:** الخط ومحطاته وأيامه وأسعاره بنفس الـ SaveChanges (Transaction وحدة).
  - **العرض:** خطوط السائق، وتفاصيل خط، وكلها مقفولة على صاحب الخط بس.
  - **الإغلاق:** حالة الخط تصير Closed. الطلبات المعلقة تنرفض تلقائياً، والاشتراكات الفعّالة تبقى لحد نهاية مدتها، وكل مشترك ياخذ إشعار بنوع `LineClosed`.
- **ليش:** الخطوة 4 بالـ roadmap، وقاعدة 1 (السعر والمحطات والأيام ثابتة، والتغيير يكون بإغلاق الخط وإنشاء خط جديد).
- **قاعدتين جديدات (اقتراح مني):**
  - سعر الخط الكامل إجباري.
  - إغلاق الخط يرفض الطلبات المعلقة.
- **الملفات:** `TransportLines/Services/LineService.cs`

### 33. DriverController
- **شنو سويت:** ضفت هذي الـ Actions:
  - `MyLines`.
  - `LineDetails` (يرجع 404 إذا الخط مو إله).
  - `CreateLine` GET/POST: الـ GET يحوّل للملف الشخصي إذا السائق مو موافق عليه.
  - `CloseLine` (POST).
- **الملفات:** `TransportLines/Controllers/DriverController.cs`، و `Program.cs` (انسجلت LineService)

### 34. الاختبارات
- **شنو سويت:** 26 اختبار جديد بـ `LineServiceTests`:
  - إنشاء صحيح، والصفوف الفارغة تنتجاهل.
  - رفض الإنشاء إذا:
    - السائق Pending، أو الفحص منتهي، أو المقاعد أكثر من السيارة.
    - الأيام غلط.
    - المحطة وحدة، أو مكررة، أو موقوفة، أو مو موجودة، أو أوقات الوصول مو بالترتيب.
    - السعر بالعكس، أو صفر، أو سالب، أو بيه كسور، أو كبير جداً، أو لمحطة مو على الخط، أو مكرر، أو ماكو سعر للخط الكامل.
  - كل سائق يشوف خطوطه بس.
  - الإغلاق يرفض الطلبات المعلقة ويبقي الفعّالة.
  - سائق ما يكدر يغلق خط غيره، ولا يغلق خط مغلق.
- **الملفات:** `TransportLines.Tests/LineServiceTests.cs`، و `TestHelper.cs` (انضافت منطقة و5 محطات، و `AddApprovedDriverAsync`، و `ValidLine`)
- **النتيجة:** `dotnet test` نجح، 113 من 113، بدون تحذيرات.
- **ملاحظة:** الاختبارات على داتابيس وهمية. حفظ الأسعار على MySQL الحقيقية (بسبب الـ FK المركب للمحطات) راح نتأكد منه بالمرحلة 3.

---

## 2026-11-21 — المرحلة 1 / الخطوة 6: الاشتراكات من جهة الراكب (بحث، طلب، إلغاء)

### 35. RequestSubscriptionViewModel
- **شنو سويت:** فورم طلب الاشتراك، بيه: الخط، ومحطة الصعود، ومحطة النزول، وعدد المقاعد، وتاريخ البداية، وعدد الأشهر.
- **الملفات:** `TransportLines/ViewModels/RequestSubscriptionViewModel.cs`

### 36. SubscriptionService
- **شنو سويت:**
  - **البحث:**
    - يرجع الخطوط المفتوحة اللي سائقها موافق عليه وحسابه فعّال.
    - إذا اختار محطتين: يرجع بس الخطوط اللي تمر بيهم بالترتيب وإلها سعر بينهم.
    - الخطوط النسائية تظهر للراكبات بس.
  - **طلب الاشتراك:**
    - المستخدم لازم يكون راكب فعّال، والخط مفتوح.
    - محطة الصعود قبل النزول، وأكو سعر للزوج.
    - المقاعد من 1 لحد مقاعد الخط.
    - البداية من اليوم لحد 60 يوم.
    - الأشهر من `MinSubscriptionMonths` لـ 12.
    - طلب أو اشتراك مفتوح واحد بس على نفس الخط.
    - السعر ينحفظ نسخة وقت الطلب.
    - تاريخ النهاية = البداية + الأشهر - يوم (يطابق CHECK الداتابيس).
    - السائق ياخذ إشعار بنوع `SubscriptionRequest`.
  - **اشتراكاتي:** كل راكب يشوف اشتراكاته بس.
  - **الإلغاء:**
    - الراكب يلغي طلبه المعلق أو اشتراكه الفعّال (اشتراكه هو بس).
    - ينحفظ `CancelledAt` و `CancelReason` (السبب اختياري، أكثر شي 255 حرف).
    - السائق ياخذ إشعار بنوع `SubscriptionCancelled`.
- **ليش:** الخطوة 5 بالـ roadmap، والقواعد 2 و 3 و 5 و 6 بـ CLAUDE.md. الطلب Pending ما يحجز مقاعد.
- **ملاحظة:** أسماء الـ scaffold بجدول Subscriptions مربكة: `Linestop` = محطة النزول، و `LinestopNavigation` = محطة الصعود. كتبتها كتعليق بالكود.
- **الملفات:** `TransportLines/Services/SubscriptionService.cs`

### 37. PassengerController
- **شنو سويت:** كونترولر عليه `[Authorize(Roles = "Passenger")]`، بيه هذي الـ Actions:
  - `SearchLines`.
  - `LineDetails`.
  - `RequestSubscription` GET/POST: قائمة المحطات بيه هي محطات الخط نفسه بس.
  - `MySubscriptions`.
  - `CancelSubscription` (POST).
- **الملفات:** `TransportLines/Controllers/PassengerController.cs`، و `Program.cs` (انسجلت SubscriptionService)

### 38. الاختبارات
- **شنو سويت:** 22 اختبار جديد بـ `SubscriptionServiceTests`:
  - **البحث:** فلترة بالمحطات والترتيب والسعر، والخط النسائي يظهر للراكبات بس، والخط المغلق ما يظهر.
  - **الطلب الصحيح:** ينحفظ مع الإشعار، وحساب تاريخ النهاية صحيح.
  - **رفض الطلب إذا:**
    - اللي يطلب سائق.
    - الخط مغلق أو مو موجود.
    - المحطات غلط.
    - المقاعد غلط.
    - التاريخ غلط.
    - الأشهر أقل من أقل مدة.
    - راكب ذكر على خط نسائي.
    - أكو طلب ثاني مفتوح على نفس الخط (ينسمح بعد الإلغاء).
  - **اشتراكاتي:** كل راكب يشوف اشتراكاته بس.
  - **الإلغاء:** إلغاء صحيح. ورفض إلغاء اشتراك غيره، وإلغاء اشتراك ملغي، وسبب طويل.
- **الملفات:** `TransportLines.Tests/SubscriptionServiceTests.cs`، و `TestHelper.cs` (انضافت `AddPassengerAsync`)
- **النتيجة:** `dotnet test` نجح، 135 من 135، بدون تحذيرات.

---

## 2026-11-22 — المرحلة 1 / الخطوة 7: موافقة السائق على الطلبات وفحص المقاعد لكل مقطع

### 39. RejectRequestViewModel
- **شنو سويت:** فورم رفض طلب الاشتراك، بيه رقم الطلب ورقم الخط والسبب (من 5 إلى 255 حرف).
- **الملفات:** `TransportLines/ViewModels/RejectRequestViewModel.cs`

### 40. SubscriptionApprovalService
- **شنو سويت:**
  - **عرض طلبات الخط:** للسائق صاحب الخط بس.
  - **الموافقة داخل Transaction:**
    1. يقفل صف الخط بـ `SELECT ... FOR UPDATE` (عن طريق JOIN ويا رقم الطلب)، وهذا أول أمر بالـ Transaction.
    2. يتحقق إن الطلب موجود وعلى خط هذا السائق، والسائق موافق عليه، والخط مفتوح، والطلب Pending، ومدته ما خلصت.
    3. يفحص كل مقطع يمر بيه الطلب: مقاعد الاشتراكات الفعّالة اللي تمر بالمقطع وتتداخل تواريخها، زائد مقاعد الطلب، لازم ما تتجاوز مقاعد الخط.
    4. إذا أكو مكان، الحالة تصير Active، وينسجل `ApprovedAt`، والراكب ياخذ إشعار. وبعدها Commit.
  - **الرفض:** السبب إجباري، والحالة تصير Rejected، والراكب ياخذ إشعار.
- **ليش:** قاعدة 3 (فحص المقاعد وقت الموافقة داخل Transaction تقفل صف الخط) وقاعدة 4 (السعة لكل مقطع) بـ CLAUDE.md.
- **الأمان:** الـ SQL الوحيد مكتوب بـ `ExecuteSqlInterpolatedAsync`، يعني الرقم يروح كـ Parameter، فـ SQL Injection ما يشتغل.
- **ليش القفل أول أمر:** MySQL (REPEATABLE READ) ياخذ نسخة ثابتة من البيانات (Snapshot) عند أول قراءة عادية داخل الـ Transaction. فإذا قرينا الطلب قبل القفل، راح نشوف بيانات قديمة حتى بعد ما ناخذ القفل.
- **الملفات:** `TransportLines/Services/SubscriptionApprovalService.cs`

### 41. DriverController
- **شنو سويت:** ضفت هذي الـ Actions:
  - `LineRequests` (يرجع 404 إذا الخط مو إله).
  - `ApproveRequest` (POST).
  - `RejectRequest` (POST).
  - ودالة `SetResultMessage` لرسائل النجاح والخطأ.
- **الملفات:** `TransportLines/Controllers/DriverController.cs`، و `Program.cs` (انسجلت SubscriptionApprovalService)

### 42. الاختبارات
- **شنو سويت:** 14 اختبار جديد بـ `SubscriptionApprovalServiceTests`، على خط بيه 3 مقاعد ومحطات 1←2←3:
  - **الموافقة الصحيحة:** تنجح ويا الإشعار.
  - **رفض الموافقة إذا:** اللي يوافق سائق ثاني، أو الموافقة مكررة، أو الخط مغلق، أو السائق انسحبت موافقته، أو مدة الطلب خلصت.
  - **فحص المقاعد لكل مقطع:**
    - طلب يتجاوز المقاعد بمقطع واحد ينرفض، وطلب على مقطع ثاني فارغ ينقبل.
    - المقاطع اللي ما تتداخل تنقبل، والتواريخ اللي ما تتداخل تنقبل.
    - الطلب المعلق ما يحجز مقاعد، والاشتراك الملغي يحرر مقاعده.
    - اختبار مباشر لدالة `CountSeatsOnSegment`.
  - **الرفض:** رفض صحيح. ورفض الرفض إذا السبب قصير، أو اللي يرفض سائق ثاني.
  - **عرض الطلبات:** لصاحب الخط بس.
- **الملفات:** `TransportLines.Tests/SubscriptionApprovalServiceTests.cs`، و `TestHelper.cs` (الداتابيس الوهمية تتجاهل الـ Transactions، لأنها ما تدعمها)
- **النتيجة:** `dotnet test` نجح، 149 من 149، بدون تحذيرات.
- **ملاحظة:** أمر القفل `FOR UPDATE` ما يشتغل على الداتابيس الوهمية. لازم نجربه على MySQL الحقيقية بالمرحلة 3، بعد موافقتك.

---

## 2026-11-22 — تقرير التنفيذ

### 43. docs/Implementation_Report.md
- **شنو سويت:** كتبت تقرير تنفيذ بالعربي للمرحلة اللي وصلناها، بيه 8 أقسام:
  1. التقنيات والحزم.
  2. الاتصال بالداتابيس والـ Models.
  3. الأمان.
  4. الخدمات والـ Controllers.
  5. الفالديشن.
  6. الاختبارات.
  7. ما لم ينفذ بعد.
  8. جدول الكلاسات ووظائفها.
- **محتوى كل ميزة:** الكلاس والملف وأهم الدوال، وشنو تسوي، وشنو تحتاج، وليش اخترناها، وكيف اختبرناها، وسيناريو توضيحي.
- **ليش:** طلب الطالب، حتى يكون عنده توثيق للتقرير وللمشرف.
- **المصدر:** الكود الفعلي و CHANGES.md فقط. أعداد الاختبارات أخذتها من `dotnet test --list-tests` (149 حالة).
- **الملفات:** `docs/Implementation_Report.md`

### 44. docs/Code_Map.md
- **شنو سويت:** كتبت خريطة للكود بالعربي لطالب مبتدئ، بيها 4 أقسام:
  1. كل كلاس (Services و Controllers و ViewModels والمساعدة): مسؤوليته، ومنو يستدعيه، وشنو يستقبل وشنو يرجع.
  2. رسم نصي لمسار 14 ميزة من المتصفح لحد الجداول، وشنو يصير مع كل طلب قبل ما يوصل للـ Controller.
  3. جدول "إذا أريد أعدل... أروح لـ..." مقسم حسب الميزات.
  4. جدول "شنو ينكسر إذا عدلت كلاس معين"، وعليه علامة ⚠️ للأشياء اللي ما يكشفها لا الـ build ولا الاختبارات.
- **ليش:** طلب الطالب، حتى يعرف وين كل شي ويكدر يشرح الكود للمشرف.
- **المصدر:** الكود الفعلي فقط. تأكدت آلياً إن كل أسماء الدوال المذكورة موجودة بالكود.
- **الملفات:** `docs/Code_Map.md`

---

## 2026-11-22 — المرحلة 3 (جزئي): اختبار على MySQL الحقيقية (قراءة فقط)

### 45. فحص الداتابيس (قراءة فقط)
- **شنو سويت:** شغلت استعلامات SELECT بس، بـ `mysql.exe` وكلمة المرور من user-secrets بدون ما تنطبع. شفت بيها:
  - صلاحيات `transport_app`.
  - إعدادات الـ Transaction.
  - عدد الصفوف بالجداول.
  - البيانات التجريبية.
- **النتائج:**
  - صلاحيات `transport_app` = `SELECT, INSERT, UPDATE, DELETE` على `line_subscription` فقط (مطابق لـ CLAUDE.md).
  - `transaction_isolation = REPEATABLE-READ`، وهذا يأكد قرار وضع القفل أول أمر بالموافقة.
  - أرقام الأدوار بالداتابيس 4 و 5 و 6. الكود يقراها بالاسم، فما تأثر.
  - الـ Triggers ما تظهر لـ `transport_app` لأن ما عنده صلاحية TRIGGER، وهذا ما يأثر على عملها.
- **ما تغير شي بالداتابيس.**

### 46. اختبارات MySQL الحقيقية
- **قرار الطالب:** قراءة فقط، وتشتغل بس عند الطلب.
- **شنو سويت:**
  - `RealDbFactAttribute`: علامة تخلي الاختبار ينتخطى إلا إذا `RUN_REAL_DB_TESTS=1`.
  - `RealDbHelper`:
    - الاتصال بـ MySQL من user-secrets.
    - **صورة (Snapshot) للداتابيس قبل وبعد كل اختبار:** عدد الصفوف بـ 9 جداول، وحالات السائقين والخطوط والاشتراكات، ووقت آخر دخول. أي كتابة تفشّل الاختبار.
    - إيجاد البيانات التجريبية.
  - `RealDatabaseTests`: 14 اختبار. كل عملية فيها حفظ تنطاها مدخل يفشل بالفالديشن قبل سطر الحفظ، حتى كل الاستعلامات تشتغل على MySQL بدون كتابة.
  - `DatabaseConnectionTests`: صار يستخدم `RealDbHelper` و `[RealDbFact]`، حتى هو ما يلمس الداتابيس إلا عند الطلب.
- **شنو تأكدنا منه على MySQL:**
  - استعلامات كل الخدمات تشتغل، ومن ضمنها الـ Includes المعقدة.
  - أسماء الـ scaffold المربكة صحيحة فعلاً: `LinestopNavigation` = الصعود، و `Linestop` = النزول.
  - فحص تكرار الهاتف يشتغل.
  - البحث بالاتجاه الصحيح فقط.
  - حساب المقاعد لكل مقطع على البيانات الحقيقية يطابق الحساب اليدوي (المقطع 1 = 3، والمقطع 2 = 2).
  - أمر `SELECT ... FOR UPDATE` يشتغل بدون أخطاء.
  - **القفل يشتغل فعلاً:** لما اتصال يقفل صف الخط، الموافقة من اتصال ثاني تنتظر لحد ما ينفك القفل.
- **طريقة التشغيل:** `$env:RUN_REAL_DB_TESTS = "1"; dotnet test --filter "FullyQualifiedName~RealDatabaseTests|FullyQualifiedName~DatabaseConnectionTests"`
- **النتيجة:**
  - على MySQL: 16 من 16 نجحت، والداتابيس ما تغيرت.
  - الـ `dotnet test` العادي: 147 نجحت، و16 انتخطت، بدون تحذيرات.
- **ما انختبر (بسبب قرار القراءة فقط):**
  - الحفظ الفعلي: تسجيل سائق، إنشاء خط (الـ FK المركب للأسعار والـ Triggers)، طلب اشتراك، الموافقة الكاملة، الإغلاق.
  - تحقق bcrypt لكلمة مرور صحيحة، لأن ما نعرف كلمات مرور البيانات التجريبية.
- **الملفات:**
  - جديدة: `TransportLines.Tests/RealDbFactAttribute.cs`، `TransportLines.Tests/RealDbHelper.cs`، `TransportLines.Tests/RealDatabaseTests.cs`
  - معدلة: `TransportLines.Tests/DatabaseConnectionTests.cs`
