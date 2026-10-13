using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PACE.BLL.Analysis;
using PACE.BLL.Analysis.Git;
using PACE.BLL.Analysis.Parsing;
using PACE.BLL.Services.Implementations;
using PACE.BLL.Services.Interfaces;

namespace PACE.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AnalysisOptions>(configuration.GetSection(AnalysisOptions.SectionName));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAnalysisRunService, AnalysisRunService>();
        services.AddScoped<IBusinessService, BusinessService>();
        services.AddScoped<IBusinessFlowService, BusinessFlowService>();
        services.AddScoped<IMethodSourceService, MethodSourceService>();
        services.AddScoped<IRubricCriterionService, RubricCriterionService>();

        services.AddSingleton<IGitCloneService, GitCloneService>();
        services.AddSingleton<CSharpRoslynParser>();
        services.AddSingleton<JavaTreeSitterParser>();
        services.AddSingleton<JsTsTreeSitterParser>();
        services.AddScoped<IRepoAnalysisService, RepoAnalysisService>();

        return services;
    }
}
