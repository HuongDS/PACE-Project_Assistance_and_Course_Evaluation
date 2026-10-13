using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Models;
using PACE.Application.Common;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Dtos;

public record MethodSourceSummaryDto(
    Guid Id,
    Guid AnalysisRunId,
    string ClassName,
    string MethodName,
    string? FilePath,
    ProgrammingLanguage Language,
    NodeType NodeType,
    string? Parameters);

public record MethodSourceDto(MethodSourceSummaryDto Method, string SourceCode, IReadOnlyList<Guid> BusinessIds);

public class MethodSourceQuery : PageQuery
{
    public Guid? AnalysisRunId { get; set; }
    public Guid? BusinessId { get; set; }
    public ProgrammingLanguage? Language { get; set; }
}
