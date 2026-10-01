using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.WebRazor.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.WebRazor.Pages.Exams;

public class DetailsModel : AppPageModel
{
    private readonly IExamService _examService;

    public DetailsModel(IExamService examService) => _examService = examService;

    public ExamDetailDto Exam { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var result = await _examService.GetDetailAsync(CurrentUserId, id);
        if (!result.Succeeded) return MapFailure(result) ?? NotFound();

        Exam = result.Data!;
        return Page();
    }

    public async Task<IActionResult> OnPostSetOpenAsync(int id, bool open)
    {
        var result = await _examService.SetOpenAsync(CurrentUserId, id, open);
        if (!result.Succeeded) return MapFailure(result) ?? NotFound();

        FlashSuccess(open ? "Đã mở lại phiên thi." : "Đã đóng phiên thi.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReallocateAsync(int id)
    {
        var result = await _examService.ReallocateAsync(CurrentUserId, id);

        if (result.Succeeded) FlashSuccess("Đã chọn lại bộ câu hỏi cho toàn bộ thí sinh.");
        else
        {
            var mapped = MapFailure(result);
            if (mapped is not null) return mapped;
            FlashError(result.Error!);
        }

        return RedirectToPage(new { id });
    }
}
