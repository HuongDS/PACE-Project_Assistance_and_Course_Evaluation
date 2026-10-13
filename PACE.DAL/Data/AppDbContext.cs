using Microsoft.EntityFrameworkCore;
using PACE.DAL.Models;

namespace PACE.DAL.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<User> Users => Set<User>();
    public DbSet<GeneratedQuestion> GeneratedQuestions => Set<GeneratedQuestion>();
    public DbSet<StudentAnswer> StudentAnswers => Set<StudentAnswer>();
    public DbSet<RubricCriterion> RubricCriteria => Set<RubricCriterion>();
    public DbSet<RubricScore> RubricScores => Set<RubricScore>();
    public DbSet<MethodSource> MethodSources => Set<MethodSource>();
    public DbSet<BusinessMethod> BusinessMethods => Set<BusinessMethod>();
    public DbSet<BusinessFlow> BusinessFlows => Set<BusinessFlow>();
    public DbSet<DataFlowEdge> DataFlowEdges => Set<DataFlowEdge>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
