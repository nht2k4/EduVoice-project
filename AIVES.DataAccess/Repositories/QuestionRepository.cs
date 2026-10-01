using AIVES.DataAccess.Entities;
using AIVES.DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AIVES.DataAccess.Repositories;

public class QuestionRepository : IQuestionRepository
{
    private readonly AppDbContext _context;

    public QuestionRepository(AppDbContext context) => _context = context;

    public Task<List<Question>> GetBySubjectAsync(int subjectId, bool onlyActive) =>
        _context.Questions.AsNoTracking()
            .Where(q => q.SubjectId == subjectId && (!onlyActive || q.IsActive))
            .OrderBy(q => q.Id)
            .ToListAsync();

    public Task<Question?> GetByIdAsync(int id) =>
        _context.Questions.FirstOrDefaultAsync(q => q.Id == id);

    public async Task AddAsync(Question question)
    {
        _context.Questions.Add(question);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Question question)
    {
        _context.Questions.Update(question);
        await _context.SaveChangesAsync();
    }
}
