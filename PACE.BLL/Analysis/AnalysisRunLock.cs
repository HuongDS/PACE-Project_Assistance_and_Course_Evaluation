using System.Collections.Concurrent;

namespace PACE.BLL.Analysis;

public static class AnalysisRunLock
{
    private static readonly ConcurrentDictionary<Guid, byte> Running = new();

    public static bool TryAcquire(Guid analysisRunId) => Running.TryAdd(analysisRunId, 0);

    public static void Release(Guid analysisRunId) => Running.TryRemove(analysisRunId, out _);

    public static bool IsRunning(Guid analysisRunId) => Running.ContainsKey(analysisRunId);
}
