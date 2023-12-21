namespace ScriptureRenderingPipelineWorker.Models;

public class OutputNavigation
{
    public string File { get; set; }
    public string Slug { get; set; }
    public string Label { get; set; }
    public List<OutputNavigation> Children { get; set; }

    public OutputNavigation()
    {
        Children = new List<OutputNavigation>();
    }
}