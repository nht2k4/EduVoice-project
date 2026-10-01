using AIVES.Business.Common;
using AIVES.Business.DTOs;

namespace AIVES.Business.Interfaces;

public interface IInterviewService
{
    // Bắt đầu hoặc tiếp tục phỏng vấn: gọi lại nhiều lần vẫn trả về đúng lượt hỏi đang mở
    Task<ServiceResult<InterviewStateDto>> StartAsync(int currentUserId, int participantId);

    // Nộp câu trả lời (đã là văn bản sau STT). Trả về lượt hỏi kế tiếp: câu hỏi xoáy, câu chính tiếp theo, hoặc kết thúc.
    Task<ServiceResult<InterviewStateDto>> SubmitAnswerAsync(int currentUserId, int turnId, string? answer, bool timedOut);

    Task<ServiceResult<TranscriptDto>> GetTranscriptAsync(int currentUserId, int participantId);
}
