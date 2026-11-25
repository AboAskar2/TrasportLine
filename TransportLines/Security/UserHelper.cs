using System.Security.Claims;

namespace TransportLines.Security;

// أداة صغيرة تقرا رقم المستخدم الداخل من الـ Cookie
// نستخدمها بدل ما ناخذ رقم المستخدم من الرابط، حتى محد يغير الرقم ويشوف بيانات غيره
public static class UserHelper
{
    // يرجع رقم المستخدم الداخل، أو 0 إذا ما موجود
    public static int GetUserId(ClaimsPrincipal user)
    {
        string? idText = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idText, out int userId))
            return userId;

        return 0;
    }
}
