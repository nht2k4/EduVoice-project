using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.WebRazor.Infrastructure;

namespace AIVES.WebRazor.Pages.Exams;

public class IndexModel : AppPageModel
{
    private readonly IExamService _examService;

    public IndexModel(IExamService examService) => _examService = examService;

    public List<ExamListItemDto> Exams { get; private set; } = new();

    public async Task OnGetAsync() => Exams = await _examService.GetListAsync(CurrentUserId);
}
