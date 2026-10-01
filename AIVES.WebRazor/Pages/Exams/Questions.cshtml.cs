using System.ComponentModel.DataAnnotations;
using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.WebRazor.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.WebRazor.Pages.Exams;

// Ngân hàng câu hỏi theo môn: nguồn để phiên thi chọn câu cho từng sinh viên
public class QuestionsModel : AppPageModel
{
    private readonly IQuestionService _questionService;

    public QuestionsModel(IQuestionService questionService) => _questionService = questionService;

    [BindProperty(SupportsGet = true)]
    public int SubjectId { get; set; }

    public List<QuestionDto> Questions { get; private set; } = new();

    [BindProperty]
    public NewQuestionInput NewQuestion { get; set; } = new();

    public class NewQuestionInput
    {
        [Required(ErrorMessage = "Nhập nội dung câu hỏi.")]
        [StringLength(1000, ErrorMessage = "Tối đa 1000 ký tự.")]
        [Display(Name = "Nội dung câu hỏi")]
        public string Content { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Tối đa 1000 ký tự.")]
        [Display(Name = "Các ý chính cần có (ngăn cách bằng dấu ;)")]
        public string? ExpectedPoints { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var result = await _questionService.GetBySubjectAsync(CurrentUserId, SubjectId);
        if (!result.Succeeded) return MapFailure(result) ?? NotFound();

        Questions = result.Data!;
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        if (!ModelState.IsValid)
        {
            FlashError("Nhập nội dung câu hỏi (tối đa 1000 ký tự).");
            return RedirectToPage(new { subjectId = SubjectId });
        }

        var result = await _questionService.AddAsync(CurrentUserId,
            new AddQuestionRequest(SubjectId, NewQuestion.Content, NewQuestion.ExpectedPoints));

        if (result.Succeeded) FlashSuccess("Đã thêm câu hỏi.");
        else
        {
            var mapped = MapFailure(result);
            if (mapped is not null) return mapped;
            FlashError(result.Error!);
        }

        return RedirectToPage(new { subjectId = SubjectId });
    }

    public async Task<IActionResult> OnPostSetActiveAsync(int questionId, bool isActive)
    {
        var result = await _questionService.SetActiveAsync(CurrentUserId, questionId, isActive);
        if (!result.Succeeded) return MapFailure(result) ?? NotFound();

        FlashSuccess(isActive ? "Đã bật câu hỏi." : "Đã tắt câu hỏi, các phiên thi mới sẽ không chọn câu này.");
        return RedirectToPage(new { subjectId = SubjectId });
    }
}
