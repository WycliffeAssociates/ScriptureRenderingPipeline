using System.Text.Json.Serialization;
using PipelineCommon.Models.BusMessages;
using PipelineCommon.Models.Webhook;

namespace ScriptureRenderingPipeline;

[JsonSerializable(typeof(WebhookEvent))]
[JsonSerializable(typeof(WACSMessage))]
[JsonSerializable(typeof(MergeRequest))]
public partial class PipelineJsonContext: JsonSerializerContext
{
    
}