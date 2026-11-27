using PACE.Application.Common.Exceptions;

namespace PACE.API.Controllers;

public static class ProposalFileRules
{
    public const long MaxFileBytes = 10 * 1024 * 1024;
    public const long MaxRequestBytes = MaxFileBytes + 1024 * 1024;
    public const string RequiredMessage = "Cần đính kèm file proposal (PDF, DOC hoặc DOCX) khi gửi đề tài.";

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".doc"] = "application/msword"
    };

    public static async Task<ProposalFile> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null)
            throw new BadRequestException(RequiredMessage);
        if (file.Length == 0)
            throw new BadRequestException("File proposal đang trống.");
        if (file.Length > MaxFileBytes)
            throw new BadRequestException("File proposal tối đa 10 MB.");

        var extension = Path.GetExtension(file.FileName);
        if (!ContentTypes.TryGetValue(extension, out var contentType))
            throw new BadRequestException("File proposal chỉ nhận PDF, DOC hoặc DOCX.");

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        var bytes = stream.ToArray();
        if (!MatchesSignature(extension, bytes))
            throw new BadRequestException("Nội dung file không đúng định dạng PDF, DOC hoặc DOCX.");

        var name = Path.GetFileName(file.FileName);
        if (name.Length > 255) name = name[^255..];
        return new ProposalFile(name, contentType, bytes);
    }

    private static bool MatchesSignature(string extension, byte[] b) => extension.ToLowerInvariant() switch
    {
        ".pdf" => b.Length > 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46,
        ".docx" => b.Length > 4 && b[0] == 0x50 && b[1] == 0x4B && b[2] == 0x03 && b[3] == 0x04,
        ".doc" => b.Length > 8 && b[0] == 0xD0 && b[1] == 0xCF && b[2] == 0x11 && b[3] == 0xE0,
        _ => false
    };
}
