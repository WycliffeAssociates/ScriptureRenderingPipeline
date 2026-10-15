namespace ScriptureRenderingPipelineWorker;

/// <summary>
/// The topics the worker sends results to. Each one gets a long lived sender registered in Program.cs,
/// which the triggers get from IAzureClientFactory&lt;ServiceBusSender&gt; by these names.
/// </summary>
public static class ServiceBusTopics
{
    public const string RepoRendered = "RepoRendered";
    public const string RepoAnalysisResult = "RepoAnalysisResult";
    public const string VerseCountingResult = "VerseCountingResult";

    public static readonly string[] All = [RepoRendered, RepoAnalysisResult, VerseCountingResult];
}
