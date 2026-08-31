using System.Collections.Generic;
using System.Text.Json;
using BTTWriterCatalog.Models;
using Microsoft.Extensions.Logging;

namespace BTTWriterCatalog.Helpers
{
    /// <summary>
    /// Reads the chunks.json from the root of a repository
    /// </summary>
    public static class RepositoryChunks
    {
        /// <summary>
        /// Options for reading a repository's chunks.json, which stores chunks as [startingVerse, endingVerse] pairs
        /// </summary>
        /// <remarks>Cached because System.Text.Json builds and caches metadata per options instance</remarks>
        private static readonly JsonSerializerOptions ChunkJsonOptions =
            new JsonSerializerOptions() { Converters = { new VerseChunkArrayConverter() } };

        /// <summary>
        /// Parse the contents of a repository's chunks.json
        /// </summary>
        /// <param name="content">The contents of the file</param>
        /// <param name="log">An instance of ILogger to warn about duplicated books</param>
        /// <returns>Chunks in our internal format keyed by upper case book abbreviation</returns>
        /// <remarks>
        /// Book abbreviations are matched without regard to case. If a book somehow shows up more than
        /// once then the last one wins, since dropping the whole repository over it would be worse.
        /// </remarks>
        public static Dictionary<string, Dictionary<int, List<VerseChunk>>> Parse(string content, ILogger log)
        {
            Dictionary<string, Dictionary<int, List<VerseChunk>>> parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<int, List<VerseChunk>>>>(content, ChunkJsonOptions);
            }
            catch (JsonException ex)
            {
                // The path is where in the file the problem is, which is the only way an author can find it
                var location = string.IsNullOrEmpty(ex.Path) || ex.Path == "$" ? string.Empty : $" at {ex.Path}";
                throw new JsonException($"Unable to read chunks.json{location}: {ex.Message}", ex);
            }

            var output = new Dictionary<string, Dictionary<int, List<VerseChunk>>>(parsed?.Count ?? 0);
            foreach (var (book, chapters) in parsed ?? new Dictionary<string, Dictionary<int, List<VerseChunk>>>())
            {
                var bookId = book.ToUpper();
                if (!output.TryAdd(bookId, chapters))
                {
                    log.LogWarning("{Book} shows up more than once in chunks.json, only the last one will be used", bookId);
                    output[bookId] = chapters;
                }
            }
            return output;
        }
    }
}
