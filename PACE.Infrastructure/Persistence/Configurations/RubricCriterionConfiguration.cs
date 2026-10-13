using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Infrastructure.Persistence.Configurations;

public class RubricCriterionConfiguration : IEntityTypeConfiguration<RubricCriterion>
{
    public void Configure(EntityTypeBuilder<RubricCriterion> e)
    {
        e.ToTable("rubric_criteria");
        e.HasKey(x => x.Id);
        e.Property(x => x.Code).IsRequired().HasMaxLength(50);
        e.Property(x => x.Name).IsRequired().HasMaxLength(255);
        e.Property(x => x.Statement).IsRequired();
        e.HasIndex(x => x.Code).IsUnique();

        e.HasData(
            new RubricCriterion { Id = Guid.Parse("b2e1d3c5-0001-4d7f-8c2b-000000000001"), Code = "ACCURACY", Name = "Độ chính xác kỹ thuật", Statement = "Nhận xét đúng về mặt kỹ thuật so với code thực tế trong repo của nhóm tôi.", DisplayOrder = 1 },
            new RubricCriterion { Id = Guid.Parse("b2e1d3c5-0001-4d7f-8c2b-000000000002"), Code = "FAIRNESS", Name = "Công bằng", Statement = "Điểm số và nhận xét phản ánh công bằng chất lượng câu trả lời của tôi.", DisplayOrder = 2 },
            new RubricCriterion { Id = Guid.Parse("b2e1d3c5-0001-4d7f-8c2b-000000000003"), Code = "SPECIFICITY", Name = "Cụ thể", Statement = "Nhận xét chỉ rõ phần nào trong câu trả lời của tôi đúng, phần nào sai hoặc còn thiếu.", DisplayOrder = 3 },
            new RubricCriterion { Id = Guid.Parse("b2e1d3c5-0001-4d7f-8c2b-000000000004"), Code = "ACTIONABILITY", Name = "Có thể hành động", Statement = "Sau khi đọc, tôi biết cụ thể cần làm gì để cải thiện câu trả lời.", DisplayOrder = 4 },
            new RubricCriterion { Id = Guid.Parse("b2e1d3c5-0001-4d7f-8c2b-000000000005"), Code = "USEFULNESS", Name = "Hữu ích", Statement = "Nhận xét này giúp tôi hiểu rõ hơn luồng xử lý trong project của mình.", DisplayOrder = 5 },
            new RubricCriterion { Id = Guid.Parse("b2e1d3c5-0001-4d7f-8c2b-000000000006"), Code = "CLARITY", Name = "Rõ ràng", Statement = "Nhận xét được trình bày rõ ràng, dễ hiểu.", DisplayOrder = 6 },
            new RubricCriterion { Id = Guid.Parse("b2e1d3c5-0001-4d7f-8c2b-000000000007"), Code = "ACCEPTANCE", Name = "Chấp nhận", Statement = "Tôi đồng ý và chấp nhận nhận xét này.", DisplayOrder = 7 },
            new RubricCriterion { Id = Guid.Parse("b2e1d3c5-0001-4d7f-8c2b-000000000008"), Code = "WILLINGNESS", Name = "Sẵn sàng cải thiện", Statement = "Tôi sẵn sàng sửa lại câu trả lời dựa trên nhận xét này.", DisplayOrder = 8 },
            new RubricCriterion { Id = Guid.Parse("b2e1d3c5-0001-4d7f-8c2b-000000000009"), Code = "AFFECT", Name = "Cảm xúc", Statement = "Đọc nhận xét này khiến tôi thấy được động viên, không bị nản.", DisplayOrder = 9 }
        );
    }
}
