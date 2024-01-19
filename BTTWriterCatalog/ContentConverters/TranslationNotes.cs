using BTTWriterCatalog.Helpers;
using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.OutputFormats;
using Markdig;
using Markdig.Syntax;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;
using PipelineCommon.Helpers.MarkdigExtensions;
using PipelineCommon.Models.ResourceContainer;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BTTWriterCatalog.ContentConverters
{
    public static class TranslationNotes
    {
        /// <summary>
        /// Generate translationNotes source files for BTTWriter from a project
        /// </summary>
        /// <param name="fileSystem">A ZipFileSystem holding the data</param>
        /// <param name="basePath">A base path inside of the zip file holding the information</param>
        /// <param name="outputPath">The directory to output the resulting files</param>
        /// <param name="container">Resource Container for all of the project metadata</param>
        /// <param name="chunks">Chunking information to use to split up the notes</param>
        /// <param name="log">An instance of ILogger to log warnings and information</param>
        /// <returns>A list of all of the books successfully processed</returns>
        /// <remarks>The json file this writes out is a notes broken up by chapter and then chapter chunk</remarks>
        public static async Task<List<string>> ConvertAsync(IZipFileSystem fileSystem, string basePath, string outputPath, ResourceContainer container, Dictionary<string,Dictionary<int,List<VerseChunk>>> chunks, ILogger log)
        {
            MarkdownPipeline markdownPipeline = new MarkdownPipelineBuilder().Use(new RCLinkExtension(new RCLinkOptions() { RenderAsBTTWriterLinks = true })).Build();
            var files = ConversionUtils.LoadScriptureMarkdownFiles(fileSystem, basePath, container, markdownPipeline);
            var convertedBooks = new List<string>();
            var writingTasks = new List<Task>();
            foreach (var book in files)
            {
                var bookOutput = new List<TranslationNoteChunk>();
                // If we don't have chunks for this then skip
                if (!chunks.ContainsKey(book.Key.ToUpper()) || chunks[book.Key.ToUpper()].Count == 0)
                {
                    log.LogWarning("Missing chunks for {book}", book.Key);
                    continue;
                }
                convertedBooks.Add(book.Key);

                var maxChapterNumberChars = book.Value.Max(i => i.ChapterNumber).ToString().Length;
                foreach (var chapter in book.Value)
                {
                    var maxVerseNumberChars = chapter.Verses.Max(v => v.VerseNumber).ToString().Length;
                    var verseChunks = chunks[book.Key.ToUpper()][chapter.ChapterNumber];
                    foreach(var chunk in verseChunks)
                    {
                        var currentChunk = new TranslationNoteChunk() { 
                            Id = $"{chapter.ChapterNumber.ToString().PadLeft(maxChapterNumberChars,'0')}-{chunk.StartingVerse.ToString().PadLeft(maxVerseNumberChars,'0')}" 
                        };
                        var content = new List<(string title, MarkdownDocument content)>();
                        if (chunk.EndingVerse == 0)
                        {
                            foreach(var i in chapter.Verses.Where(i => i.VerseNumber >= chunk.StartingVerse))
                            {
                                content.AddRange(i.Content);
                            }
                        }
                        foreach(var i in chapter.Verses.Where(i => i.VerseNumber >= chunk.StartingVerse && i.VerseNumber < chunk.EndingVerse))
                        {
                            content.AddRange(i.Content);
                        }

                        currentChunk.Notes.AddRange(content.Select(c => new TranslationNoteEntry() { Reference = c.title, Text = ConversionUtils.RenderMarkdownToPlainText(c.content, markdownPipeline).Trim() }));
                        bookOutput.Add(currentChunk);
                    }
                }
                string bookDir = Path.Join(outputPath,book.Key);
                if (!Directory.Exists(bookDir))
                {
                    Directory.CreateDirectory(bookDir);
                }
                writingTasks.Add(File.WriteAllTextAsync(Path.Join(bookDir, "notes.json"), JsonSerializer.Serialize(bookOutput, CatalogJsonContext.Default.ListTranslationNoteChunk)));
            }
            await Task.WhenAll(writingTasks);
            return convertedBooks;
        }
    }
}
