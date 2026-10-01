using System.ComponentModel.DataAnnotations;
using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.WebRazor.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AIVES.WebRazor.Pages.Subjects;

public class DetailsModel : AppPageModel
{
    private readonly ISubjectService _subjectService;
    private readonly IAssignmentService _assignmentService;

    public DetailsModel(ISubjectService subjectService, IAssignmentService assignmentService)
    {
        _subjectService = subjectService;
        _assignmentService = assignmentService;
    }

    public SubjectDetailDto Detail { get; private set; } = null!;

    public List<SelectListItem> LecturerOptions { get; private set; } = new();

    [BindProperty]
    public AssignInput Assign { get; set; } = new();

    public class AssignInput
    {
        public int SubjectId { get; set; }

        [Required(ErrorMessage = "Chọn giảng viên cần phân công.")]
        [Display(Name = "Giảng viên")]
        public int? LecturerId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var result = await _subjectService.GetDetailAsync(id);
        if (!result.Succeeded) return MapFailure(result) ?? NotFound();

        Detail = result.Data!;
        LecturerOptions = Detail.AvailableLecturers
            .Select(l => new SelectListItem($"{l.FullName} ({l.Email})", l.Id.ToString()))
            .ToList();
        Assign.SubjectId = id;
        return Page();
    }

    // MAIN FLOW: PageModel chỉ nhận request -> gọi Service -> trả kết quả. Không có luật nghiệp vụ ở đây.
    public async Task<IActionResult> OnPostAssignAsync()
    {
        if (!ModelState.IsValid || Assign.LecturerId is null)
        {
            FlashError("Chọn giảng viên cần phân công.");
            return RedirectToPage(new { id = Assign.SubjectId });
        }

        var result = await _assignmentService.AssignAsync(Assign.LecturerId.Value, Assign.SubjectId);

        if (result.Succeeded) FlashSuccess("Đã phân công giảng viên.");
        else FlashError(result.Error!);

        return RedirectToPage(new { id = Assign.SubjectId });
    }

    public async Task<IActionResult> OnPostUnassignAsync(int subjectId, int lecturerId)
    {
        var result = await _assignmentService.UnassignAsync(lecturerId, subjectId);

        if (result.Succeeded) FlashSuccess("Đã gỡ phân công.");
        else FlashError(result.Error!);

        return RedirectToPage(new { id = subjectId });
    }
}
