using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PACE.API.Controllers;
using PACE.Application.Common.Exceptions;
using PACE.Infrastructure.Storage;
using Xunit;

namespace PACE.Tests;

/// <summary>Yêu cầu gửi sang Cloudinary phải ký đúng, không lộ khóa bí mật và không tạo đường dẫn công khai.</summary>
public class FileStoreTests
{
    private static readonly CloudinaryAccount Account = new("demo-cloud", "1234", "abcd");

    private sealed class Recorder(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? Address { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Address = request.RequestUri;
            Body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new ByteArrayContent([1, 2, 3]) };
        }
    }

    private static CloudinaryFileStore Store(Recorder recorder, CloudinaryAccount? account) =>
        new(new HttpClient(recorder), account, NullLogger<CloudinaryFileStore>.Instance);

    // Hai ví dụ trong tài liệu chữ ký của Cloudinary, khóa bí mật "abcd".
    [Fact]
    public void Signature_Matches_The_Documented_Examples()
    {
        Assert.Equal("a21ad0f63beb4de2e5575204b79ab90bffb02c10",
            CloudinaryFileStore.Sign(new Dictionary<string, string> { ["timestamp"] = "1315060510" }, "abcd"));
        Assert.Equal("bfd09f95f331f558cbd1320e67aa8d488770583e",
            CloudinaryFileStore.Sign(new Dictionary<string, string>
            {
                ["timestamp"] = "1315060510", ["public_id"] = "sample_image", ["eager"] = "w_400,h_300,c_pad|w_260,h_200,c_crop"
            }, "abcd"));
    }

    [Fact]
    public void Account_Is_Read_From_The_Url_Keeping_The_Cloud_Name_Case()
    {
        var account = CloudinaryAccount.Parse("cloudinary://1234:s3cr-et_x@PaceCloud");

        Assert.Equal(("PaceCloud", "1234", "s3cr-et_x"), (account.CloudName, account.ApiKey, account.ApiSecret));
        Assert.DoesNotContain("s3cr-et_x", account.ToString());
    }

    [Theory]
    [InlineData("https://1234:abcd@cloud")]
    [InlineData("cloudinary://1234@cloud")]
    [InlineData("khong-phai-url")]
    public void Malformed_Url_Is_Rejected_Without_Echoing_It(string url)
    {
        var error = Assert.Throws<InvalidOperationException>(() => CloudinaryAccount.Parse(url));

        Assert.DoesNotContain(url, error.Message);
    }

    [Fact]
    public void Malformed_Setting_Warns_And_Falls_Back_To_The_Database()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["CLOUDINARY_URL"] = "cloudinary://thieu-khoa@cloud" }).Build();
        var warnings = new List<string>();

        Assert.Null(CloudinaryAccount.FromOrNull(configuration, warnings.Add));
        Assert.DoesNotContain("thieu-khoa", Assert.Single(warnings));
    }

    [Fact]
    public void No_Configuration_Means_Files_Stay_In_The_Database()
    {
        var account = CloudinaryAccount.From(new ConfigurationBuilder().Build());

        Assert.Null(account);
        Assert.False(Store(new Recorder(), account).Enabled);
    }

    [Fact]
    public async Task Upload_Is_Signed_Private_And_Never_Sends_The_Secret()
    {
        var recorder = new Recorder();

        await Store(recorder, Account).UploadAsync("pace/groups/a/b.pdf", [9, 9], "SRS.pdf", default);

        Assert.Equal("https://api.cloudinary.com/v1_1/demo-cloud/raw/upload", recorder.Address!.ToString());
        Assert.Contains("authenticated", recorder.Body);
        Assert.Contains("pace/groups/a/b.pdf", recorder.Body);
        Assert.Contains("signature", recorder.Body);
        Assert.DoesNotContain("abcd", recorder.Body);
    }

    [Fact]
    public async Task Download_Uses_The_Signed_Api_Address()
    {
        var recorder = new Recorder();

        var bytes = await Store(recorder, Account).DownloadAsync("pace/groups/a/b.pdf", default);

        Assert.Equal([1, 2, 3], bytes);
        Assert.Equal("/v1_1/demo-cloud/raw/download", recorder.Address!.AbsolutePath);
        Assert.Contains("type=authenticated", recorder.Address.Query);
        Assert.Contains("public_id=pace%2Fgroups%2Fa%2Fb.pdf", recorder.Address.Query);
        Assert.DoesNotContain("abcd", recorder.Address.Query);
    }

    [Fact]
    public async Task Rejected_Request_Becomes_A_Service_Error()
    {
        var store = Store(new Recorder(HttpStatusCode.Unauthorized), Account);

        await Assert.ThrowsAsync<ExternalServiceException>(() => store.DeleteAsync("pace/groups/a/b.pdf", default));
    }

    [Fact]
    public async Task Delete_That_Removes_Nothing_Is_An_Error()
    {
        // Máy chủ giả trả nội dung không phải {"result":"ok"}.
        var store = Store(new Recorder(), Account);

        await Assert.ThrowsAsync<ExternalServiceException>(() => store.DeleteAsync("pace/groups/a/b.pdf", default));
    }

    [Fact]
    public void Storage_Key_Ignores_The_Uploaded_Name_Except_Its_Extension()
    {
        var group = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");

        Assert.Equal("pace/groups/11111111111111111111111111111111/22222222222222222222222222222222.pdf",
            SubmissionRules.StorageKey(group, id, "Bản đặc tả & ghi chú.PDF"));
    }
}
