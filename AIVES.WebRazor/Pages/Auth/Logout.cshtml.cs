using AIVES.WebRazor.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.WebRazor.Pages.Auth;

public class LogoutModel : AppPageModel
{
    public IActionResult OnGet() => RedirectToPage("/Auth/Login");

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Auth/Login");
    }
}
