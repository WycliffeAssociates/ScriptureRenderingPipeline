using BTTWriterCatalog.Models;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using USFMToolsSharp.Models.Markers;

namespace BTTWriterCatalog.Helpers
{
    public static class ConversionUtils
    {
        // This allows for an instance of cosmosdb to be created when needed while still allowing it to be static
        // Azure functions want you to do it this way so it can share the object between function executions
        private static Lazy<CosmosClient> lazyCosmosClient = new Lazy<CosmosClient>(InitializeCosmosClient);
        public static CosmosClient cosmosClient = lazyCosmosClient.Value;
        private static CosmosClient InitializeCosmosClient()
        {
            var databaseConnectionString = Environment.GetEnvironmentVariable("DBConnectionString");
            return new CosmosClient(databaseConnectionString);
        }

        /// <summary>
        /// Parse a markdown file into sections based upon the headings
        /// </summary>
        /// <param name="input">The document to split</param>
        /// <returns>A list of titles and the markdown content</returns>
        /// <remarks>This does modify the input document</remarks>
        public static List<(string title, MarkdownDocument content)> ParseMarkdownFileIntoTitleSections(MarkdownDocument input)
        {
            var output = new List<(string title, MarkdownDocument content)>();
            var currentDocument = new MarkdownDocument();
            string currentTitle = null;
            foreach (var i in input.Descendants<Block>())
            {
                // if this is title block then restart the current temporary document and add to the output
                if (i is HeadingBlock heading)
                {
                    if (currentTitle != null)
                    {
                        output.Add((currentTitle, currentDocument));
                        currentDocument = new MarkdownDocument();
                    }

                    currentTitle = heading.Inline.FirstChild?.ToString() ?? "";
                }
                else
                {
                    input.Remove(i);
                    // If the parent still isn't null then this is in a nested item which has been moved already
                    if (i.Parent != null)
                    {
                        continue;
                    }
                    currentDocument.Add(i);
                }
            }

            // Handle the last item in the list if there was one
            if (currentTitle != null)
            {
                output.Add((currentTitle, currentDocument));
            }

            return output;
        }
        /// <summary>
        /// Render a MarkdownDocument to plain text
        /// </summary>
        /// <param name="input">The document to convert</param>
        /// <param name="pipeline">Optional Markdig pipeline to configure the renderer</param>
        /// <returns>The resulting plain text rendering</returns>
        /// <remarks>Essentially this just uses the HTMLRenderer with all of the HTML markers turned off</remarks>
        public static string RenderMarkdownToPlainText(MarkdownDocument input, MarkdownPipeline pipeline = null)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            // Set up a HTMLRenderer for plaintext
            var renderer = new HtmlRenderer(writer) { EnableHtmlForBlock = false, EnableHtmlForInline = false, EnableHtmlEscape = false };
            if (pipeline != null)
            {
                pipeline.Setup(renderer);
            }
            renderer.Render(input);
            writer.Flush();
            stream.Position = 0;
            var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        /// <summary>
        /// Load scriptural markdown files
        /// </summary>
        /// <param name="fileSystem">A ZipFileSystem to load things from</param>
        /// <param name="basePath">A base path of the content under the zip root</param>
        /// <param name="resourceContainer">A resource container for this project</param>
        /// <param name="pipeline">An optional Markdig pipeline to configure the markdown parsing</param>
        /// <returns>A Dictionary of book to List of chapters that contains the content</returns>
        /// <exception cref="Exception">Throws an exception if a file can't be loaded for some reason</exception>
        /// <remarks>This is pretty much only used for translationQuestions and translationNotes</remarks>
        public static Dictionary<string, List<MarkdownChapter>> LoadScriptureMarkdownFiles(ZipFileSystem fileSystem, string basePath, ResourceContainer resourceContainer, MarkdownPipeline pipeline)
        {
            var output = new Dictionary<string, List<MarkdownChapter>>();
            foreach (var project in resourceContainer.projects)
            {
                var chapters = new List<MarkdownChapter>();
                foreach (var chapter in fileSystem.GetFolders(fileSystem.Join(basePath, project.path)))
                {
                    // If this isn't a numeric dir then skip it
                    if (!int.TryParse(chapter, out int chapterNumber))
                    {
                        continue;
                    }
                    var chapterOutput = new MarkdownChapter(chapterNumber);
                    var files = fileSystem.GetFiles(fileSystem.Join(basePath, project.path, chapter), ".md");
                    foreach (var verse in files)
                    {
                        // if the filename isn't numeric then skip it since all md files are for a numbered verse
                        if (!int.TryParse(Path.GetFileNameWithoutExtension(verse), out int verseNumber))
                        {
                            continue;
                        }
                        try
                        {
                            var verseContent = ParseMarkdownFileIntoTitleSections(Markdown.Parse(fileSystem.ReadAllText(verse), pipeline));
                            chapterOutput.Verses.Add(new MarkdownVerseContainer(verseNumber, verseContent));
                        }
                        catch (Exception ex)
                        {
                            // If there was a problem loading the file then let users know what file it is
                            throw new Exception($"Error loading source file {verse}", ex);
                        }
                    }
                    chapters.Add(chapterOutput);
                }
                output.Add(project.identifier, chapters);
            }
            return output;
        }

        /// <summary>
        /// Convert chunks for a book in the Door43 format to our internal format
        /// </summary>
        /// <param name="input">A list of chunks to convert</param>
        /// <returns>Chunks in our internal format</returns>
        public static Dictionary<int, List<VerseChunk>> ConvertChunks(List<Door43Chunk> input)
        {
            var output = new Dictionary<int, List<VerseChunk>>();
            var parsed = new Dictionary<int, List<int>>();
            foreach (var chunk in input)
            {
                if (int.TryParse(chunk.Chapter, out int chapter))
                {
                    if (!parsed.ContainsKey(chapter))
                    {
                        parsed.Add(chapter, new List<int>());
                    }

                    if (int.TryParse(chunk.FirstVerse, out int firstVerse))
                    {
                        if (!parsed[chapter].Contains(firstVerse))
                        {
                            parsed[chapter].Add(firstVerse);
                        }
                    }
                }
            }

            // Order and set verse start/end
            foreach (var (chapter, chunks) in parsed)
            {
                var orderedChunks = new List<VerseChunk>();
                if (chunks.Count > 0)
                {
                    chunks.Sort();
                    for (var i = 0; i < chunks.Count; i++)
                    {
                        if (i == chunks.Count - 1)
                        {
                            orderedChunks.Add(new VerseChunk(chunks[i], 0));
                            continue;
                        }
                        orderedChunks.Add(new VerseChunk(chunks[i], chunks[i + 1] - 1));
                    }
                }
                output.Add(chapter, orderedChunks);
            }
            return output;

        }
        /// <summary>
        /// Get chunking information from a USFM file
        /// </summary>
        /// <param name="documents">A list of Documents to get the chunking information from</param>
        /// <param name="log">An instance of ILogger to log any problems to</param>
        /// <returns>A dictionary of our internal chunking format</returns>
        /// <remarks>Chunks are defined as the content between S5 markers</remarks>
        public static Dictionary<string, Dictionary<int, List<VerseChunk>>> GetChunksFromUSFM(List<USFMDocument> documents, ILogger log)
        {
            var output = new Dictionary<string, Dictionary<int, List<VerseChunk>>>();
            foreach (var document in documents)
            {
                var chapterChunkMapping = new Dictionary<int, List<VerseChunk>>();
                int currentChapter = 0;
                var tmpDocument = new USFMDocument();
                var bookId = document.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation?.ToUpper();
                var stack = new Stack<Marker>(document.Contents.Count * 20);
                stack.Push(document);
                while (stack.Count > 0)
                {
                    var current = stack.Pop();
                    if (current is SMarker section)
                    {
                        // if this is a s5 then use that chunk information
                        if (section.Weight == 5)
                        {
                            // skip if there is no content in the tmp chunk or chapter wasn't hit yet
                            if (tmpDocument.Contents.Count != 0 && currentChapter != 0)
                            {
                                InsertChunks(log, chapterChunkMapping, currentChapter, tmpDocument, bookId);
                                tmpDocument = new USFMDocument();
                            }
                        }
                    }
                    else if (current is CMarker chapter)
                    {
                        currentChapter = chapter.Number;
                        chapterChunkMapping.Add(chapter.Number, new List<VerseChunk>());
                    }
                    else if (!(current is USFMDocument))
                    {
                        tmpDocument.Insert(current);
                    }
                    if (current.Contents.Count != 0)
                    {
                        for (var i = current.Contents.Count - 1; i >= 0; i--)
                        {
                            if (!((current.Contents[i] is TextBlock) || current.Contents[i] is FMarker))
                            {
                                stack.Push(current.Contents[i]);
                            }
                        }
                        current.Contents.Clear();
                    }
                }

                // Insert the last chunk so it isn't missed
                InsertChunks(log, chapterChunkMapping, currentChapter, tmpDocument, bookId);
                output.Add(bookId, chapterChunkMapping);
            }

            return output;
        }

        /// <summary>
        /// A helper method to insert chunks into a chunking information
        /// </summary>
        /// <param name="log">An instance of ILogger to log warnings</param>
        /// <param name="chapterChunkMapping">The chunk information to insert into</param>
        /// <param name="currentChapter">What the current chapter number is</param>
        /// <param name="tmpDocument">The USFMDocument containing the chunk we are inserting into the structure</param>
        /// <param name="bookId">The ID of the current Book</param>
        /// <remarks>This is exclusively used inside of GetChunksFromUSFM </remarks>
        private static void InsertChunks(ILogger log, Dictionary<int, List<VerseChunk>> chapterChunkMapping, int currentChapter, USFMDocument tmpDocument, string bookId)
        {
            var verses = tmpDocument.GetChildMarkers<VMarker>();
            if (verses.Count == 0)
            {
                log.LogWarning("Empty chunk found in {Book} {Chapter}", bookId, currentChapter);
            }
            else
            {
                // This will handle verse bridges also
                chapterChunkMapping[currentChapter].Add(new VerseChunk(verses[0].StartingVerse, verses[^1].EndingVerse));
            }
        }

        /// <summary>
        /// Figure out what the maximum length of of string representation of a list of numbers will be
        /// </summary>
        /// <param name="input"></param>
        /// <returns>The number of string </returns>
        public static int GetMaxStringLength(IEnumerable<int> input)
        {
            return input.Max().ToString().Length;
        }

        /// <summary>
        /// Converts from our internal chunking format back to D43 chunks for a book
        /// </summary>
        /// <param name="input">A Dictionary of chapter to verse chunks for a book</param>
        /// <returns>A list of chunks in the D43 format</returns>
        /// <remarks>This is used for when we are outputting things for BTTWriter</remarks>
        public static List<Door43Chunk> ConvertToD43Chunks(Dictionary<int, List<VerseChunk>> input)
        {
            var output = new List<Door43Chunk>();
            var maxChapterLength = input.Select(k => k.Key).Max().ToString().Length;
            foreach (var (chapter, verses) in input)
            {
                var maxVerseLength = verses.Select(k => k.EndingVerse).Max().ToString().Length;
                foreach (var verse in verses.OrderBy(v => v.StartingVerse))
                {
                    output.Add(new Door43Chunk()
                    {
                        // Pad everything with a 0 because that is how D43 does it and BTTWriter expects it
                        Chapter = chapter.ToString().PadLeft(maxChapterLength, '0'),
                        FirstVerse = verse.StartingVerse.ToString().PadLeft(maxVerseLength, '0')
                    });
                }
            }
            return output;
        }
    }
}
