using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Analysis.Template;

/// <summary>Đọc/ghi template nghiệp vụ lưu trên <see cref="AnalysisRun"/>.</summary>
public static class StoredBusinessTemplate
{
    public static List<BusinessTemplate>? Load(AnalysisRun run)
        => run.BusinessTemplateJson is null ? null : BusinessTemplateLoader.Parse(run.BusinessTemplateJson);

    public static void Save(AnalysisRun run, IReadOnlyList<BusinessTemplate> businesses, BusinessTemplateSource source)
    {
        var json = BusinessTemplateGenerator.Serialize(businesses);
        // jsonb định dạng lại chuỗi khi lưu nên so sánh sau khi chuẩn hoá, tránh đổi TemplateUpdatedAt khi nội dung không đổi.
        if (run.TemplateSource == source && run.BusinessTemplateJson is not null && Canonical(run.BusinessTemplateJson) == json) return;

        run.BusinessTemplateJson = json;
        run.TemplateSource = source;
        // Postgres chỉ lưu tới micro-giây; làm tròn để response sau khi lưu khớp với giá trị đọc lại từ DB.
        var now = DateTime.UtcNow;
        run.TemplateUpdatedAt = now.AddTicks(-(now.Ticks % 10));
    }

    private static string? Canonical(string json)
    {
        try { return BusinessTemplateGenerator.Serialize(BusinessTemplateLoader.Parse(json)); }
        catch (BadRequestException) { return null; }
    }
}
