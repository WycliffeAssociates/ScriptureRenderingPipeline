using System.Collections.Generic;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;

namespace PipelineCommon.Models;

public class RepoIdentificationResult
{
    public string languageName { get; set; }
    public string resourceName { get; set; }
    public string languageCode { get; set; }
    public string languageDirection { get; set; }
    public RepoType repoType { get; set; }
    public ResourceContainer.ResourceContainer ResourceContainer { get; set; }
    public RepoFormat RepoFormat { get; set; }
    /// <summary>
    /// The book in the repo, or null when there isn't exactly one. Falls back to the repo name.
    /// </summary>
    public string? BookId { get; set; }
    /// <summary>
    /// The resource identifier, such as <c>ulb</c>. Falls back to the repo name.
    /// </summary>
    public string? ResourceId { get; set; }
    /// <summary>
    /// Finished chunk ids from a BTT Writer manifest. Null for other formats.
    /// </summary>
    public IEnumerable<string>? FinishedChunks { get; set; }
}
