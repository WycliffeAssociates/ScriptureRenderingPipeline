namespace PipelineCommon.Models.BusMessages;

public class MergeResult
{
    public MergeResult(bool success, string message, string userTriggered)
    {
        Success = success;
        if (success)
        {
            MergedUrl = message;
        }
        else
        {
            Message = message;
        }
        UserTriggered = userTriggered;
    }
    public bool Success { get; set; }
    public string Message { get; set; }
    public string UserTriggered { get; set; }
    public string MergedUrl { get; set; }
}