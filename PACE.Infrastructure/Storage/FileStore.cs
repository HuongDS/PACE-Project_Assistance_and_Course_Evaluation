using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PACE.Application.Common.Exceptions;

namespace PACE.Infrastructure.Storage;

/// <summary>Kho tệp ngoài database. Chưa cấu hình thì <see cref="Enabled"/> là false và tệp được lưu trong database.</summary>
public interface IFileStore
{
    bool Enabled { get; }
    Task UploadAsync(string key, byte[] content, string fileName, CancellationToken ct);
    Task<byte[]> DownloadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

/// <summary>Tài khoản Cloudinary đọc từ chuỗi <c>cloudinary://api_key:api_secret@cloud_name</c>.</summary>
public record CloudinaryAccount(string CloudName, string ApiKey, string ApiSecret)
{
    /// <summary>Đọc <c>Cloudinary:Url</c> hoặc biến môi trường <c>CLOUDINARY_URL</c>. Không có thì trả null; viết sai thì báo lỗi, không đưa giá trị vào thông báo.</summary>
    public static CloudinaryAccount? From(IConfiguration configuration)
    {
        var url = configuration["Cloudinary:Url"] ?? configuration["CLOUDINARY_URL"];
        return string.IsNullOrWhiteSpace(url) ? null : Parse(url);
    }

    /// <summary>Như <see cref="From"/>, nhưng chuỗi viết sai thì báo qua <paramref name="warn"/> và coi như chưa cấu hình.</summary>
    public static CloudinaryAccount? FromOrNull(IConfiguration configuration, Action<string> warn)
    {
        try
        {
            return From(configuration);
        }
        catch (InvalidOperationException ex)
        {
            warn(ex.Message + " Uploaded files stay in the database.");
            return null;
        }
    }

    public static CloudinaryAccount Parse(string url)
    {
        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == "cloudinary" && uri.Host.Length > 0)
        {
            var parts = uri.UserInfo.Split(':', 2);
            if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
                // Uri chuyển tên máy thành chữ thường, còn tên cloud phân biệt hoa thường.
                return new(url.Trim()[(url.Trim().LastIndexOf('@') + 1)..].TrimEnd('/'), Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1]));
        }
        throw new InvalidOperationException("The Cloudinary URL must look like cloudinary://api_key:api_secret@cloud_name.");
    }

    // Không in khóa bí mật khi đối tượng bị ghi vào log.
    public override string ToString() => $"CloudinaryAccount({CloudName})";
}

/// <summary>
/// Lưu tệp lên Cloudinary dạng raw, kiểu phân phối "authenticated": không có đường dẫn công khai,
/// chỉ máy chủ tải được bằng yêu cầu có chữ ký, rồi mới trả cho người đủ quyền.
/// </summary>
public class CloudinaryFileStore(HttpClient http, CloudinaryAccount? account, ILogger<CloudinaryFileStore> logger) : IFileStore
{
    private const string Delivery = "authenticated";

    public bool Enabled => account != null;

    /// <summary>Chữ ký của Cloudinary: tham số xếp theo tên, nối bằng &amp;, ghép khóa bí mật rồi băm SHA-1.</summary>
    public static string Sign(IEnumerable<KeyValuePair<string, string>> parameters, string secret)
    {
        var text = string.Join("&", parameters.Where(x => x.Value.Length > 0).OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={x.Value.Replace("&", "%26")}"));
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(text + secret))).ToLowerInvariant();
    }

    private CloudinaryAccount Account => account ?? throw new ExternalServiceException("storage", "File storage is not configured on this server.");

    private Uri Endpoint(string action) => new($"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(Account.CloudName)}/raw/{action}");

    private Dictionary<string, string> Signed(Dictionary<string, string> parameters)
    {
        parameters["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        parameters["signature"] = Sign(parameters, Account.ApiSecret);
        parameters["api_key"] = Account.ApiKey;
        return parameters;
    }

    public async Task UploadAsync(string key, byte[] content, string fileName, CancellationToken ct)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, value) in Signed(new() { ["public_id"] = key, ["type"] = Delivery }))
            form.Add(new StringContent(value), name);
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        using var response = await Send(() => http.PostAsync(Endpoint("upload"), form, ct), "upload", ct);
    }

    public async Task<byte[]> DownloadAsync(string key, CancellationToken ct)
    {
        var query = Signed(new() { ["public_id"] = key, ["type"] = Delivery });
        var url = Endpoint("download") + "?" + string.Join("&", query.Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}"));
        using var response = await Send(() => http.GetAsync(url, ct), "download", ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        var form = new FormUrlEncodedContent(Signed(new() { ["public_id"] = key, ["type"] = Delivery, ["invalidate"] = "true" }));
        using var response = await Send(() => http.PostAsync(Endpoint("destroy"), form, ct), "delete", ct);
        // Cloudinary trả 200 cả khi không xóa được gì, ví dụ {"result":"not found"}.
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!body.Contains("\"result\":\"ok\"", StringComparison.Ordinal))
        {
            logger.LogWarning("Cloudinary delete did not remove {Key}: {Answer}", key, body.Length > 200 ? body[..200] : body);
            throw new ExternalServiceException("storage", "File storage did not remove the file.");
        }
    }

    private async Task<HttpResponseMessage> Send(Func<Task<HttpResponseMessage>> request, string action, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await request();
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogWarning("Cloudinary {Action} did not complete: {Reason}", action, ex.GetType().Name);
            throw new ExternalServiceException("storage", "File storage is unreachable. Try again.");
        }
        if (response.IsSuccessStatusCode)
            return response;
        // Lý do nằm ở tiêu đề X-Cld-Error; không ghi địa chỉ yêu cầu vì chứa chữ ký.
        var reason = response.Headers.TryGetValues("X-Cld-Error", out var values) ? string.Join("; ", values) : "";
        logger.LogWarning("Cloudinary {Action} failed with {Status}: {Reason}", action, (int)response.StatusCode, reason);
        response.Dispose();
        throw new ExternalServiceException("storage", $"File storage rejected the {action} request.");
    }
}
