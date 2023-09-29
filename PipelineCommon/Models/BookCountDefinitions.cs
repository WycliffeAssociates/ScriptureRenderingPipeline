using System.Collections.Generic;

namespace PipelineCommon.Models;

public  class BookCountDefinitions
{
    public int ExpectedChapters { get; set; }
    public Dictionary<int, int> ExpectedChapterCounts { get; set; } = new Dictionary<int, int>();
}