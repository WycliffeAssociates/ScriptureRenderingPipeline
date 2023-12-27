using System.Collections.Generic;
using System.Text.Json.Serialization;
using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.BIELCatalog;
using BTTWriterCatalog.Models.DataModel;
using BTTWriterCatalog.Models.OutputFormats;
using BTTWriterCatalog.Models.UnfoldingWordCatalog;
using BTTWriterCatalog.Models.WriterCatalog;
using CatalogResource = BTTWriterCatalog.Models.WriterCatalog.CatalogResource;

namespace BTTWriterCatalog;

[JsonSerializable(typeof(Webhook))]
[JsonSerializable(typeof(List<Door43Chunk>))]
[JsonSerializable(typeof(ScriptureResourceModel))]
[JsonSerializable(typeof(SupplimentalResourcesModel))]
[JsonSerializable(typeof(List<CatalogProject>))]
[JsonSerializable(typeof(List<CatalogResource>))]
[JsonSerializable(typeof(List<CatalogBook>))]
[JsonSerializable(typeof(UnfoldingWordCatalogRoot))]
[JsonSerializable(typeof(CatalogRoot))]
[JsonSerializable(typeof(ScriptureResource))]
[JsonSerializable(typeof(List<TranslationNoteChunk>))]
[JsonSerializable(typeof(List<TranslationQuestionChapter>))]
[JsonSerializable(typeof(List<TranslationWord>))]
[JsonSerializable(typeof(TranslationWordsCatalogRoot))]
public partial class CatalogJsonContext: JsonSerializerContext
{
    
}