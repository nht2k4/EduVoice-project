using AIVES.DataAccess.Entities;
using AIVES.DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AIVES.DataAccess.Repositories;

public class ExamRepository : IExamRepository
{
    private readonly AppDbContext _context;

    public ExamRepository(AppDbContext context) => _context = context;

    public Task<List<ExamSession>> GetListAsync(IReadOnlyCollection<int>? subjectIds) =>
        _context.ExamSessions.AsNoTracking()
            .Include(s => s.Subject)
            .Include(s => s.ExamParticipants)
            .Where(s => subjectIds == null || subjectIds.Contains(s.SubjectId))
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();

    public Task<ExamSession?> GetDetailAsync(int id) =>
        _context.ExamSessions
            .Include(s => s.Subject)
            .Include(s => s.ExamParticipants).ThenInclude(p => p.ParticipantQuestions).ThenInclude(pq => pq.Question)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task AddAsync(ExamSession session)
    {
        _context.ExamSessions.Add(session);
        await _context.SaveChangesAsync();
    }

    public Task SaveAsync() => _context.SaveChangesAsync();
}
