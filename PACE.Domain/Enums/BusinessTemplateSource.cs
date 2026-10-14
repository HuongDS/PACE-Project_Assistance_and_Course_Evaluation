namespace PACE.Domain.Enums;

public enum BusinessTemplateSource
{
    /// <summary>File template_business.json có sẵn trong repo.</summary>
    Repository,
    /// <summary>Bản nháp do Gemini gom endpoint thành nghiệp vụ.</summary>
    Ai,
    /// <summary>Bản nháp gom mỗi controller thành một nghiệp vụ.</summary>
    Controller,
    /// <summary>Admin đã xem, chỉnh và chốt; được ưu tiên hơn file trong repo.</summary>
    Confirmed
}
