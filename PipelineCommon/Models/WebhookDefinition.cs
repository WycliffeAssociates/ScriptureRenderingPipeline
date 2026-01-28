namespace PipelineCommon.Models;
/// <summary>
/// Represents a webhook definition for outgoing notifications.
/// </summary>
public class WebhookDefinition
{
    public string Url { get; set; } = string.Empty;
    public string MessageType { get; set; } = "WACSEvent";
    public string EventType { get; set; } = string.Empty;
}
