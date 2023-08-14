using System.Collections.Generic;
using System.Text.Json.Serialization;
using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.BIELCatalog;
using BTTWriterCatalog.Models.DataModel;
using BTTWriterCatalog.Models.OutputFormats;
using BTTWriterCatalog.Models.UnfoldingWordCatalog;
using PipelineCommon.Models.Webhook;

namespace BTTWriterCatalog;

[JsonSerializable(typeof(WebhookEvent))]
[JsonSerializable(typeof(CatalogRoot))]
[JsonSerializable(typeof(UnfoldingWordCatalogRoot))]
[JsonSerializable(typeof(List<Door43Chunk>))]
[JsonSerializable(typeof(Dictionary<int,List<VerseChunk>>))]
[JsonSerializable(typeof(ScriptureResourceModel))]
[JsonSerializable(typeof(SupplimentalResourcesModel))]
[JsonSerializable(typeof(List<CatalogResource>))]
[JsonSerializable(typeof(ScriptureResource))]
[JsonSerializable(typeof(List<TranslationNoteChunk>))]
[JsonSerializable(typeof(List<TranslationQuestionChapter>))]
[JsonSerializable(typeof(List<TranslationWord>))]
[JsonSerializable(typeof(TranslationWordsCatalogRoot))]
internal partial class JSONContext: JsonSerializerContext
{
    
}