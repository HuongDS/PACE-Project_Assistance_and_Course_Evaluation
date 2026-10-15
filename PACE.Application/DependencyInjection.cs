using Microsoft.Extensions.DependencyInjection;
using PACE.Application.Analysis;
using PACE.Application.Services.Implementations;
using PACE.Application.Services.Interfaces;

namespace PACE.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAnalysisRunService, AnalysisRunService>();
        services.AddScoped<IBusinessService, BusinessService>();
        services.AddScoped<IBusinessFlowService, BusinessFlowService>();
        services.AddScoped<IMethodSourceService, MethodSourceService>();
        services.AddScoped<IRubricCriterionService, RubricCriterionService>();
        services.AddScoped<IRepoAnalysisService, RepoAnalysisService>();
        services.AddScoped<IQuestionGenerationService, QuestionGenerationService>();
        services.AddScoped<IStudentAnswerService, StudentAnswerService>();
        return services;
    }
}
