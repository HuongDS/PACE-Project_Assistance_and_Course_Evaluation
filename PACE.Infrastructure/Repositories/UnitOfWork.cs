using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Interfaces.Repositories;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private IAnalysisRunRepository? _analysisRuns;
    private IBusinessRepository? _businesses;
    private IStudentSubmissionRepository? _studentSubmissions;
    private IUserRepository? _users;
    private IMethodSourceRepository? _methodSources;
    private IBusinessMethodRepository? _businessMethods;
    private IBusinessFlowRepository? _businessFlows;
    private IDataFlowEdgeRepository? _dataFlowEdges;
    private IRubricCriterionRepository? _rubricCriteria;
    private IStudentAnswerRepository? _studentAnswers;
    private IGeneratedQuestionRepository? _generatedQuestions;

    public IAnalysisRunRepository AnalysisRuns => _analysisRuns ??= new AnalysisRunRepository(context);
    public IBusinessRepository Businesses => _businesses ??= new BusinessRepository(context);
    public IStudentSubmissionRepository StudentSubmissions => _studentSubmissions ??= new StudentSubmissionRepository(context);
    public IUserRepository Users => _users ??= new UserRepository(context);
    public IMethodSourceRepository MethodSources => _methodSources ??= new MethodSourceRepository(context);
    public IBusinessMethodRepository BusinessMethods => _businessMethods ??= new BusinessMethodRepository(context);
    public IBusinessFlowRepository BusinessFlows => _businessFlows ??= new BusinessFlowRepository(context);
    public IDataFlowEdgeRepository DataFlowEdges => _dataFlowEdges ??= new DataFlowEdgeRepository(context);
    public IRubricCriterionRepository RubricCriteria => _rubricCriteria ??= new RubricCriterionRepository(context);
    public IStudentAnswerRepository StudentAnswers => _studentAnswers ??= new StudentAnswerRepository(context);
    public IGeneratedQuestionRepository GeneratedQuestions => _generatedQuestions ??= new GeneratedQuestionRepository(context);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => context.SaveChangesAsync(ct);

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
        => ExecuteInTransactionAsync<bool>(async token =>
        {
            await action(token);
            return true;
        }, ct);

    public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async token =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(token);
            try
            {
                var result = await action(token);
                await context.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                context.ChangeTracker.Clear();
                throw;
            }
        }, ct);
    }

    public void ClearChangeTracker() => context.ChangeTracker.Clear();
}