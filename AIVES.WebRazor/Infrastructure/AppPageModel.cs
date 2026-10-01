using AIVES.Business.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AIVES.WebRazor.Infrastructure;

// Lớp cha của mọi PageModel: lấy người dùng hiện tại, thông báo flash, và đổi kết quả của Business thành mã HTTP
public abstract class AppPageModel : PageModel
{
    protected int CurrentUserId => User.RequireUserId();

    protected void FlashSuccess(string message) => TempData["Success"] = message;
    protected void FlashError(string message) => TempData["Error"] = message;

    // NotFound/Forbidden -> mã HTTP tương ứng. Lỗi Validation -> null để trang tự hiển thị lên form.
    protected IActionResult? MapFailure(ServiceResult result) => result.ErrorType switch
    {
        ServiceErrorType.NotFound => NotFound(),
        ServiceErrorType.Forbidden => Forbid(),
        _ => null
    };
}
