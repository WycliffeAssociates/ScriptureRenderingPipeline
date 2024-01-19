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

namespace BTTWriterCatalog.ContentConverters
{
    public static class Scripture
    {
        /// <summary>
        /// Generate scripture source files for BTTWriter from a project
        /// </summary>
        /// <param name="fileSystem">A ZipFileSytem holding the data</param>
        /// <param name="basePath">A base path inside of the zip file holding the information</param>
        /// <param name="outputPath">The directory to output the resulting json files into</param>
        /// <param name="resourceContainer">Resource Container for all of the project metadata</param>
        /// <param name="chunks">Chunking information to use to split up the USFM files</param>
        /// <param name="log">An instance of ILogger to log warnings and information</param>
        /// <returns>A list of all of the books successfully processed</returns>
        /// <exception cref="Exception">Logs an error if there is a unhandled problem loading a file</exception>
        public static async Task<List<string>> ConvertAsync(IZipFileSystem fileSystem, string basePath, IOutputInterface outputInterface, ResourceContainer resourceContainer, Dictionary<string, Dictionary<int, List<VerseChunk>>> chunks, ILogger log)
        {
            // Partial USX allows us to render a portion of USFM to USX without creating a whole document
            var renderer = new USXRenderer(new USXConfig() { PartialUSX = true });
            // Skip s5 markers because we no longer need them
            var parser = new USFMParser(new List<string>() { "s5" }, true);
            var convertedBooks = new List<string>();
            var outputTasks = new List<Task>();
            foreach (var project in resourceContainer.projects)
            {
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
                            var currentChapter = allChapters.First(c => c.Number == chapterNumber);
                            var allVerses = currentChapter.GetChildMarkers<VMarker>();
                            // If there just so happens to be no verses in a chapter warn about it and continue on
                            if (allVerses.Count == 0)
                            {
                                log.LogError("No verses found for {book} {chapter}", bookAbbreviation, chapterNumber);
                                continue;
                            }
                            var maxVerseNumberLength = allVerses.Select(c => c.EndingVerse).Max().ToString().Length;
                            var outputChapter = new ScriptureChapter() { ChapterNumber = chapterNumber.ToString().PadLeft(maxChapterNumberLength, '0'), Reference = string.Empty, Title = string.Empty };
                            foreach (var chunk in chapterChunks)
                            {
                                // Create a new USFM document, insert all the verses for this chunk and then convert them to USX
                                var content = new USFMDocument();
                                content.InsertMultiple(allVerses.Where(v => v.StartingVerse >= chunk.StartingVerse && (chunk.EndingVerse == 0 || v.EndingVerse <= chunk.EndingVerse)));
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
    }
}
