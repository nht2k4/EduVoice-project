using AIVES.Business.Models;
using AIVES.WebRazor.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.WebRazor.Pages;

public class IndexModel : AppPageModel
{
    public IActionResult OnGet() =>
        User.IsInRole(Roles.Admin) ? RedirectToPage("/Subjects/Index") : RedirectToPage("/MySubjects/Index");
}
