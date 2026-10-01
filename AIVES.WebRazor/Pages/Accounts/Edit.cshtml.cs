using System.ComponentModel.DataAnnotations;
using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.Business.Models;
using AIVES.WebRazor.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.WebRazor.Pages.Accounts;

public class EditModel : AppPageModel
{
    private readonly IAccountService _accountService;

    public EditModel(IAccountService accountService) => _accountService = accountService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nhập họ tên.")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nhập email.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(150)]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Vai trò")]
        public AppRole Role { get; set; }

        public bool IsActive { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var result = await _accountService.GetByIdAsync(id);
        if (!result.Succeeded) return MapFailure(result) ?? NotFound();

        var a = result.Data!;
        Input = new InputModel { Id = a.Id, FullName = a.FullName, Email = a.Email, Role = a.Role, IsActive = a.IsActive };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var result = await _accountService.UpdateAsync(
            CurrentUserId, new UpdateAccountRequest(Input.Id, Input.FullName, Input.Email, Input.Role));

        if (!result.Succeeded)
        {
            var mapped = MapFailure(result);
            if (mapped is not null) return mapped;

            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }

        FlashSuccess("Đã lưu thay đổi tài khoản.");
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(int id, string newPassword)
    {
        var result = await _accountService.ResetPasswordAsync(id, newPassword);

        if (result.Succeeded) FlashSuccess("Đã đặt lại mật khẩu.");
        else FlashError(result.Error!);

        return RedirectToPage(new { id });
    }
}
