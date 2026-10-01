using AIVES.Business.Common;
using AIVES.Business.DTOs;
using AIVES.Business.Interfaces;
using AIVES.DataAccess.Entities;
using AIVES.DataAccess.Repositories.Interfaces;

namespace AIVES.Business.Services;

public class QuestionService : IQuestionService
{
    private readonly IQuestionRepository _questions;
    private readonly IAssignmentService _assignmentService;

    public QuestionService(IQuestionRepository questions, IAssignmentService assignmentService)
    {
        _questions = questions;
        _assignmentService = assignmentService;
    }

    public async Task<ServiceResult<List<QuestionDto>>> GetBySubjectAsync(int currentUserId, int subjectId)
    {
        if (!await _assignmentService.CanAccessSubjectAsync(currentUserId, subjectId))
            return ServiceResult<List<QuestionDto>>.Forbidden("Bạn chưa được phân công môn học này.");

        var list = await _questions.GetBySubjectAsync(subjectId, onlyActive: false);
        return ServiceResult<List<QuestionDto>>.Ok(
            list.Select(q => new QuestionDto(q.Id, q.Content, q.ExpectedPoints, q.IsActive)).ToList());
    }

    public async Task<ServiceResult> AddAsync(int currentUserId, AddQuestionRequest request)
    {
        var content = request.Content?.Trim() ?? string.Empty;
        var points = request.ExpectedPoints?.Trim();

        if (content.Length == 0)
            return ServiceResult.Fail("Nội dung câu hỏi không được để trống.");
        if (content.Length > 1000 || points?.Length > 1000)
            return ServiceResult.Fail("Nội dung và ý chính tối đa 1000 ký tự.");

        if (!await _assignmentService.CanAccessSubjectAsync(currentUserId, request.SubjectId))
            return ServiceResult.Forbidden("Bạn chưa được phân công môn học này.");

        await _questions.AddAsync(new Question
        {
            SubjectId = request.SubjectId,
            Content = content,
            ExpectedPoints = string.IsNullOrEmpty(points) ? null : points,
            IsActive = true
        });
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetActiveAsync(int currentUserId, int questionId, bool isActive)
    {
        var question = await _questions.GetByIdAsync(questionId);
        if (question is null)
            return ServiceResult.NotFound("Không tìm thấy câu hỏi.");

        if (!await _assignmentService.CanAccessSubjectAsync(currentUserId, question.SubjectId))
            return ServiceResult.Forbidden("Bạn chưa được phân công môn học này.");

        question.IsActive = isActive;
        await _questions.UpdateAsync(question);
        return ServiceResult.Ok();
    }
}
