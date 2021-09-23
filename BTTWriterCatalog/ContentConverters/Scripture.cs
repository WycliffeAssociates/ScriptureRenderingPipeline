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
            foreach(var item in fileSystem.GetAllFiles(".usfm"))
            {
                var document = parser.ParseFromString(fileSystem.ReadAllText(item));
                var bookAbbriviation = document.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation?.ToUpper();
                convertedBooks.Add(bookAbbriviation.ToLower());
                var resource = new ScriptureResource();
                var allChapters = document.GetChildMarkers<CMarker>();
                if (chunks.ContainsKey(bookAbbriviation))
                {
                    foreach(var (chapterNumber, chapterChunks) in chunks[bookAbbriviation])
                    {
                        var currentChapter = allChapters.First(c => c.Number == chapterNumber);
                        var allVerses = currentChapter.GetChildMarkers<VMarker>();
                        // TODO: this probably needs to be zero padded
                        var outputChapter = new ScriptureChapter() { ChapterNumber = chapterNumber.ToString() };
                        foreach(var chunk in chapterChunks)
                        {
                            var content = new USFMDocument();
                            content.InsertMultiple(allVerses.Where(v => v.StartingVerse >= chunk.StartingVerse && (chunk.EndingVerse == 0 || v.EndingVerse <= chunk.EndingVerse)));
                            var text = renderer.Render(content);
                            outputChapter.Frames.Add(new ScriptureFrame() { Format = "USX", Id = $"{chapterNumber}-{chunk.StartingVerse}", LastVerse = chunk.EndingVerse.ToString(), Image = "", Text = text }) ;
                        }
                        resource.Chapters.Add(outputChapter);
                    }
                }
                var specificOutputPath = Path.Join(outputPath, bookAbbriviation.ToLower());
                if (!Directory.Exists(specificOutputPath))
                {
                    Directory.CreateDirectory(specificOutputPath);
                }
                File.WriteAllText(Path.Join(specificOutputPath, "source.json"), JsonConvert.SerializeObject(resource)) ;
            }
            return convertedBooks;
        }
    }
}
