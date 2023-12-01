using PipelineCommon.Helpers;

namespace PipelineCommon.Models;

public class RepoIdentificationResult
{
    public string languageName { get; set; }
    public string resourceName { get; set; }
    public string languageCode { get; set; }
    public string languageDirection { get; set; }
    public RepoType repoType { get; set; }
    public bool isBTTWriterProject { get; set; }
    public ResourceContainer.ResourceContainer ResourceContainer { get; set; }
}