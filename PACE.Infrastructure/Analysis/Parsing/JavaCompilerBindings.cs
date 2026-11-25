using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using PACE.Application.Analysis.Models;

namespace PACE.Infrastructure.Analysis.Parsing;

internal sealed class JavaSourcePositions(SourceFile source)
{
    public SourceFile Source { get; } = source;
    public Dictionary<int, ParsedMethod> Methods { get; } = [];
    public Dictionary<ParsedMethod, SpringMethodMapping> SpringMethods { get; } = [];
    public Dictionary<(int Start, int End), ParsedCall> Calls { get; } = [];
}

internal static class JavaCompilerBindings
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    public static void Apply(IReadOnlyList<JavaSourcePositions> sources, ParseResult result, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            var output = RunAsync(sources, timeout.Token).GetAwaiter().GetResult();
            ApplyOutput(output, sources, result);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            result.Warnings.Add("Phân giải lời gọi Java bằng javac vượt quá giới hạn 45 giây.");
        }
        catch (Win32Exception)
        {
            result.Warnings.Add("Không khởi động được java. Cần JDK 17 trở lên để phân giải lời gọi Java.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            result.Warnings.Add($"Phân giải lời gọi Java chưa hoàn tất: {ex.Message}");
        }

        var unresolved = sources.Sum(file => file.Calls.Values.Count(call => call.JavaBinding is null));
        if (unresolved > 0)
            result.Warnings.Add($"Java: {unresolved} lời gọi chưa phân giải được khai báo bằng javac.");
    }

    private static async Task<string> RunAsync(IReadOnlyList<JavaSourcePositions> sources, CancellationToken ct)
    {
        var work = Directory.CreateTempSubdirectory("pace-java-");
        try
        {
            var input = work.CreateSubdirectory("sources");
            var emptyClasspath = work.CreateSubdirectory("classpath");
            for (var i = 0; i < sources.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var folder = input.CreateSubdirectory(i.ToString(CultureInfo.InvariantCulture));
                var name = Path.GetFileNameWithoutExtension(sources[i].Source.RelativePath) + ".java";
                await File.WriteAllTextAsync(Path.Combine(folder.FullName, name), sources[i].Source.Content, ct)
                    .ConfigureAwait(false);
            }

            var helperPath = Path.Combine(work.FullName, "PaceJavaSymbols.java");
            using (var source = typeof(JavaCompilerBindings).Assembly.GetManifestResourceStream("PaceJavaSymbols.java")!)
            using (var destination = File.Create(helperPath))
                await source.CopyToAsync(destination, ct).ConfigureAwait(false);

            var classpath = Environment.GetEnvironmentVariable("PACE_JAVA_CLASSPATH")
                ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => Path.GetFullPath(path));
            var start = new ProcessStartInfo("java")
            {
                WorkingDirectory = work.FullName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };
            start.Environment.Remove("JDK_JAVA_OPTIONS");
            start.Environment.Remove("JAVA_TOOL_OPTIONS");
            start.Environment.Remove("_JAVA_OPTIONS");
            start.Environment.Remove("CLASSPATH");
            foreach (var arg in new[] { "-Xmx512m", "-XX:ActiveProcessorCount=2", "-Dfile.encoding=UTF-8",
                         "--source", "17", helperPath, input.FullName,
                         classpath is null ? emptyClasspath.FullName : string.Join(Path.PathSeparator, classpath) })
                start.ArgumentList.Add(arg);

            using var process = Process.Start(start) ?? throw new IOException("Không khởi động được tiến trình Java.");
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.ReadToEndAsync(ct);
            try
            {
                await process.WaitForExitAsync(ct).ConfigureAwait(false);
                var output = await stdout.ConfigureAwait(false);
                await stderr.ConfigureAwait(false);
                if (process.ExitCode != 0)
                    throw new IOException($"Tiến trình javac kết thúc với mã {process.ExitCode}.");
                return output;
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private static void ApplyOutput(string output, IReadOnlyList<JavaSourcePositions> sources, ParseResult result)
    {
        var declarations = new List<(ParsedMethod Method, string Symbol, string[] Overrides)>();
        var bindings = new List<(ParsedCall Call, JavaCallBinding Binding)>();
        using var reader = new StringReader(output);
        var errors = -1;
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split('\t');
            if (parts.Length == 2 && parts[0] == "E" && int.TryParse(parts[1], out errors)) continue;
            if (parts.Length < 4 || !int.TryParse(parts[1], out var fileId) ||
                fileId < 0 || fileId >= sources.Count || !int.TryParse(parts[2], out var start) ||
                start < 0 || start > sources[fileId].Source.Content.Length)
                throw new IOException("Dữ liệu vị trí từ javac không hợp lệ.");

            var source = sources[fileId];
            if (parts[0] == "D")
            {
                if (source.Methods.TryGetValue(start, out var method))
                    declarations.Add((method, parts[3], parts[4..]));
            }
            else if (parts[0] == "C" && parts.Length >= 5 && int.TryParse(parts[3], out var end) &&
                     end > start && end <= source.Source.Content.Length)
            {
                if (parts[4].Length > 0 && source.Calls.TryGetValue((start, end), out var call))
                    bindings.Add((call, new JavaCallBinding(parts[4], parts[5..])));
            }
            else
            {
                throw new IOException("Dữ liệu symbol từ javac không hợp lệ.");
            }
        }

        if (errors < 0) throw new IOException("Javac chưa trả về báo cáo hoàn chỉnh.");
        foreach (var (method, symbol, overrides) in declarations)
        {
            method.JavaSymbol = symbol;
            method.JavaOverriddenSymbols = overrides;
        }
        foreach (var (call, binding) in bindings) call.JavaBinding = binding;
        if (errors > 0)
            result.Warnings.Add($"Javac báo {errors} lỗi trên source đầu vào; graph Java có thể thiếu cạnh.");
    }
}
