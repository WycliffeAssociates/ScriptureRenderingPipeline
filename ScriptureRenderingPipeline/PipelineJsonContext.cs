using System.Text.Json.Serialization;
using PipelineCommon.Models.BusMessages;
using PipelineCommon.Models.Webhook;

namespace ScriptureRenderingPipeline;

[JsonSerializable(typeof(WebhookEvent))]
[JsonSerializable(typeof(WACSMessage))]
public partial class PipelineJsonContext: JsonSerializerContext
{
    
}