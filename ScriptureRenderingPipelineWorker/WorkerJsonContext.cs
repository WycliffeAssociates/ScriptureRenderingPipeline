using System.Text.Json.Serialization;
using PipelineCommon.Models.BusMessages;
using ScriptureRenderingPipelineWorker.Models;

namespace ScriptureRenderingPipelineWorker;

[JsonSerializable(typeof(WACSMessage))]
[JsonSerializable(typeof(OutputIndex))]
[JsonSerializable(typeof(DownloadIndex))]
[JsonSerializable(typeof(VerseCountingResult))]
[JsonSerializable(typeof(RenderingResultMessage))]
[JsonSerializable(typeof(BuildLog))]
[JsonSerializable(typeof(AppMeta))]
[JsonSerializable(typeof(Dictionary<string,string>))]
[JsonSerializable(typeof(MergeRequest))]
internal partial class WorkerJsonContext: JsonSerializerContext
{
    
}