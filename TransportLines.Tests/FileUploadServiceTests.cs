using Microsoft.AspNetCore.Http;
using TransportLines.Services;

namespace TransportLines.Tests;

// اختبارات خدمة رفع الصور: تقبل الصور الحقيقية وترفض أي ملف ثاني
public class FileUploadServiceTests
{
    private readonly string _folder;
    private readonly FileUploadService _service;

    // كل اختبار ياخذ مجلد مؤقت جديد
    public FileUploadServiceTests()
    {
        _folder = TestHelper.CreateTempFolder();
        _service = new FileUploadService(_folder);
    }

    // صورة JPG حقيقية لازم تنقبل
    [Fact]
    public void ValidateImage_RealJpg_ReturnsNoError()
    {
        string? error = _service.ValidateImage(TestHelper.CreateJpg());
        Assert.Null(error);
    }

    // صورة PNG حقيقية لازم تنقبل
    [Fact]
    public void ValidateImage_RealPng_ReturnsNoError()
    {
        string? error = _service.ValidateImage(TestHelper.CreatePng());
        Assert.Null(error);
    }

    // إذا ماكو ملف لازم يطلع خطأ
    [Fact]
    public void ValidateImage_NoFile_ReturnsError()
    {
        string? error = _service.ValidateImage(null);
        Assert.NotNull(error);
    }

    // ملف أكبر من 2 ميگا لازم ينرفض
    [Fact]
    public void ValidateImage_TooBig_ReturnsError()
    {
        byte[] bigFile = new byte[FileUploadService.MaxFileSize + 1];
        bigFile[0] = 0xFF;
        bigFile[1] = 0xD8;
        bigFile[2] = 0xFF;

        string? error = _service.ValidateImage(TestHelper.CreateFile(bigFile, "big.jpg"));
        Assert.NotNull(error);
    }

    // ملف بامتداد خطير (exe، php، html) لازم ينرفض حتى لو بايتاته تشبه صورة
    [Theory]
    [InlineData("virus.exe")]
    [InlineData("shell.php")]
    [InlineData("page.html")]
    [InlineData("photo.jpg.exe")]
    public void ValidateImage_DangerousExtension_ReturnsError(string fileName)
    {
        IFormFile file = TestHelper.CreateFile(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, fileName);
        string? error = _service.ValidateImage(file);
        Assert.NotNull(error);
    }

    // ملف نصي اسمه .jpg (محاولة خداع) لازم ينرفض لأن بايتاته مو بايتات صورة
    [Fact]
    public void ValidateImage_TextFileNamedJpg_ReturnsError()
    {
        byte[] text = System.Text.Encoding.UTF8.GetBytes("this is plain text, not an image");
        string? error = _service.ValidateImage(TestHelper.CreateFile(text, "photo.jpg"));
        Assert.NotNull(error);
    }

    // الحفظ لازم يغير اسم الملف لاسم عشوائي، والحذف لازم يشيل الملف
    [Fact]
    public async Task SaveImage_UsesRandomName_AndDeleteRemovesIt()
    {
        string path = await _service.SaveImageAsync(TestHelper.CreateJpg("my-license.jpg"), "licenses");
        string fullPath = Path.Combine(_folder, path);

        Assert.True(File.Exists(fullPath));
        Assert.DoesNotContain("my-license", path);
        Assert.EndsWith(".jpg", path);

        _service.DeleteImage(path);
        Assert.False(File.Exists(fullPath));
    }

    // مسار صورة محفوظة يرجع المسار الكامل، ونوع المحتوى صحيح
    [Fact]
    public async Task GetSafeFullPath_SavedImage_ReturnsPath()
    {
        string path = await _service.SaveImageAsync(TestHelper.CreatePng(), "ids");

        string? fullPath = _service.GetSafeFullPath(path);

        Assert.NotNull(fullPath);
        Assert.Equal("image/png", FileUploadService.GetContentType(fullPath));
    }

    // محاولات الطلوع برا مجلد الرفع (Path Traversal) أو ملف مو موجود ترجع null
    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData(@"..\..\Program.cs")]
    [InlineData(@"C:\Windows\win.ini")]
    [InlineData("licenses/not-there.jpg")]
    [InlineData("")]
    [InlineData(null)]
    public void GetSafeFullPath_OutsideOrMissing_ReturnsNull(string? path)
    {
        Assert.Null(_service.GetSafeFullPath(path));
    }
}
