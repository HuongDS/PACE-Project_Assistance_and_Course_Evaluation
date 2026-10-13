namespace PACE.BLL.Analysis;

public class AnalysisOptions
{
    public const string SectionName = "Analysis";

    public string? CloneRoot { get; set; }
    public string GitExecutable { get; set; } = "git";
    public int CloneTimeoutSeconds { get; set; } = 180;
    public string? GitHubToken { get; set; }
    public int MaxFlowDepth { get; set; } = 12;
    public int MaxStepsPerFlow { get; set; } = 250;
}
