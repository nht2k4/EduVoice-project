using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.Business.Models;
using AIVES.WebRazor.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.WebRazor.Pages.Accounts;

public class IndexModel : AppPageModel
{
    private readonly IAccountService _accountService;

    public IndexModel(IAccountService accountService) => _accountService = accountService;

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public AppRole? Role { get; set; }

    public List<AccountDto> Accounts { get; private set; } = new();

    public async Task OnGetAsync() => Accounts = await _accountService.SearchAsync(Keyword, Role);

    public async Task<IActionResult> OnPostSetActiveAsync(int id, bool isActive)
    {
        var result = await _accountService.SetActiveAsync(CurrentUserId, id, isActive);

        if (result.Succeeded) FlashSuccess(isActive ? "Đã mở khóa tài khoản." : "Đã khóa tài khoản.");
        else FlashError(result.Error!);

        return RedirectToPage();
    }
}
