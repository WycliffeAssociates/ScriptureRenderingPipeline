using System.Collections.Generic;
using System.Text.Json.Serialization;
using PipelineCommon.Models.Webhook;
using ScriptureRenderingPipeline.Models;

namespace ScriptureRenderingPipeline;

/// <summary>
/// This class is populated with type information at compile time so that reflection isn't needed at runtime
/// </summary>
[JsonSerializable(typeof(BuildLog))]
[JsonSerializable(typeof(OutputIndex))]
[JsonSerializable(typeof(OutputBook))]
[JsonSerializable(typeof(OutputChapters))]
[JsonSerializable(typeof(WebhookEvent))]
[JsonSerializable(typeof(AppMeta))]
[JsonSerializable(typeof(DownloadIndex))]
[JsonSerializable(typeof(Dictionary<string,string>))]
internal partial class JSONContext: JsonSerializerContext
{
}