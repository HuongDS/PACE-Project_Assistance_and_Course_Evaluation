using PACE.Application.Dtos;

namespace PACE.Application.Common.Interfaces;

public interface ISurveyExcelService
{
    byte[] ExportSurveyResults(SurveySummaryResponseDto summary);
    IReadOnlyList<UpsertRubricCriterionRequest> ReadCriteriaFromExcel(Stream stream);
    byte[] GenerateCriteriaTemplate();
}
