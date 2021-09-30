using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.OutputFormats;
using Newtonsoft.Json;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using USFMToolsSharp;
using USFMToolsSharp.Models.Markers;
using USFMToolsSharp.Renderers.USX;

namespace BTTWriterCatalog.ContentConverters
{
    public static class Scripture
    {
        public static List<string> Convert(ZipFileSystem fileSystem, string basePath, string outputPath, ResourceContainer container, Dictionary<string,Dictionary<int,List<VerseChunk>>> chunks)
        {
            var renderer = new USXRenderer(new USXConfig() { PartialUSX = true });
            var parser = new USFMParser( new List<string>() { "s5" });
            var convertedBooks = new List<string>();
            foreach(var project in container.projects)
            {
                var bookText = fileSystem.ReadAllText(fileSystem.Join(basePath, project.path));
                var document = parser.ParseFromString(bookText);
                var bookAbbriviation = project.identifier.ToUpper();
                convertedBooks.Add(bookAbbriviation.ToLower());
                var resource = new ScriptureResource
                {
                    ModifiedOn = DateTime.Now.ToString("yyyyMMdd")
                };
                var allChapters = document.GetChildMarkers<CMarker>();
                var maxChapterNumberLength = allChapters.Select(c => c.Number).Max().ToString().Length;
                if (chunks.ContainsKey(bookAbbriviation))
                {
                    foreach(var (chapterNumber, chapterChunks) in chunks[bookAbbriviation])
                    {
                        var currentChapter = allChapters.First(c => c.Number == chapterNumber);
                        var allVerses = currentChapter.GetChildMarkers<VMarker>();
                        var maxVerseNumberLength = allVerses.Select(c => c.EndingVerse).Max().ToString().Length;
                        var outputChapter = new ScriptureChapter() { ChapterNumber = chapterNumber.ToString().PadLeft(maxChapterNumberLength,'0'), Reference = string.Empty, Title=string.Empty };
                        foreach(var chunk in chapterChunks)
                        {
                            var content = new USFMDocument();
                            content.InsertMultiple(allVerses.Where(v => v.StartingVerse >= chunk.StartingVerse && (chunk.EndingVerse == 0 || v.EndingVerse <= chunk.EndingVerse)));
                            var text = renderer.Render(content);
                            outputChapter.Frames.Add(new ScriptureFrame() { 
                                Format = "usx", Id = $"{chapterNumber.ToString().PadLeft(maxChapterNumberLength,'0')}-{chunk.StartingVerse.ToString().PadLeft(maxVerseNumberLength,'0')}",
                                LastVerse = chunk.EndingVerse.ToString(),
                                Image = "",
                                Text = text }) ;
                        }
                        resource.Chapters.Add(outputChapter);
                    }
                }
                var specificOutputPath = Path.Join(outputPath, bookAbbriviation.ToLower());
                if (!Directory.Exists(specificOutputPath))
                {
                    Directory.CreateDirectory(specificOutputPath);
                }
                File.WriteAllText(Path.Join(specificOutputPath, "source.usfm"), bookText);
                File.WriteAllText(Path.Join(specificOutputPath, "source.json"), JsonConvert.SerializeObject(resource)) ;
            }
            return convertedBooks;
        }
    }
}
