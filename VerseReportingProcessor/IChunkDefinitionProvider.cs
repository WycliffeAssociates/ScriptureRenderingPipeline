namespace VerseReportingProcessor;

/// <summary>
/// Gets how a book is split into chunks, so the source of chunk definitions can be swapped out
/// </summary>
public interface IChunkDefinitionProvider
{
    /// <summary>
    /// Gets the number of chunks in a book
    /// </summary>
    /// <param name="languageCode">The language, for languages that have their own chunks</param>
    /// <param name="bookId">The book abbreviation</param>
    /// <returns>The chunk count, or null when there is no chunk definition for the book</returns>
    Task<int?> GetExpectedChunkCountAsync(string? languageCode, string bookId);
}
