using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PACE.DAL.Models;

namespace PACE.DAL.Data.Configurations;

public class GeneratedQuestionConfiguration : IEntityTypeConfiguration<GeneratedQuestion>
{
    public void Configure(EntityTypeBuilder<GeneratedQuestion> e)
    {
        e.ToTable("generated_questions");
        e.HasKey(x => x.Id);
        e.Property(x => x.QuestionText).IsRequired();
        e.Property(x => x.Difficulty).HasMaxLength(20);
        e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        e.HasOne(x => x.Business)
            .WithMany(b => b.Questions)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
