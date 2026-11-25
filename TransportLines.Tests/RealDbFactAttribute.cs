namespace TransportLines.Tests;

// علامة لاختبارات الداتابيس الحقيقية: تنتخطى (Skip) إلا إذا متغير البيئة RUN_REAL_DB_TESTS = 1
// هيج dotnet test العادي ما يلمس MySQL أبداً بالغلط
// طريقة التشغيل (PowerShell):  $env:RUN_REAL_DB_TESTS = "1"; dotnet test
public class RealDbFactAttribute : FactAttribute
{
    // إذا المتغير مو 1، نخلي سبب التخطي حتى xUnit يتخطى الاختبار
    public RealDbFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_REAL_DB_TESTS") != "1")
        {
            Skip = "اختبار على MySQL الحقيقية: شغّله بـ RUN_REAL_DB_TESTS=1";
        }
    }
}
