using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.OutputFormats;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using USFMToolsSharp;
using USFMToolsSharp.Models.Markers;
using USFMToolsSharp.Renderers.USX;

namespace BTTWriterCatalog.ContentConverters;

public static class Scripture
{
    private static readonly List<string> _ignoredMarkers =[ "s5", "tr", "tc", "tc1", "tc2", "tc3", "tc4","tcr", "tcr1", "tcr2", "tcr3", "tcr4" ];
    /// <summary>
    /// Generate scripture source files for BTTWriter from a project
    /// </summary>
    /// <param name="fileSystem">A ZipFileSystem holding the data</param>
    /// <param name="basePath">A base path inside the zip file holding the information</param>
    /// <param name="outputInterface">The interface to write the resulting files to</param>
    /// <param name="resourceContainer">Resource Container for all the project metadata</param>
    /// <param name="chunks">Chunking information to use to split up the USFM files</param>
    /// <param name="log">An instance of ILogger to log warnings and information</param>
    /// <returns>A list of all the books successfully processed</returns>
    /// <exception cref="Exception">Logs an error if there is an unhandled problem loading a file</exception>
    public static async Task<List<string>> ConvertAsync(IZipFileSystem fileSystem, string basePath, IOutputInterface outputInterface, ResourceContainer resourceContainer, Dictionary<string, Dictionary<int, List<VerseChunk>>> chunks, ILogger log)
    {
        // Partial USX allows us to render a portion of USFM to USX without creating a whole document
        var renderer = new USXRenderer(new USXConfig() { PartialUSX = true });
        // Skip s5 markers because we no longer need them
        var parser = new USFMParser(_ignoredMarkers, true);
        var convertedBooks = new List<string>();
        var outputTasks = new List<Task>();
        foreach (var project in resourceContainer.projects)
        {
            if (!fileSystem.FileExists(fileSystem.Join(basePath, project.path)))
            {
                log.LogError("File {File} does not exist in the zip file", fileSystem.Join(basePath, project.path));
                continue;
            }
            var bookText = await fileSystem.ReadAllTextAsync(fileSystem.Join(basePath, project.path));
            var document = parser.ParseFromString(bookText);
            var bookAbbreviation = project.identifier.ToUpper();
            convertedBooks.Add(bookAbbreviation.ToLower());
            var resource = new ScriptureResource
            {
                ModifiedOn = DateTime.Now.ToString("yyyyMMdd")
            };

            var allChapters = document.GetChildMarkers<CMarker>();
            var maxChapterNumberLength = allChapters.Select(c => c.Number).Max().ToString().Length;
            if (chunks.TryGetValue(bookAbbreviation, out var bookChunks))
            {
                try
                {

                    foreach (var (chapterNumber, chapterChunks) in bookChunks)
                    {
                        var currentChapter = allChapters.FirstOrDefault(c => c.Number == chapterNumber);
                        var allVerses = currentChapter?.GetChildMarkers<VMarker>();
                        // If there just so happens to be no verses in a chapter warn about it and continue on
                        if (allVerses == null || allVerses.Count  == 0)
                        {
                            log.LogError("No verses found for {Book} {Chapter}", bookAbbreviation, chapterNumber);
                            continue;
                        }
                        var maxVerseNumberLength = allVerses.Select(c => c.EndingVerse).Max().ToString().Length;
                        var outputChapter = new ScriptureChapter() { ChapterNumber = chapterNumber.ToString().PadLeft(maxChapterNumberLength, '0'), Reference = string.Empty, Title = string.Empty };
                        for (int i = 0; i < chapterChunks.Count; i++)
                        {
                            var chunk = chapterChunks[i];
                            // Look if we need to adjust the chunk to include a verse bridge
                            var bridge = allVerses.FirstOrDefault(v => v.StartingVerse < v.EndingVerse && chunk.EndingVerse >= v.StartingVerse && chunk.EndingVerse < v.EndingVerse);
                            if (bridge != null)
                            {
                                // Extend chunk to end of bridge
                                chunk.EndingVerse = bridge.EndingVerse;
                                // Adjust next chunk to avoid duplication
                                if (i + 1 < chapterChunks.Count)
                                {
                                    chapterChunks[i + 1].StartingVerse = bridge.EndingVerse + 1;
                                }
                            }
                            // Create a new USFM document, insert all the verses for this chunk and then convert them to USX
                            var content = new USFMDocument();
                            content.InsertMultiple(allVerses.Where(v => v.StartingVerse >= chunk.StartingVerse && (chunk.EndingVerse == 0 || v.EndingVerse <= chunk.EndingVerse)));
                            ReplaceWordsWithText(content);
                            var text = renderer.Render(content);
                            outputChapter.Frames.Add(new ScriptureFrame()
                            {
                                Format = "usx",
                                Id = $"{chapterNumber.ToString().PadLeft(maxChapterNumberLength, '0')}-{chunk.StartingVerse.ToString().PadLeft(maxVerseNumberLength, '0')}",
                                LastVerse = chunk.EndingVerse.ToString(),
                                Image = "",
                                Text = text
                            });
                        }
                        resource.Chapters.Add(outputChapter);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error rendering {bookAbbreviation}", ex);
                }
            }
            var specificOutputPath = bookAbbreviation.ToLower();
                
            if (!outputInterface.DirectoryExists(specificOutputPath))
            {
                outputInterface.CreateDirectory(specificOutputPath);
            }
                
            outputTasks.Add(outputInterface.WriteAllTextAsync(Path.Join(specificOutputPath, $"{bookAbbreviation.ToLower()}.usfm"), bookText));
            outputTasks.Add(outputInterface.WriteAllTextAsync(Path.Join(specificOutputPath, "source.json"), JsonSerializer.Serialize(resource, CatalogJsonContext.Default.ScriptureResource)));
        }
        // When all of the IO tasks are complete then continue on
        await Task.WhenAll(outputTasks);
        return convertedBooks;
    }

    /// <summary>
    /// Replaces the USFM markers with text
    /// </summary>
    /// <remarks>This is a destructive operation it does change the usfm marker</remarks>
    /// <param name="document">The document to alter</param>
    private static void ReplaceWordsWithText(Marker document)
    {
        if (document == null || document.Contents == null || document.Contents.Count == 0)
        {
            return;
        }
        var words = document.GetChildMarkers<WMarker>();
        if (words.Count == 0)
        {
            return;
        }
        var hierarchy = document.GetHierachyToMultipleMarkers(new List<Marker>(words));
        foreach (var (marker, path) in hierarchy)
        {
            var wordMarker = (WMarker)marker;
            var index = path[^2].Contents.IndexOf(wordMarker);
            path[^2].Contents.Insert(index, new TextBlock(wordMarker.Term));
            path[^2].Contents.Remove(wordMarker);
        }
    }
}