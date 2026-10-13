using System.ComponentModel.DataAnnotations;

namespace PACE.BLL.Dtos;

public record RubricCriterionDto(Guid Id, string Code, string Name, string Statement, int DisplayOrder);

public class UpsertRubricCriterionRequest
{
    [Required, MaxLength(50), RegularExpression("^[A-Z][A-Z0-9_]*$", ErrorMessage = "Code chỉ gồm chữ in hoa, số và dấu _.")]
    public string Code { get; set; } = null!;

    [Required, MaxLength(255)]
    public string Name { get; set; } = null!;

    [Required]
    public string Statement { get; set; } = null!;

    [Range(1, 1000)]
    public int DisplayOrder { get; set; }
}

