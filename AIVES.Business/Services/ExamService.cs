using AIVES.Business.Common;
using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.Business.Models;
using AIVES.DataAccess.Entities;
using AIVES.DataAccess.Repositories.Interfaces;

namespace AIVES.Business.Services;

public class ExamService : IExamService
{
    private const int MaxStudents = 200;

    private readonly IExamRepository _exams;
    private readonly IQuestionRepository _questions;
    private readonly IAccountRepository _accounts;
    private readonly ISubjectRepository _subjects;
    private readonly IAssignmentService _assignmentService;

    public ExamService(IExamRepository exams, IQuestionRepository questions, IAccountRepository accounts,
        ISubjectRepository subjects, IAssignmentService assignmentService)
    {
        _exams = exams;
        _questions = questions;
        _accounts = accounts;
        _subjects = subjects;
        _assignmentService = assignmentService;
    }

    public async Task<List<ExamListItemDto>> GetListAsync(int currentUserId)
    {
        var subjectIds = await AccessibleSubjectIdsAsync(currentUserId);
        var sessions = await _exams.GetListAsync(subjectIds);
        return sessions
            .Select(s => new ExamListItemDto(s.Id, s.Title, s.Subject.Code, s.Subject.Name, s.StartTime, s.ExamParticipants.Count, s.Status))
            .ToList();
    }

    public async Task<List<SubjectDto>> GetSubjectsForExamAsync(int currentUserId)
    {
        var account = await _accounts.GetByIdAsync(currentUserId);
        if (account is null || !account.IsActive) return new();

        var subjects = account.Role == Roles.Admin
            ? await _subjects.GetAllWithAssignmentsAsync()
            : await _subjects.GetByLecturerAsync(currentUserId);

        return subjects.Where(s => s.IsActive).Select(s => s.ToDto()).ToList();
    }

    public async Task<ServiceResult<int>> CreateAsync(int currentUserId, CreateExamRequest r)
    {
        var title = r.Title?.Trim() ?? string.Empty;
        if (title.Length == 0 || title.Length > 150)
            return ServiceResult<int>.Fail("Tên phiên thi bắt buộc và tối đa 150 ký tự.");
        if (r.StartTime < DateTime.Now.AddMinutes(-5))
            return ServiceResult<int>.Fail("Thời điểm bắt đầu không được ở trong quá khứ.");
        if (r.StartTime > DateTime.Now.AddYears(1))
            return ServiceResult<int>.Fail("Thời điểm bắt đầu không được vượt quá 1 năm tới.");
        if (r.SlotMinutes is < 1 or > 120)
            return ServiceResult<int>.Fail("Khung thời gian mỗi thí sinh từ 1 đến 120 phút.");
        if (r.MainQuestionCount is < 1 or > 20)
            return ServiceResult<int>.Fail("Số câu hỏi chính từ 1 đến 20.");
        if (r.MaxFollowUps is < 0 or > 40)
            return ServiceResult<int>.Fail("Số câu đào sâu tối đa mỗi thí sinh từ 0 đến 40.");
        if (r.MaxFollowUpsPerQuestion is < 0 or > 5)
            return ServiceResult<int>.Fail("Số lượt hỏi xoáy tối đa mỗi câu từ 0 đến 5.");
        if (r.AnswerSeconds is < 10 or > 600)
            return ServiceResult<int>.Fail("Thời gian trả lời mỗi câu từ 10 đến 600 giây.");

        var students = ParseStudents(r.StudentList, out var parseError);
        if (parseError is not null) return ServiceResult<int>.Fail(parseError);

        var subject = await _subjects.GetByIdAsync(r.SubjectId);
        if (subject is null)
            return ServiceResult<int>.NotFound("Không tìm thấy môn học.");
        if (!await _assignmentService.CanAccessSubjectAsync(currentUserId, r.SubjectId))
            return ServiceResult<int>.Forbidden("Bạn chưa được phân công môn học này.");
        if (!subject.IsActive)
            return ServiceResult<int>.Fail($"Môn {subject.Code} đã ngừng hoạt động, không thể tạo phiên thi.");

        var pool = await _questions.GetBySubjectAsync(r.SubjectId, onlyActive: true);
        if (pool.Count < r.MainQuestionCount)
            return ServiceResult<int>.Fail($"Ngân hàng câu hỏi của môn {subject.Code} chỉ có {pool.Count} câu đang dùng, chưa đủ {r.MainQuestionCount} câu chính.");

        var session = new ExamSession
        {
            SubjectId = r.SubjectId,
            Title = title,
            StartTime = r.StartTime,
            SlotMinutes = r.SlotMinutes,
            MainQuestionCount = r.MainQuestionCount,
            MaxFollowUps = r.MaxFollowUps,
            MaxFollowUpsPerQuestion = r.MaxFollowUpsPerQuestion,
            AnswerSeconds = r.AnswerSeconds,
            Status = ExamStatuses.Open,
            CreatedBy = currentUserId
        };

        for (var i = 0; i < students.Count; i++)
        {
            var slotStart = r.StartTime.AddMinutes(i * r.SlotMinutes);
            session.ExamParticipants.Add(new ExamParticipant
            {
                StudentCode = students[i].Code,
                FullName = students[i].Name,
                SlotStart = slotStart,
                SlotEnd = slotStart.AddMinutes(r.SlotMinutes),
                Status = ParticipantStatuses.Scheduled
            });
        }

        Allocate(session, pool.Select(q => q.Id).ToList());
        await _exams.AddAsync(session);
        return ServiceResult<int>.Ok(session.Id);
    }

    public async Task<ServiceResult<ExamDetailDto>> GetDetailAsync(int currentUserId, int examId)
    {
        var session = await _exams.GetDetailAsync(examId);
        if (session is null)
            return ServiceResult<ExamDetailDto>.NotFound("Không tìm thấy phiên thi.");
        if (!await _assignmentService.CanAccessSubjectAsync(currentUserId, session.SubjectId))
            return ServiceResult<ExamDetailDto>.Forbidden("Bạn chưa được phân công môn học này.");

        var participants = session.ExamParticipants
            .OrderBy(p => p.SlotStart)
            .Select(p => new ParticipantDto(
                p.Id, p.StudentCode, p.FullName, p.SlotStart, p.SlotEnd, p.Status,
                p.ParticipantQuestions.OrderBy(pq => pq.OrderNo).Select(pq => pq.Question.Content).ToList()))
            .ToList();

        return ServiceResult<ExamDetailDto>.Ok(new ExamDetailDto(
            session.Id, session.SubjectId, session.Subject.Code, session.Subject.Name, session.Title,
            session.StartTime, session.SlotMinutes, session.MainQuestionCount,
            session.MaxFollowUps, session.MaxFollowUpsPerQuestion, session.AnswerSeconds, session.Status, participants));
    }

    public async Task<ServiceResult> SetOpenAsync(int currentUserId, int examId, bool open)
    {
        var session = await _exams.GetDetailAsync(examId);
        if (session is null)
            return ServiceResult.NotFound("Không tìm thấy phiên thi.");
        if (!await _assignmentService.CanAccessSubjectAsync(currentUserId, session.SubjectId))
            return ServiceResult.Forbidden("Bạn chưa được phân công môn học này.");

        session.Status = open ? ExamStatuses.Open : ExamStatuses.Closed;
        await _exams.SaveAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ReallocateAsync(int currentUserId, int examId)
    {
        var session = await _exams.GetDetailAsync(examId);
        if (session is null)
            return ServiceResult.NotFound("Không tìm thấy phiên thi.");
        if (!await _assignmentService.CanAccessSubjectAsync(currentUserId, session.SubjectId))
            return ServiceResult.Forbidden("Bạn chưa được phân công môn học này.");
        if (session.ExamParticipants.Any(p => p.Status != ParticipantStatuses.Scheduled))
            return ServiceResult.Fail("Đã có thí sinh bắt đầu thi, không thể chọn lại câu hỏi.");

        var pool = await _questions.GetBySubjectAsync(session.SubjectId, onlyActive: true);
        if (pool.Count < session.MainQuestionCount)
            return ServiceResult.Fail($"Ngân hàng câu hỏi chỉ có {pool.Count} câu đang dùng, chưa đủ {session.MainQuestionCount} câu chính.");

        // Lưu riêng bước xóa trước khi thêm: có thể trùng khóa (thí sinh, câu hỏi) với bản ghi cũ
        foreach (var p in session.ExamParticipants) p.ParticipantQuestions.Clear();
        await _exams.SaveAsync();

        Allocate(session, pool.Select(q => q.Id).ToList());
        await _exams.SaveAsync();
        return ServiceResult.Ok();
    }

    private static void Allocate(ExamSession session, IReadOnlyList<int> poolIds)
    {
        var ordered = session.ExamParticipants.OrderBy(p => p.SlotStart).ToList();
        var sets = QuestionAllocator.Allocate(poolIds, ordered.Count, session.MainQuestionCount, Random.Shared);

        for (var i = 0; i < ordered.Count; i++)
            for (var n = 0; n < sets[i].Length; n++)
                ordered[i].ParticipantQuestions.Add(new ParticipantQuestion { QuestionId = sets[i][n], OrderNo = n + 1 });
    }

    // Admin: null = mọi môn. Giảng viên: các môn được phân công. Tài khoản bị khóa: rỗng.
    private async Task<IReadOnlyCollection<int>?> AccessibleSubjectIdsAsync(int userId)
    {
        var account = await _accounts.GetByIdAsync(userId);
        if (account is null || !account.IsActive) return Array.Empty<int>();
        if (account.Role == Roles.Admin) return null;

        return (await _subjects.GetByLecturerAsync(userId)).Select(s => s.Id).ToList();
    }

    // Mỗi dòng "MSSV; Họ tên" (cũng nhận dấu phẩy, tab hoặc khoảng trắng làm dấu ngăn)
    private static List<(string Code, string Name)> ParseStudents(string? text, out string? error)
    {
        error = null;
        var result = new List<(string, string)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = (text ?? string.Empty).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < lines.Length; i++)
        {
            var sep = lines[i].IndexOfAny(new[] { ';', ',', '\t', ' ' });
            var code = sep < 0 ? lines[i] : lines[i][..sep].Trim();
            var name = sep < 0 ? string.Empty : lines[i][(sep + 1)..].Trim();

            if (code.Length == 0 || name.Length == 0 || code.Length > 30 || name.Length > 100)
                error = $"Dòng {i + 1} không hợp lệ. Mỗi dòng phải có dạng \"MSSV; Họ tên\".";
            else if (!seen.Add(code))
                error = $"Mã sinh viên {code} bị trùng trong danh sách.";

            if (error is not null) return result;
            result.Add((code, name));
        }

        if (result.Count == 0) error = "Nhập ít nhất một sinh viên.";
        else if (result.Count > MaxStudents) error = $"Tối đa {MaxStudents} sinh viên mỗi phiên thi.";
        return result;
    }
}
