using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.WebRazor.Infrastructure;

namespace AIVES.WebRazor.Pages.Subjects;

public class IndexModel : AppPageModel
{
    private readonly ISubjectService _subjectService;

    public IndexModel(ISubjectService subjectService) => _subjectService = subjectService;

    public List<SubjectDto> Subjects { get; private set; } = new();

    public async Task OnGetAsync() => Subjects = await _subjectService.GetAllAsync();
}
