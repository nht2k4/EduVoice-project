using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.WebRazor.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AIVES.WebRazor.Pages.Exams;

public class CreateModel : AppPageModel
{
    private readonly IExamService _examService;

    public CreateModel(IExamService examService) => _examService = examService;

    public List<SelectListItem> SubjectOptions { get; private set; } = new();

    public string MinStartTime => DateTime.Now.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    public string MaxStartTime => DateTime.Now.AddYears(1).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel : IValidatableObject
    {
        [Required(ErrorMessage = "Chọn môn học.")]
        [Display(Name = "Môn học")]
        public int? SubjectId { get; set; }

        [Required(ErrorMessage = "Nhập tên phiên thi.")]
        [StringLength(150, ErrorMessage = "Tối đa 150 ký tự.")]
        [Display(Name = "Tên phiên thi")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Chọn thời điểm bắt đầu.")]
        [Display(Name = "Bắt đầu lúc")]
        public DateTime? StartTime { get; set; }

        [Range(1, 120, ErrorMessage = "Từ 1 đến 120 phút.")]
        [Display(Name = "Khung thời gian mỗi thí sinh (phút)")]
        public int SlotMinutes { get; set; } = 10;

        [Range(1, 20, ErrorMessage = "Từ 1 đến 20 câu.")]
        [Display(Name = "Số câu hỏi chính mỗi thí sinh")]
        public int MainQuestionCount { get; set; } = 3;

        [Range(0, 40, ErrorMessage = "Từ 0 đến 40 câu.")]
        [Display(Name = "Số câu đào sâu tối đa mỗi thí sinh")]
        public int MaxFollowUps { get; set; } = 4;

        [Range(0, 5, ErrorMessage = "Từ 0 đến 5 lượt.")]
        [Display(Name = "Số lượt hỏi xoáy tối đa mỗi câu")]
        public int MaxFollowUpsPerQuestion { get; set; } = 2;

        [Range(10, 600, ErrorMessage = "Từ 10 đến 600 giây.")]
        [Display(Name = "Thời gian trả lời mỗi câu (giây)")]
        public int AnswerSeconds { get; set; } = 60;

        [Required(ErrorMessage = "Nhập danh sách sinh viên.")]
        [Display(Name = "Danh sách sinh viên (mỗi dòng: MSSV; Họ tên)")]
        public string StudentList { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartTime.HasValue)
            {
                if (StartTime.Value < DateTime.Now.AddMinutes(-5))
                {
                    yield return new ValidationResult(
                        "Thời điểm bắt đầu không được ở trong quá khứ.",
                        new[] { nameof(StartTime) });
                }
                else if (StartTime.Value > DateTime.Now.AddYears(1))
                {
                    yield return new ValidationResult(
                        "Thời điểm bắt đầu không được vượt quá 1 năm tới.",
                        new[] { nameof(StartTime) });
                }
            }
        }
    }

    public async Task OnGetAsync()
    {
        Input.StartTime = DateTime.Today.AddDays(1).AddHours(8);
        await LoadSubjectsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadSubjectsAsync();
        if (!ModelState.IsValid) return Page();

        var result = await _examService.CreateAsync(CurrentUserId, new CreateExamRequest(
            Input.SubjectId!.Value, Input.Title, Input.StartTime!.Value, Input.SlotMinutes,
            Input.MainQuestionCount, Input.MaxFollowUps, Input.MaxFollowUpsPerQuestion, Input.AnswerSeconds,
            Input.StudentList));

        if (!result.Succeeded)
        {
            var mapped = MapFailure(result);
            if (mapped is not null) return mapped;

            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }

        FlashSuccess("Đã tạo phiên thi và chọn bộ câu hỏi cho từng sinh viên.");
        return RedirectToPage("Details", new { id = result.Data });
    }

    private async Task LoadSubjectsAsync() =>
        SubjectOptions = (await _examService.GetSubjectsForExamAsync(CurrentUserId))
            .Select(s => new SelectListItem($"{s.Code}: {s.Name}", s.Id.ToString()))
            .ToList();
}
