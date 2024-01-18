namespace PipelineCommon.Models.BusMessages;

public class RenderedFile
{
    public string Path { get; set; }
    public long Size { get; set; }
    public string FileType { get; set; }
    public string Hash { get; set; }
}