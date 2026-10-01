using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.WebRazor.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.WebRazor.Pages.Exams;

public class TranscriptModel : AppPageModel
{
    private readonly IInterviewService _interviewService;

    public TranscriptModel(IInterviewService interviewService) => _interviewService = interviewService;

    public TranscriptDto Transcript { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int participantId)
    {
        var result = await _interviewService.GetTranscriptAsync(CurrentUserId, participantId);
        if (!result.Succeeded) return MapFailure(result) ?? NotFound();

        Transcript = result.Data!;
        return Page();
    }
}
