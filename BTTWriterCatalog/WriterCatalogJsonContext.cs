using System.Collections.Generic;
using System.Text.Json.Serialization;
using BTTWriterCatalog.Models.WriterCatalog;

namespace BTTWriterCatalog;

/// <summary>
/// This class is populated with class information at compile time so that reflection isn't needed at runtime
/// This is different than JsonContext because there are name collisions between writer catalog and biel catalog
/// </summary>
[JsonSerializable(typeof(List<CatalogResource>))]
[JsonSerializable(typeof(List<CatalogProject>))]
[JsonSerializable(typeof(List<CatalogBook>))]
internal partial class WriterCatalogJsonContext: JsonSerializerContext
{
    
}