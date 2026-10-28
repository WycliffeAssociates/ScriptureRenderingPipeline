using System.Diagnostics;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VerseReportingProcessor.Models;

namespace VerseReportingProcessor;

/// <summary>
/// Reads chunk definitions from the <c>chunks.json</c> files BTTWriterCatalog keeps in blob storage,
/// preferring a language-specific chunk file over the default one
/// </summary>
public class BlobChunkDefinitionProvider : IChunkDefinitionProvider
{
    private readonly IConfiguration _config;
    private readonly ILogger _log;
    private readonly IMemoryCache _cache;
    private readonly ActivitySource _activitySource = new(nameof(VerseCounterService));

    public BlobChunkDefinitionProvider(IConfiguration config, ILogger<BlobChunkDefinitionProvider> log, IMemoryCache cache)
    {
        _config = config;
        _log = log;
        _cache = cache;
    }

    public async Task<int?> GetExpectedChunkCountAsync(string? languageCode, string bookId)
    {
        using var activity = _activitySource.StartActivity();
        var book = bookId.ToLowerInvariant();
        var cacheKey = $"chunks:{languageCode}:{book}";
        if (_cache.TryGetValue(cacheKey, out int? cached))
        {
            return cached;
        }

        var client = new BlobContainerClient(_config.GetConnectionString("BlobStorage"), GetChunkContainerName());
        BlobClient? source = null;
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var languageSpecific = client.GetBlobClient($"{languageCode}/{book}/chunks.json");
            if (await languageSpecific.ExistsAsync())
            {
                source = languageSpecific;
            }
        }
        if (source == null)
        {
            var defaultSource = client.GetBlobClient($"default/{book}/chunks.json");
            if (await defaultSource.ExistsAsync())
            {
                source = defaultSource;
            }
        }

        int? output = null;
        if (source != null)
        {
            await using var stream = await source.OpenReadAsync();
            var chunks = await JsonSerializer.DeserializeAsync<List<ChunkDefinition>>(stream);
            output = chunks?.Count;
        }
        else
        {
            _log.LogWarning("No chunk definition found for {Language} {Book}", languageCode, book);
        }

        _cache.Set(cacheKey, output, TimeSpan.FromMinutes(30));
        return output;
    }

    private string GetChunkContainerName()
    {
        return _config.GetValue("BlobStorageChunkContainer", "chunkscontainer")!;
    }
}
