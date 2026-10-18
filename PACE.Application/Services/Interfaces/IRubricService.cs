using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IRubricCriterionService
{
    Task<IReadOnlyList<RubricCriterionDto>> GetAllAsync(CancellationToken ct = default);
    Task<RubricCriterionDto> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RubricCriterionDto> CreateAsync(UpsertRubricCriterionRequest request, CancellationToken ct = default);
    Task<RubricCriterionDto> UpdateAsync(Guid id, UpsertRubricCriterionRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<BulkImportRubricCriteriaResult> BulkImportAsync(List<UpsertRubricCriterionRequest> items, CancellationToken ct = default);
    Task<BulkImportRubricCriteriaResult> ImportFromStreamAsync(Stream stream, string fileName, CancellationToken ct = default);
    Task<byte[]> GenerateCriteriaTemplateExcelAsync(CancellationToken ct = default);
    Task<SurveySummaryResponseDto> GetSurveyResultsAsync(CancellationToken ct = default);
    Task<byte[]> ExportSurveyResultsToExcelAsync(CancellationToken ct = default);
    Task<int> SeedSampleSurveysIfEmptyAsync(CancellationToken ct = default);
    Task SubmitSurveyAsync(SubmitSurveyRequest request, Guid userId, CancellationToken ct = default);
}

