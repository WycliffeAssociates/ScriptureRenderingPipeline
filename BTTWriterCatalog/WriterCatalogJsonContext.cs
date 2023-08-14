using System.Collections.Generic;
using System.Text.Json.Serialization;
using BTTWriterCatalog.Models.WriterCatalog;

namespace BTTWriterCatalog;

[JsonSerializable(typeof(List<CatalogResource>))]
[JsonSerializable(typeof(List<CatalogProject>))]
[JsonSerializable(typeof(List<CatalogBook>))]
internal partial class WriterCatalogJsonContext: JsonSerializerContext
{
    
}