using Microsoft.EntityFrameworkCore;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Application.Common.Interfaces;

namespace PACE.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUser = null) : DbContext(options)
{
    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<User> Users => Set<User>();
    public DbSet<GeneratedQuestion> GeneratedQuestions => Set<GeneratedQuestion>();
    public DbSet<StudentSubmission> StudentSubmissions => Set<StudentSubmission>();
    public DbSet<StudentAnswer> StudentAnswers => Set<StudentAnswer>();
    public DbSet<RubricCriterion> RubricCriteria => Set<RubricCriterion>();
    public DbSet<RubricScore> RubricScores => Set<RubricScore>();
    public DbSet<MethodSource> MethodSources => Set<MethodSource>();
    public DbSet<BusinessMethod> BusinessMethods => Set<BusinessMethod>();
    public DbSet<BusinessFlow> BusinessFlows => Set<BusinessFlow>();
    public DbSet<DataFlowEdge> DataFlowEdges => Set<DataFlowEdge>();

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseProject> CourseProjects => Set<CourseProject>();
    public DbSet<AssessmentSession> AssessmentSessions => Set<AssessmentSession>();
    public DbSet<SessionStudent> SessionStudents => Set<SessionStudent>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (currentUser?.IsAuthenticated == true)
        {
            ChangeTracker.DetectChanges();
            var entries = ChangeTracker.Entries().Where(e => e.Entity is User or AnalysisRun or RubricCriterion or Business or GeneratedQuestion or StudentSubmission or StudentAnswer
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
            foreach (var entry in entries)
            {
                var id = entry.Property("Id").CurrentValue is Guid key ? key : Guid.Empty;
                AuditEvents.Add(new AuditEvent { ActorId = currentUser.UserId, ActorName = currentUser.Email, ResourceId = id,
                    EventType = entry.Entity.GetType().Name + "_" + entry.State, Details = "Persisted entity change." });
            }
        }
        return base.SaveChangesAsync(ct);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
