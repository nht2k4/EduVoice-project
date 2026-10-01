using System.ComponentModel.DataAnnotations;
using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.Business.Models;
using AIVES.WebRazor.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.WebRazor.Pages.Language;

// Cả Admin và Giảng viên đều vào được. Ai được sửa môn nào thì tầng Business quyết định.
public class EditModel : AppPageModel
{
    private readonly ILanguageConfigService _languageService;

    public EditModel(ILanguageConfigService languageService) => _languageService = languageService;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public int SubjectId { get; set; }
        public string SubjectCode { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;

        [Display(Name = "Ngôn ngữ nhận dạng giọng nói (STT)")]
        public AppLanguage SttLanguage { get; set; }

        [Display(Name = "Ngôn ngữ đọc văn bản (TTS)")]
        public AppLanguage TtsLanguage { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int subjectId)
    {
        var result = await _languageService.GetForEditAsync(CurrentUserId, subjectId);
        if (!result.Succeeded) return MapFailure(result) ?? BadRequest();

        var c = result.Data!;
        Input = new InputModel
        {
            SubjectId = c.SubjectId, SubjectCode = c.SubjectCode, SubjectName = c.SubjectName,
            SttLanguage = c.SttLanguage, TtsLanguage = c.TtsLanguage, UpdatedAt = c.UpdatedAt, UpdatedBy = c.UpdatedBy
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var result = await _languageService.UpdateAsync(
            CurrentUserId, new UpdateLanguageRequest(Input.SubjectId, Input.SttLanguage, Input.TtsLanguage));

        if (!result.Succeeded)
        {
            var mapped = MapFailure(result);
            if (mapped is not null) return mapped;

            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }

        FlashSuccess($"Đã lưu cấu hình ngôn ngữ cho {Input.SubjectCode}.");
        return User.IsInRole(Roles.Admin)
            ? RedirectToPage("/Subjects/Details", new { id = Input.SubjectId })
            : RedirectToPage("/MySubjects/Index");
    }
}
