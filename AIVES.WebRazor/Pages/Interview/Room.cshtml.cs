using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.WebRazor.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AIVES.WebRazor.Pages.Interview;

// Phòng phỏng vấn: trang chỉ là khung. Trình duyệt đọc câu hỏi (TTS), nghe câu trả lời (STT)
// rồi gọi hai handler JSON bên dưới; mọi luật (giới hạn lượt, thời gian, AI hỏi xoáy) nằm ở InterviewService.
public class RoomModel : AppPageModel
{
    private readonly IInterviewService _interviewService;

    public RoomModel(IInterviewService interviewService) => _interviewService = interviewService;

    public TranscriptDto Info { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int participantId)
    {
        var result = await _interviewService.GetTranscriptAsync(CurrentUserId, participantId);
        if (!result.Succeeded) return MapFailure(result) ?? NotFound();

        Info = result.Data!;
        return Page();
    }

    public async Task<IActionResult> OnPostStartAsync(int participantId) =>
        ToJson(await _interviewService.StartAsync(CurrentUserId, participantId));

    public async Task<IActionResult> OnPostAnswerAsync(int turnId, string? answer, bool timedOut) =>
        ToJson(await _interviewService.SubmitAnswerAsync(CurrentUserId, turnId, answer, timedOut));

    private IActionResult ToJson(Business.Common.ServiceResult<InterviewStateDto> result)
    {
        if (result.Succeeded) return new JsonResult(result.Data);

        var status = result.ErrorType switch
        {
            Business.Common.ServiceErrorType.NotFound => 404,
            Business.Common.ServiceErrorType.Forbidden => 403,
            _ => 400
        };
        return new JsonResult(new { error = result.Error }) { StatusCode = status };
    }
}
