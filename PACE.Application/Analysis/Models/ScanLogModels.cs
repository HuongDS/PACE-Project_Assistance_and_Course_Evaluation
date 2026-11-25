namespace PACE.Application.Analysis.Models;

public enum ScanFileStatus
{
    Accepted,
    Ignored,
    Failed
}

/// <summary>Một dòng log quét repo: file hoặc thư mục, kết quả và lý do. Không chứa nội dung file.</summary>
public sealed record ScanFileEntry(string RelativePath, ScanFileStatus Status, string Reason);

/// <summary>Một chỗ đã che trong file. Không chứa giá trị bí mật.</summary>
public sealed record RedactionFinding(string RelativePath, string Rule, int Line);
