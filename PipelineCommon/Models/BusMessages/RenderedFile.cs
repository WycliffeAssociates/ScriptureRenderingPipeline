namespace PipelineCommon.Models.BusMessages;

public class RenderedFile
{
    public string Path { get; set; }
    public long Size { get; set; }
    public string FileType { get; set; }
    public string Hash { get; set; }
    public int? Chapter { get; set; }
    public string Book { get; set; }
    public string Slug { get; set; }
}