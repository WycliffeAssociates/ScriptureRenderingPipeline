using System.Collections.Generic;
using System.Text.Json.Serialization;
using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.BIELCatalog;
using BTTWriterCatalog.Models.DataModel;
using BTTWriterCatalog.Models.OutputFormats;
using BTTWriterCatalog.Models.UnfoldingWordCatalog;
using BTTWriterCatalog.Models.WriterCatalog;
using PipelineCommon.Models.Webhook;

namespace BTTWriterCatalog;

[JsonSerializable(typeof(WebhookEvent))]
[JsonSerializable(typeof(List<Door43Chunk>))]
[JsonSerializable(typeof(ScriptureResourceModel))]
[JsonSerializable(typeof(SupplimentalResourcesModel))]
[JsonSerializable(typeof(List<WriterCatalogProject>))]
[JsonSerializable(typeof(List<WriterCatalogResource>))]
[JsonSerializable(typeof(List<WriterCatalogBook>))]
[JsonSerializable(typeof(UnfoldingWordCatalogRoot))]
[JsonSerializable(typeof(CatalogRoot))]
[JsonSerializable(typeof(ScriptureResource))]
[JsonSerializable(typeof(List<TranslationNoteChunk>))]
[JsonSerializable(typeof(List<TranslationQuestionChapter>))]
[JsonSerializable(typeof(List<TranslationWord>))]
[JsonSerializable(typeof(TranslationWordsCatalogRoot))]
internal partial class CatalogJsonContext: JsonSerializerContext
{
    
}