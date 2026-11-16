using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Security;
using PACE.Infrastructure.Analysis;
using Xunit;

namespace PACE.Tests;

public class SecretRedactionTests
{
    // Các "bí mật" giả được ghép từ nhiều mảnh để file test này không giống token thật với công cụ quét secret.
    private static readonly string GitHubToken = "gh" + "p_" + new string('a', 36);
    private static readonly string AwsKey = "AK" + "IA" + "ABCDEFGHIJKLMNOP";
    private static readonly string LlmKey = "sk" + "-" + "proj1234567890abcdefghij";
    private static readonly string GoogleKey = "AI" + "za" + "Sy" + new string('b', 33);
    private static readonly string SlackToken = "xo" + "xb-" + "1234567890-abcdefghij";
    private static readonly string Jwt = "ey" + "J" + "hbGciOiJIUzI1NiJ9" + "." + "ey" + "J" + "zdWIiOiIxMjM0NTY3ODkwIn0" + "." + "SflKxwRJSMeKKF2QT4fw";
    private static readonly string PrivateKey =
        "-----BEGIN RSA " + "PRIVATE KEY-----\nMIIEowIBAAKCAQEA1234567890\nabcdefghijklmnop\n-----END RSA " + "PRIVATE KEY-----";

    private static void AssertRedacted(string code, string secret, string expectedRule)
    {
        var result = SecretRedactor.Redact(code);

        Assert.DoesNotContain(secret, result.Content);
        Assert.Contains(SecretRedactor.Placeholder, result.Content);
        Assert.Contains(result.Hits, h => h.Rule == expectedRule);
    }

    [Fact]
    public void Redacts_KnownTokenFormats()
    {
        AssertRedacted($"var t = \"{GitHubToken}\";", GitHubToken, "github-token");
        AssertRedacted($"var k = \"{AwsKey}\";", AwsKey, "aws-access-key");
        AssertRedacted($"client.Key = \"{LlmKey}\";", LlmKey, "llm-api-key");
        AssertRedacted($"var g = \"{GoogleKey}\";", GoogleKey, "google-api-key");
        AssertRedacted($"var s = \"{SlackToken}\";", SlackToken, "slack-token");
        AssertRedacted($"var j = \"{Jwt}\";", Jwt, "jwt");
    }

    [Fact]
    public void Redacts_PrivateKeyBlock_AcrossLines()
    {
        var result = SecretRedactor.Redact($"var pem = @\"{PrivateKey}\";\nvar next = 1;");

        Assert.DoesNotContain("MIIEowIBAAKCAQEA", result.Content);
        Assert.Contains("var next = 1;", result.Content);
        Assert.Contains(result.Hits, h => h.Rule == "private-key");
    }

    [Fact]
    public void Redacts_ConnectionStringPassword_ButKeepsTheRestOfTheString()
    {
        var result = SecretRedactor.Redact("\"Host=db.example.com;Database=pace;Username=app;Password=Sup3rS3cret!;Timeout=15\"");

        Assert.DoesNotContain("Sup3rS3cret", result.Content);
        Assert.Contains("Host=db.example.com;Database=pace;Username=app;Password=[REDACTED];Timeout=15", result.Content);
    }

    [Fact]
    public void Redacts_UrlCredentials_AndBearerTokens()
    {
        var result = SecretRedactor.Redact("var u = \"https://deploy:hunter2pass@git.example.com/repo.git\"; var h = \"Bearer abcdefghijklmnopqrstuvwxyz123456\";");

        Assert.DoesNotContain("hunter2pass", result.Content);
        Assert.DoesNotContain("abcdefghijklmnopqrstuvwxyz123456", result.Content);
        Assert.Contains("https://deploy:[REDACTED]@git.example.com/repo.git", result.Content);
    }

    [Theory]
    [InlineData("private const string ApiKey = \"live_abcdef123456\";", "live_abcdef123456")]
    [InlineData("const config = { password: 'correcthorse99' };", "correcthorse99")]
    [InlineData("\"client_secret\": \"zx81-qpw0-lmn7\"", "zx81-qpw0-lmn7")]
    [InlineData("String token = \"abcd1234efgh5678\";", "abcd1234efgh5678")]
    public void Redacts_SecretAssignments_KeepingTheKeyName(string code, string secret)
    {
        var result = SecretRedactor.Redact(code);

        Assert.DoesNotContain(secret, result.Content);
        Assert.Contains(SecretRedactor.Placeholder, result.Content);
    }

    [Theory]
    [InlineData("var password = request.Password;")]
    [InlineData("var token = await GetTokenAsync(user);")]
    [InlineData("password = \"${DB_PASSWORD}\";")]
    [InlineData("apiKey = \"<your-api-key-here>\";")]
    [InlineData("password = \"xxxxxxxx\";")]
    [InlineData("var x = Build(); var password = request.Password; Save(password);")]
    [InlineData("ErrorMessage = \"Password must contain a number\";")]
    [InlineData("var PasswordError = \"incorrectpassword\";")]
    [InlineData("if (user.PasswordHash == hash) return Ok();")]
    public void LeavesOrdinaryCodeAndPlaceholdersUntouched(string code)
    {
        var result = SecretRedactor.Redact(code);

        Assert.Equal(code, result.Content);
        Assert.Empty(result.Hits);
    }

    [Fact]
    public void Redaction_IsIdempotent()
    {
        var once = SecretRedactor.Redact($"var a = \"{GitHubToken}\"; password = \"correcthorse99\";");
        var twice = SecretRedactor.Redact(once.Content);

        Assert.Equal(once.Content, twice.Content);
        Assert.Empty(twice.Hits);
    }

    [Fact]
    public void Hits_RecordLineNumbers_AndNeverTheSecretValue()
    {
        var result = SecretRedactor.Redact($"line one\nline two\nvar t = \"{GitHubToken}\";\n");

        var hit = Assert.Single(result.Hits);
        Assert.Equal(3, hit.Line);
        Assert.DoesNotContain("aaaa", hit.ToString());
    }

    // ---- Tích hợp với RepositoryScanner ----

    private static string MakeRepo(Action<string> populate)
    {
        var root = Path.Combine(Path.GetTempPath(), "pace_redact_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        populate(root);
        return root;
    }

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void Scanner_RedactsSecretsInAcceptedFiles_AndLogsThem()
    {
        var root = MakeRepo(r => Write(r, "Services/MailService.cs",
            $"class MailService {{ private const string Key = \"{LlmKey}\"; void Send() {{ }} }}"));
        try
        {
            var scan = new RepositoryScanner().Scan(root, default);

            var file = Assert.Single(scan.CSharpFiles);
            Assert.DoesNotContain(LlmKey, file.Content);
            Assert.Contains(SecretRedactor.Placeholder, file.Content);
            Assert.Contains("void Send()", file.Content);
            Assert.Equal(1, scan.AcceptedCount);
            var finding = Assert.Single(scan.Redactions);
            Assert.Equal("Services/MailService.cs", finding.RelativePath);
            Assert.Contains(scan.RedactionWarnings(), w => w.Contains("1 thông tin nhạy cảm"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Scanner_IgnoresSensitiveFilesAndBuildFolders_AndLogsWhy()
    {
        var root = MakeRepo(r =>
        {
            Write(r, "Program.cs", "class Program { }");
            Write(r, ".env", "DB_PASSWORD=supersecretvalue");
            Write(r, "appsettings.Production.json", "{ \"Password\": \"supersecretvalue\" }");
            Write(r, "certs/server.pem", "-----BEGIN CERTIFICATE-----");
            Write(r, "node_modules/lib/index.js", "module.exports = 1;");
            Write(r, "bin/Debug/Generated.cs", "class Generated { }");
        });
        try
        {
            var scan = new RepositoryScanner().Scan(root, default);

            Assert.Equal(["Program.cs"], scan.CSharpFiles.Select(f => f.RelativePath).ToArray());
            var ignored = scan.FileLog.Where(e => e.Status == ScanFileStatus.Ignored).Select(e => e.RelativePath).ToHashSet();
            Assert.Contains(".env", ignored);
            Assert.Contains("appsettings.Production.json", ignored);
            Assert.Contains("certs/server.pem", ignored);
            Assert.Contains("node_modules", ignored);
            Assert.Contains("bin", ignored);
            Assert.All(scan.FileLog.Where(e => e.Status == ScanFileStatus.Ignored), e => Assert.False(string.IsNullOrWhiteSpace(e.Reason)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Scanner_IgnoresOversizedBinaryAndEmptyFiles_WithReasons()
    {
        var root = MakeRepo(r =>
        {
            Write(r, "Big.cs", "// " + new string('x', 600 * 1024));
            Write(r, "Binary.cs", "class A { }\0\0\0");
            Write(r, "Empty.cs", "");
            Write(r, "Ok.cs", "class Ok { }");
        });
        try
        {
            var scan = new RepositoryScanner().Scan(root, default);

            Assert.Equal(["Ok.cs"], scan.CSharpFiles.Select(f => f.RelativePath).ToArray());
            Assert.Equal(1, scan.AcceptedCount);
            Assert.Equal(3, scan.IgnoredCount);
            Assert.Contains(scan.FileLog, e => e.RelativePath == "Big.cs" && e.Reason.Contains("KB"));
            Assert.Contains(scan.FileLog, e => e.RelativePath == "Binary.cs" && e.Reason.Contains("nhị phân"));
            Assert.Contains(scan.FileLog, e => e.RelativePath == "Empty.cs" && e.Reason.Contains("rỗng"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Scanner_CountsAFrontendBackendFileOnce()
    {
        var root = MakeRepo(r =>
        {
            Write(r, "package.json", "{ \"dependencies\": { \"express\": \"4.0.0\" } }");
            Write(r, "src/view.tsx", "export const V = () => null;");
        });
        try
        {
            var scan = new RepositoryScanner().Scan(root, default);

            Assert.Single(scan.JsTsFiles);
            Assert.Single(scan.FrontendFiles);
            Assert.Equal(1, scan.AcceptedCount);
        }
        finally { Directory.Delete(root, true); }
    }
}
