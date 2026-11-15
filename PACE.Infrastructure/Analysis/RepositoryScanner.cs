using PACE.Application.Analysis.Template;
using System.Text.Json;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Common;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis;
using PACE.Application.Analysis.Security;
using PACE.Application.Common.Exceptions;
using System.Text.RegularExpressions;

namespace PACE.Infrastructure.Analysis;

public sealed class RepositoryScanner : IRepositoryScanner
{
    public const string StackDotNet = ".NET";
    public const string StackSpring = "Spring Boot";
    public const string StackJava = "Java";
    public const string StackNode = "Node.js";

    private const long MaxFileBytes = 512 * 1024;

    private static readonly HashSet<string> SkipDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", "dist", "build", "out", "target", "coverage", "wwwroot",
        "Migrations", "migrations", "test", "tests", "__tests__", "__mocks__", "e2e",
        "vendor", "public", "static", "assets", "logs", "tmp", "temp"
    };

    private static readonly string[] TestProjectSuffixes = [".Tests", ".Test", ".UnitTests", ".IntegrationTests", ".Specs"];

    private static readonly HashSet<string> FrontendMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "react", "react-dom", "vue", "@vue/cli-service", "@angular/core", "svelte", "next", "nuxt",
        "gatsby", "vite", "@sveltejs/kit", "solid-js", "preact", "react-native", "expo"
    };

    private static readonly HashSet<string> BackendMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "hono", "@hono/node-server", "express", "@nestjs/core", "koa", "fastify", "@hapi/hapi", "restify", "mongoose", "sequelize",
        "typeorm", "prisma", "@prisma/client", "knex", "pg", "mysql2", "mongodb", "socket.io"
    };

    private static readonly HashSet<string> JsExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".mjs", ".cjs", ".jsx", ".ts", ".tsx"
    };

    public async Task<string?> ReadBusinessTemplateAsync(IEnumerable<string> roots, CancellationToken ct)
    {
        foreach (var root in roots.Distinct())
        {
            var path = FindBusinessTemplate(root);
            if (path is not null) return await File.ReadAllTextAsync(path, ct);
        }
        return null;
    }

    private static string? FindBusinessTemplate(string root)
    {
        if (!Directory.Exists(root)) return null;
        var direct = Path.Combine(root, BusinessTemplateLoader.FileName);
        if (File.Exists(direct)) return direct;

        var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 8, MatchCasing = MatchCasing.CaseInsensitive, IgnoreInaccessible = true };
        return Directory.EnumerateFiles(root, BusinessTemplateLoader.FileName, options)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Count(ch => ch == Path.DirectorySeparatorChar))
            .FirstOrDefault();
    }

    public RepositoryScan Scan(string root, CancellationToken ct)
    {
        root = Path.GetFullPath(root);
        var scan = new RepositoryScan();
        var allFiles = new List<string>();
        var packageJsonDirs = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        bool hasDotNet = false, hasJavaBuild = false, hasSpring = false;

        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = pending.Pop();

            IEnumerable<string> subDirs;
            IEnumerable<string> files;
            try
            {
                subDirs = Directory.EnumerateDirectories(dir);
                files = Directory.EnumerateFiles(dir);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var sub in subDirs)
            {
                var name = Path.GetFileName(sub);
                if (name.StartsWith('.')) { scan.Log(RelativeTo(root, sub), ScanFileStatus.Ignored, "thư mục ẩn"); continue; }
                if (SkipDirectories.Contains(name)) { scan.Log(RelativeTo(root, sub), ScanFileStatus.Ignored, "thư mục build, phụ thuộc hoặc test"); continue; }
                if (TestProjectSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase))) { scan.Log(RelativeTo(root, sub), ScanFileStatus.Ignored, "dự án test"); continue; }
                pending.Push(sub);
            }

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                if (IsSensitiveFile(fileName))
                {
                    scan.Log(RelativeTo(root, file), ScanFileStatus.Ignored, "file cấu hình hoặc khoá có thể chứa thông tin nhạy cảm");
                    continue;
                }

                if (fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    hasDotNet = true;
                }
                else if (fileName is "pom.xml" or "build.gradle" or "build.gradle.kts")
                {
                    hasJavaBuild = true;
                    if (SafeRead(file)?.Contains("spring-boot", StringComparison.OrdinalIgnoreCase) == true) hasSpring = true;
                }
                else if (fileName == "package.json")
                {
                    packageJsonDirs[dir] = IsBackendPackage(file, scan.Warnings, root);
                }

                allFiles.Add(file);
            }
        }

        foreach (var file in allFiles)
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(file);
            var fileName = Path.GetFileName(file);

            if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                if (fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                {
                    scan.Log(RelativeTo(root, file), ScanFileStatus.Ignored, "file sinh tự động");
                    continue;
                }
                AddFile(scan, root, file, scan.CSharpFiles);
            }
            else if (ext.Equals(".java", StringComparison.OrdinalIgnoreCase))
            {
                AddFile(scan, root, file, scan.JavaFiles);
            }
            else if (JsExtensions.Contains(ext))
            {
                if (IsIgnoredScript(fileName))
                {
                    scan.Log(RelativeTo(root, file), ScanFileStatus.Ignored, "script test, cấu hình hoặc file đã minify");
                    continue;
                }
                if (NearestPackageIsBackend(Path.GetDirectoryName(file)!, root, packageJsonDirs))
                {
                    if (ext.ToLowerInvariant() is ".jsx" or ".tsx") AddFile(scan, root, file, scan.JsTsFiles, scan.FrontendFiles);
                    else AddFile(scan, root, file, scan.JsTsFiles);
                }
                else
                    AddFile(scan, root, file, scan.FrontendFiles);
            }
            else if (ext.ToLowerInvariant() is ".html" or ".vue" or ".svelte")
            {
                AddFile(scan, root, file, scan.FrontendFiles);
            }
        }

        if (hasDotNet || scan.CSharpFiles.Count > 0) scan.Stacks.Add(StackDotNet);
        if (hasSpring) scan.Stacks.Add(StackSpring);
        else if (hasJavaBuild || scan.JavaFiles.Count > 0) scan.Stacks.Add(StackJava);
        if (scan.JsTsFiles.Count > 0) scan.Stacks.Add(StackNode);

        if (scan.FrontendFiles.Count > 0) scan.Stacks.Add("Web frontend");

        return scan;
    }

    private static readonly string[] SensitiveExactNames =
    [
        ".env", ".npmrc", ".netrc", ".pgpass", "secrets.json", "secret.json", "credentials", "credentials.json",
        "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519", "launchSettings.json"
    ];

    private static readonly string[] SensitiveExtensions =
        [".pem", ".key", ".pfx", ".p12", ".jks", ".keystore", ".kdbx", ".tfvars"];

    /// <summary>File thường chứa mật khẩu/khoá: không đọc, không lưu, không gửi sang AI.</summary>
    internal static bool IsSensitiveFile(string fileName)
    {
        if (SensitiveExactNames.Contains(fileName, StringComparer.OrdinalIgnoreCase)) return true;
        if (fileName.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)) return true;
        if (SensitiveExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase)) return true;
        if (fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return true;
        if (fileName.StartsWith("application", StringComparison.OrdinalIgnoreCase)
            && (fileName.EndsWith(".properties", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))) return true;
        return false;
    }

    private static string RelativeTo(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');

    /// <summary>
    /// Đọc, kiểm tra và che thông tin nhạy cảm của một file rồi thêm vào các danh sách đích.
    /// Mọi kết quả (nhận, bỏ qua, lỗi) đều được ghi vào log của lần quét. Nếu không che được thì bỏ file
    /// thay vì dùng bản chưa che.
    /// </summary>
    private static void AddFile(RepositoryScan scan, string root, string file, params List<SourceFile>[] targets)
    {
        var relative = RelativeTo(root, file);

        long length;
        try { length = new FileInfo(file).Length; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            scan.Log(relative, ScanFileStatus.Failed, "không đọc được thông tin file");
            return;
        }
        if (length > MaxFileBytes)
        {
            scan.Log(relative, ScanFileStatus.Ignored, $"vượt quá {MaxFileBytes / 1024} KB");
            return;
        }

        var content = SafeRead(file);
        if (content is null)
        {
            scan.Log(relative, ScanFileStatus.Failed, "không đọc được file");
            return;
        }
        if (content.Length == 0)
        {
            scan.Log(relative, ScanFileStatus.Ignored, "file rỗng");
            return;
        }
        if (content.Contains('\0'))
        {
            scan.Log(relative, ScanFileStatus.Ignored, "file nhị phân");
            return;
        }

        RedactionResult redacted;
        try
        {
            redacted = SecretRedactor.Redact(content);
        }
        catch (RegexMatchTimeoutException)
        {
            scan.Log(relative, ScanFileStatus.Failed, "quét thông tin nhạy cảm quá thời gian, đã bỏ file để an toàn");
            return;
        }

        foreach (var hit in redacted.Hits) scan.Redactions.Add(new RedactionFinding(relative, hit.Rule, hit.Line));
        scan.Log(relative, ScanFileStatus.Accepted, redacted.Hits.Count == 0 ? "" : $"đã che {redacted.Hits.Count} thông tin nhạy cảm");

        var source = new SourceFile(file, relative, redacted.Content);
        foreach (var target in targets) target.Add(source);
    }

    private static bool IsIgnoredScript(string fileName)
        => fileName.EndsWith(".d.ts", StringComparison.OrdinalIgnoreCase)
           || fileName.Contains(".min.", StringComparison.OrdinalIgnoreCase)
           || fileName.Contains(".test.", StringComparison.OrdinalIgnoreCase)
           || fileName.Contains(".spec.", StringComparison.OrdinalIgnoreCase)
           || fileName.EndsWith(".config.js", StringComparison.OrdinalIgnoreCase)
           || fileName.EndsWith(".config.ts", StringComparison.OrdinalIgnoreCase)
           || fileName.EndsWith(".config.mjs", StringComparison.OrdinalIgnoreCase);

    private static bool NearestPackageIsBackend(string dir, string root, Dictionary<string, bool> packageDirs)
    {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = Path.GetFullPath(dir);
        while (true)
        {
            if (packageDirs.TryGetValue(current, out var isBackend)) return isBackend;
            if (current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Equals(rootFull, StringComparison.OrdinalIgnoreCase)) return false;
            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent.Length < rootFull.Length) return false;
            current = parent;
        }
    }

    private static bool IsBackendPackage(string packageJsonPath, List<string> warnings, string root)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            var deps = new List<string>();
            foreach (var section in new[] { "dependencies", "devDependencies" })
            {
                if (doc.RootElement.TryGetProperty(section, out var obj) && obj.ValueKind == JsonValueKind.Object)
                    deps.AddRange(obj.EnumerateObject().Select(p => p.Name));
            }

            if (deps.Any(BackendMarkers.Contains)) return true;
            return !deps.Any(FrontendMarkers.Contains);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            warnings.Add($"Không đọc được {Path.GetRelativePath(root, packageJsonPath)}: {ex.Message}");
            return false;
        }
    }

    private static string? SafeRead(string file)
    {
        try
        {
            return File.ReadAllText(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
