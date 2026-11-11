using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.Domain.Entities;

namespace PACE.Infrastructure.Persistence.Configurations;

public class OralExamConfiguration : IEntityTypeConfiguration<OralExam>
{
    public void Configure(EntityTypeBuilder<OralExam> e)
    {
        e.ToTable("oral_exams");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.ClassId).HasColumnName("class_id").IsRequired();
        e.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        e.Property(x => x.Description).HasColumnName("description");
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

        e.HasOne(x => x.Class)
            .WithMany(c => c.OralExams)
            .HasForeignKey(x => x.ClassId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OralQuestionConfiguration : IEntityTypeConfiguration<OralQuestion>
{
    public void Configure(EntityTypeBuilder<OralQuestion> e)
    {
        e.ToTable("oral_questions");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.OralExamId).HasColumnName("oral_exam_id").IsRequired();
        e.Property(x => x.GeneratedQuestionId).HasColumnName("generated_question_id");
        e.Property(x => x.Content).HasColumnName("content").IsRequired();
        e.Property(x => x.ReferenceContext).HasColumnName("reference_context");
        e.Property(x => x.ExpectedKeywords).HasColumnName("expected_keywords");

        e.HasOne(x => x.OralExam)
            .WithMany(o => o.Questions)
            .HasForeignKey(x => x.OralExamId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.GeneratedQuestion)
            .WithMany()
            .HasForeignKey(x => x.GeneratedQuestionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class StudentOralSessionConfiguration : IEntityTypeConfiguration<StudentOralSession>
{
    public void Configure(EntityTypeBuilder<StudentOralSession> e)
    {
        e.ToTable("student_oral_sessions");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.OralExamId).HasColumnName("oral_exam_id").IsRequired();
        e.Property(x => x.StudentCode).HasColumnName("student_code").HasMaxLength(20).IsRequired();
        e.Property(x => x.ExaminerCode).HasColumnName("examiner_code").HasMaxLength(20);
        e.Property(x => x.StartTime).HasColumnName("start_time").IsRequired();
        e.Property(x => x.EndTime).HasColumnName("end_time");
        e.Property(x => x.AudioRecordUrl).HasColumnName("audio_record_url");
        e.Property(x => x.ScreenRecordUrl).HasColumnName("screen_record_url");
        e.Property(x => x.AiEvaluationSummary).HasColumnName("ai_evaluation_summary");
        e.Property(x => x.FinalFeedback).HasColumnName("final_feedback");

        e.HasOne(x => x.OralExam)
            .WithMany(o => o.StudentOralSessions)
            .HasForeignKey(x => x.OralExamId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.StudentProfile)
            .WithMany(s => s.OralSessions)
            .HasForeignKey(x => x.StudentCode)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Examiner)
            .WithMany(l => l.ExaminedOralSessions)
            .HasForeignKey(x => x.ExaminerCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class StudentOralAnswerConfiguration : IEntityTypeConfiguration<StudentOralAnswer>
{
    public void Configure(EntityTypeBuilder<StudentOralAnswer> e)
    {
        e.ToTable("student_oral_answers");
        e.HasKey(x => x.Id);

        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.SessionId).HasColumnName("session_id").IsRequired();
        e.Property(x => x.QuestionId).HasColumnName("question_id").IsRequired();
        e.Property(x => x.StudentAnswer).HasColumnName("student_answer");
        e.Property(x => x.Feedback).HasColumnName("feedback");

        e.HasOne(x => x.Session)
            .WithMany(s => s.Answers)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Question)
            .WithMany(q => q.Answers)
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

