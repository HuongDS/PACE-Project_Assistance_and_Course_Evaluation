using System.Text.Json;
using PACE.BLL.Analysis.Models;

namespace PACE.BLL.Analysis.Parsing;

public sealed class RepositoryScan
{
    public List<string> Stacks { get; } = [];
    public List<SourceFile> CSharpFiles { get; } = [];
    public List<SourceFile> JavaFiles { get; } = [];
    public List<SourceFile> JsTsFiles { get; } = [];
    public List<string> Warnings { get; } = [];
    public int TotalFiles => CSharpFiles.Count + JavaFiles.Count + JsTsFiles.Count;
}

public static class RepositoryScanner
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
        "express", "@nestjs/core", "koa", "fastify", "@hapi/hapi", "restify", "mongoose", "sequelize",
        "typeorm", "prisma", "@prisma/client", "knex", "pg", "mysql2", "mongodb", "socket.io"
    };

    private static readonly HashSet<string> JsExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".mjs", ".cjs", ".jsx", ".ts", ".tsx"
    };

    public static RepositoryScan Scan(string root, CancellationToken ct)
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
                if (name.StartsWith('.') || SkipDirectories.Contains(name)) continue;
                if (TestProjectSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase))) continue;
                pending.Push(sub);
            }

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
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
                if (fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)) continue;
                AddFile(scan.CSharpFiles, root, file);
            }
            else if (ext.Equals(".java", StringComparison.OrdinalIgnoreCase))
            {
                AddFile(scan.JavaFiles, root, file);
            }
            else if (JsExtensions.Contains(ext))
            {
                if (IsIgnoredScript(fileName)) continue;
                if (NearestPackageIsBackend(Path.GetDirectoryName(file)!, root, packageJsonDirs)) AddFile(scan.JsTsFiles, root, file);
            }
        }

        if (hasDotNet || scan.CSharpFiles.Count > 0) scan.Stacks.Add(StackDotNet);
        if (hasSpring) scan.Stacks.Add(StackSpring);
        else if (hasJavaBuild || scan.JavaFiles.Count > 0) scan.Stacks.Add(StackJava);
        if (scan.JsTsFiles.Count > 0) scan.Stacks.Add(StackNode);

        return scan;
    }

    private static void AddFile(List<SourceFile> target, string root, string file)
    {
        var info = new FileInfo(file);
        if (info.Length > MaxFileBytes) return;
        var content = SafeRead(file);
        if (string.IsNullOrEmpty(content)) return;
        target.Add(new SourceFile(file, Path.GetRelativePath(root, file).Replace('\\', '/'), content));
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
