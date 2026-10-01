using AIVES.Business.Common;
using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.Business.Models;
using AIVES.DataAccess.Entities;
using AIVES.DataAccess.Repositories.Interfaces;

namespace AIVES.Business.Services;

public class InterviewService : IInterviewService
{
    // Thời gian dành cho việc AI đọc câu hỏi (TTS): đồng hồ phía trình duyệt chỉ chạy sau khi đọc xong
    private const int ReadingGraceSeconds = 15;
    private const int MaxAnswerLength = 4000;

    private readonly IInterviewRepository _interviews;
    private readonly IAssignmentService _assignmentService;
    private readonly ILanguageConfigService _language;
    private readonly IFollowUpGenerator _followUps;

    public InterviewService(IInterviewRepository interviews, IAssignmentService assignmentService,
        ILanguageConfigService language, IFollowUpGenerator followUps)
    {
        _interviews = interviews;
        _assignmentService = assignmentService;
        _language = language;
        _followUps = followUps;
    }

    public async Task<ServiceResult<InterviewStateDto>> StartAsync(int currentUserId, int participantId)
    {
        var p = await _interviews.GetParticipantAsync(participantId);
        if (p is null)
            return ServiceResult<InterviewStateDto>.NotFound("Không tìm thấy thí sinh.");
        if (!await _assignmentService.CanAccessSubjectAsync(currentUserId, p.Session.SubjectId))
            return ServiceResult<InterviewStateDto>.Forbidden("Bạn chưa được phân công môn học này.");

        if (p.Status != ParticipantStatuses.Completed)
        {
            if (p.Session.Status != ExamStatuses.Open)
                return ServiceResult<InterviewStateDto>.Fail("Phiên thi đã đóng.");

            if (!await _interviews.HasTurnsAsync(p.Id))
            {
                var first = p.ParticipantQuestions.OrderBy(pq => pq.OrderNo).FirstOrDefault();
                if (first is null)
                    return ServiceResult<InterviewStateDto>.Fail("Thí sinh chưa được phân bổ câu hỏi.");

                p.Status = ParticipantStatuses.InProgress;
                _interviews.AddTurn(NewTurn(p.Id, first.Question, TurnKinds.Main, first.Question.Content, null));
                await _interviews.SaveAsync();
            }
        }

        return ServiceResult<InterviewStateDto>.Ok(await BuildStateAsync(p));
    }

    public async Task<ServiceResult<InterviewStateDto>> SubmitAnswerAsync(int currentUserId, int turnId, string? answer, bool timedOut)
    {
        var turn = await _interviews.GetTurnAsync(turnId);
        if (turn is null)
            return ServiceResult<InterviewStateDto>.NotFound("Không tìm thấy lượt hỏi.");

        var p = await _interviews.GetParticipantAsync(turn.ParticipantId);
        if (p is null)
            return ServiceResult<InterviewStateDto>.NotFound("Không tìm thấy thí sinh.");
        if (!await _assignmentService.CanAccessSubjectAsync(currentUserId, p.Session.SubjectId))
            return ServiceResult<InterviewStateDto>.Forbidden("Bạn chưa được phân công môn học này.");
        if (p.Session.Status != ExamStatuses.Open)
            return ServiceResult<InterviewStateDto>.Fail("Phiên thi đã đóng.");
        if (turn.AnsweredAt is not null)
            return ServiceResult<InterviewStateDto>.Fail("Câu hỏi này đã được trả lời.");

        var session = p.Session;
        var now = DateTime.UtcNow;
        var text = (answer ?? string.Empty).Trim();
        if (text.Length > MaxAnswerLength) text = text[..MaxAnswerLength];

        // Giới hạn thời gian được kiểm ở server, không chỉ dựa vào đồng hồ trình duyệt
        turn.Answer = text;
        turn.AnsweredAt = now;
        turn.TimedOut = timedOut || (now - turn.AskedAt).TotalSeconds > session.AnswerSeconds + ReadingGraceSeconds;

        var turns = await _interviews.GetTurnsAsync(p.Id);
        var sameQuestion = turns.Where(t => t.QuestionId == turn.QuestionId).ToList();
        var followUpsOnQuestion = sameQuestion.Count(t => t.Kind == TurnKinds.FollowUp);
        var followUpsTotal = turns.Count(t => t.Kind == TurnKinds.FollowUp);
        var added = false;

        if (text.Length > 0
            && followUpsOnQuestion < session.MaxFollowUpsPerQuestion
            && followUpsTotal < session.MaxFollowUps)
        {
            var locale = (await _language.GetSpeechConfigAsync(session.SubjectId)).Data?.TtsLocale ?? "vi-VN";
            var exchanges = sameQuestion
                .Where(t => t.AnsweredAt is not null)
                .Select(t => (t.Content, t.Answer ?? string.Empty))
                .ToList();

            var decision = await _followUps.DecideAsync(
                new FollowUpContext(turn.Question.Content, turn.Question.ExpectedPoints, locale, exchanges));

            if (decision.Ask && !string.IsNullOrWhiteSpace(decision.Question))
            {
                _interviews.AddTurn(NewTurn(p.Id, turn.Question, TurnKinds.FollowUp, Truncate(decision.Question, 1000), decision.Reason));
                added = true;
            }
        }

        if (!added)
        {
            var asked = turns.Where(t => t.Kind == TurnKinds.Main).Select(t => t.QuestionId).ToHashSet();
            var next = p.ParticipantQuestions.OrderBy(pq => pq.OrderNo).FirstOrDefault(pq => !asked.Contains(pq.QuestionId));

            if (next is not null)
                _interviews.AddTurn(NewTurn(p.Id, next.Question, TurnKinds.Main, next.Question.Content, null));
            else
                p.Status = ParticipantStatuses.Completed;
        }

        await _interviews.SaveAsync();
        return ServiceResult<InterviewStateDto>.Ok(await BuildStateAsync(p));
    }

    public async Task<ServiceResult<TranscriptDto>> GetTranscriptAsync(int currentUserId, int participantId)
    {
        var p = await _interviews.GetParticipantAsync(participantId);
        if (p is null)
            return ServiceResult<TranscriptDto>.NotFound("Không tìm thấy thí sinh.");
        if (!await _assignmentService.CanAccessSubjectAsync(currentUserId, p.Session.SubjectId))
            return ServiceResult<TranscriptDto>.Forbidden("Bạn chưa được phân công môn học này.");

        var turns = await _interviews.GetTurnsAsync(p.Id);
        return ServiceResult<TranscriptDto>.Ok(new TranscriptDto(
            p.Id, p.SessionId, p.Session.Title, p.Session.Subject.Code, p.StudentCode, p.FullName, p.Status,
            turns.Select(t => new TurnDto(t.Id, t.Kind, t.Content, t.Reason, t.Answer, t.TimedOut, t.AskedAt)).ToList()));
    }

    private async Task<InterviewStateDto> BuildStateAsync(ExamParticipant p)
    {
        var turns = await _interviews.GetTurnsAsync(p.Id);
        var open = turns.LastOrDefault(t => t.AnsweredAt is null);
        var speech = (await _language.GetSpeechConfigAsync(p.Session.SubjectId)).Data;
        var seconds = p.Session.AnswerSeconds;

        var secondsLeft = open is null
            ? 0
            : (int)Math.Clamp(seconds + ReadingGraceSeconds - (DateTime.UtcNow - open.AskedAt).TotalSeconds, 0, seconds);

        return new InterviewStateDto(
            p.Id, p.StudentCode, p.FullName, p.Session.Subject.Code,
            Finished: p.Status == ParticipantStatuses.Completed,
            TurnId: open?.Id, Kind: open?.Kind, Content: open?.Content,
            MainIndex: turns.Count(t => t.Kind == TurnKinds.Main), MainTotal: p.ParticipantQuestions.Count,
            FollowUpIndex: open is null ? 0 : turns.Count(t => t.QuestionId == open.QuestionId && t.Kind == TurnKinds.FollowUp),
            FollowUpMax: p.Session.MaxFollowUpsPerQuestion,
            SecondsLeft: secondsLeft, AnswerSeconds: seconds,
            SttLocale: speech?.SttLocale ?? "vi-VN", TtsLocale: speech?.TtsLocale ?? "vi-VN");
    }

    private static InterviewTurn NewTurn(int participantId, Question question, string kind, string content, string? reason) => new()
    {
        ParticipantId = participantId,
        QuestionId = question.Id,
        Kind = kind,
        Content = content,
        Reason = reason is null ? null : Truncate(reason, 500),
        AskedAt = DateTime.UtcNow
    };

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
