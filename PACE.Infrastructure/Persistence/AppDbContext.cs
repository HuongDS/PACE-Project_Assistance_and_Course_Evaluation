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
    public DbSet<MethodSource> MethodSources => Set<MethodSource>();
    public DbSet<BusinessMethod> BusinessMethods => Set<BusinessMethod>();
    public DbSet<BusinessFlow> BusinessFlows => Set<BusinessFlow>();
    public DbSet<DataFlowEdge> DataFlowEdges => Set<DataFlowEdge>();

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseProject> CourseProjects => Set<CourseProject>();
    public DbSet<AssessmentSession> AssessmentSessions => Set<AssessmentSession>();
    public DbSet<SessionStudent> SessionStudents => Set<SessionStudent>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<LecturerProfile> LecturerProfiles => Set<LecturerProfile>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<StudentClass> StudentClasses => Set<StudentClass>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
    public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<OralExam> OralExams => Set<OralExam>();
    public DbSet<OralQuestion> OralQuestions => Set<OralQuestion>();
    public DbSet<StudentOralSession> StudentOralSessions => Set<StudentOralSession>();
    public DbSet<StudentOralAnswer> StudentOralAnswers => Set<StudentOralAnswer>();

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (currentUser?.IsAuthenticated == true)
        {
            ChangeTracker.DetectChanges();
            var entries = ChangeTracker.Entries().Where(e => e.Entity is User or AnalysisRun or Business or GeneratedQuestion or StudentSubmission or StudentAnswer
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
