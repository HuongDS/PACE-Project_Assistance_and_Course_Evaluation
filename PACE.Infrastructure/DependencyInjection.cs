using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PACE.Application.Analysis;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Common.Interfaces;
using PACE.Application.Interfaces.Repositories;
using PACE.Application.QuestionGeneration;
using PACE.Application.Services.Interfaces;
using PACE.Infrastructure.AI;
using PACE.Infrastructure.Analysis;
using PACE.Infrastructure.Analysis.Parsing;
using PACE.Infrastructure.Excel;
using PACE.Infrastructure.Identity;
using PACE.Infrastructure.Llm;
using PACE.Infrastructure.Persistence;
using PACE.Infrastructure.Repositories;

namespace PACE.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npg => npg.EnableRetryOnFailure(3)));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.Configure<AnalysisOptions>(configuration.GetSection(AnalysisOptions.SectionName));
        services.AddSingleton<IGitCloneService, GitCloneService>();
        services.AddSingleton<IRepositoryScanner, RepositoryScanner>();
        services.AddSingleton<ILanguageParser, CSharpRoslynParser>();
        services.AddSingleton<ILanguageParser, JavaTreeSitterParser>();
        services.AddSingleton<ILanguageParser, JsTsTreeSitterParser>();

        services.Configure<PACE.Infrastructure.AI.GeminiOptions>(configuration.GetSection(PACE.Infrastructure.AI.GeminiOptions.SectionName));
        services.AddSingleton<IBusinessGroupingAi, GeminiBusinessGrouper>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IAdminCredentialValidator, AdminCredentialValidator>();
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();

        services.AddSingleton<IUserImportReader, ClosedXmlUserImportReader>();
        services.AddSingleton<IRepoLinkReader, ClosedXmlRepoLinkReader>();
        services.AddSingleton<ISurveyExcelService, ClosedXmlSurveyExcelService>();

        // LLM Service & HttpClient
        services.AddHttpClient();
        services.AddScoped<ILlmService, DeepSeekService>();
        services.AddScoped<IGroqService, GroqService>();

        services.Configure<QuestionGenerationOptions>(configuration.GetSection(QuestionGenerationOptions.SectionName));
        services.AddSingleton<IQuestionLlmClient, GeminiQuestionLlmClient>();
        return services;
    }
}
