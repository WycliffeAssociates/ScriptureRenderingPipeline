using System.Collections.Generic;

namespace PipelineCommon.Models;

public class CountDefinitions
{
    public Dictionary<string, BookCountDefinitions> Books { get; set; } = new();
}