namespace PACE.DAL.Repositories.Interfaces;

public interface IUnitOfWork
{
    IAnalysisRunRepository AnalysisRuns { get; }
    IBusinessRepository Businesses { get; }
    IUserRepository Users { get; }
    IMethodSourceRepository MethodSources { get; }
    IBusinessMethodRepository BusinessMethods { get; }
    IBusinessFlowRepository BusinessFlows { get; }
    IDataFlowEdgeRepository DataFlowEdges { get; }
    IRubricCriterionRepository RubricCriteria { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);

    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default);

    void ClearChangeTracker();
}
