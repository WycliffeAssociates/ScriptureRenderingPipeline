using DotLiquid;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using ScriptureRenderingPipeline.Models;

namespace ScriptureRenderingPipeline;

public class RendererInput
{
    public string UserToRouteResourcesTo { get; set; } 
    public string BaseUrl { get; set; }
    public Template PrintTemplate { get; set; }
    public IZipFileSystem FileSystem { get; set; }
    public string BasePath { get; set; }
    public bool IsBTTWriterProject { get; set; }
    public string ResourceName { get; set; }
    public string LanguageName { get; set; }
    public string LanguageCode { get; set; }
    public string LanguageTextDirection { get; set; }
    public AppMeta AppsMeta { get; set; }
    public ResourceContainer ResourceContainer { get; set; }
    public string Title { get; set; }
    public string RepoUrl { get; set; }
}