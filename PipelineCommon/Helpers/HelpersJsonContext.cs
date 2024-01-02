using System.Text.Json.Serialization;
using BTTWriterLib.Models;
using PipelineCommon.Models;

namespace PipelineCommon.Helpers;

[JsonSerializable(typeof(TranslationDatabaseLanguage[]))]
[JsonSerializable(typeof(BTTWriterManifest))]
[JsonSerializable(typeof(MinimalBTTWriterManifest))]
internal partial class HelpersJsonContext: JsonSerializerContext
{
    
}