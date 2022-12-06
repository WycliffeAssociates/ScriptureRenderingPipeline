using System.Collections.Generic;

namespace ScriptureRenderingPipeline.Models;

public class OutputNavigation
{
    public string Path { get; set; }
    public string Label { get; set; }
    public List<OutputNavigation> Children { get; set; }
}