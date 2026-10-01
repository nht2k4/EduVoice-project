using AIVES.Business.Interfaces;
using AIVES.Business.Models;
using AIVES.Business.Security;
using AIVES.Business.Services;
using AIVES.DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace AIVES.Business;

public static class DependencyInjection
{
    // Tầng Presentation chỉ gọi hàm này; nó không cần biết tầng DataAccess tồn tại
    public static IServiceCollection AddBusinessLayer(this IServiceCollection services, string connectionString, AiOptions? ai = null)
    {
        services.AddDataAccessLayer(connectionString);

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<IAssignmentService, AssignmentService>();
        services.AddScoped<ILanguageConfigService, LanguageConfigService>();

        // Chức năng 2 + 3
        services.AddSingleton(ai ?? new AiOptions());
        services.AddSingleton<IFollowUpGenerator, FollowUpGenerator>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IExamService, ExamService>();
        services.AddScoped<IInterviewService, InterviewService>();

        return services;
    }
}
