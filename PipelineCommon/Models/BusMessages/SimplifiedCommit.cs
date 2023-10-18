using System;

namespace PipelineCommon.Models.BusMessages;

public class SimplifiedCommit
{
    public string Hash { get; set; }
    public string Message { get; set; }
    public string Url { get; set; }
    public string Username { get; set; }
    public DateTime Timestamp { get; set; }
}