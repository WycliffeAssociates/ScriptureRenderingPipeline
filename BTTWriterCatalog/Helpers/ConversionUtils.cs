using BTTWriterCatalog.Models;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using USFMToolsSharp;
using USFMToolsSharp.Models.Markers;

namespace BTTWriterCatalog.Helpers
{
    public static class ConversionUtils
    {
        public static List<(string title, MarkdownDocument content)> ParseMarkdownFileIntoTitleSections(MarkdownDocument result)
        {
            var output = new List<(string title, MarkdownDocument content)>();
            var currentDocument = new MarkdownDocument();
            string currentTitle = null;
            foreach (var i in result.Descendants<Block>())
            {
                if (i is HeadingBlock heading)
                {
                    if (currentTitle != null)
                    {
                        output.Add((currentTitle, currentDocument));
                        currentDocument = new MarkdownDocument();
                    }

                    currentTitle = heading.Inline.FirstChild.ToString();
                }
                else
                {
                    result.Remove(i);
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
        public static string RenderMarkdownToPlainText(MarkdownDocument input, MarkdownPipeline pipeline = null)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
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
        public static Dictionary<string, List<MarkdownChapter>> LoadScriptureMarkdownFiles(ZipFileSystem fileSystem, string basePath, ResourceContainer container)
        {
            var output = new Dictionary<string, List<MarkdownChapter>>();
            foreach (var project in container.projects)
            {
                var chapters = new List<MarkdownChapter>();
                foreach (var chapter in fileSystem.GetFolders(fileSystem.Join(basePath, project.path)))
                {
                    if (!int.TryParse(chapter, out int chapterNumber))
                    {
                        continue;
                    }
                    var chapterOutput = new MarkdownChapter(chapterNumber);
                    foreach (var verse in fileSystem.GetFiles(fileSystem.Join(basePath, project.path, chapter), ".md"))
                    {
                        if (!int.TryParse(Path.GetFileNameWithoutExtension(verse), out int verseNumber))
                        {
                            continue;
                        }
                        try
                        {

                            var verseContent = ParseMarkdownFileIntoTitleSections(Markdown.Parse(fileSystem.ReadAllText(verse)));
                            chapterOutput.Verses.Add(new MarkdownVerseContainer(verseNumber, verseContent));
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"Error loading source file {verse}",ex);
                        }
                    }
                    chapters.Add(chapterOutput);
                }
                output.Add(project.identifier, chapters);
            }
            return output;
        }

        public static Dictionary<int,List<VerseChunk>> ConvertChunks(List<Door43Chunk> input)
        {
            var output = new Dictionary<int,List<VerseChunk>>();
            var parsed = new Dictionary<int, List<int>>();
            foreach(var chunk in input)
            {
                if (int.TryParse(chunk.Chapter, out int chapter))
                {
                    if (!parsed.ContainsKey(chapter))
                    {
                        parsed.Add(chapter, new List<int>());
                    }

                    if (int.TryParse(chunk.FirstVerse,out int firstVerse))
                    {
                        if (!parsed[chapter].Contains(firstVerse))
                        {
                            parsed[chapter].Add(firstVerse);
                        }
                    }
                }
            }

            // Order and set verse start/end
            foreach(var (chapter,chunks) in parsed)
            {
                var orderedChunks = new List<VerseChunk>();
                if (chunks.Count > 0)
                {
                    chunks.Sort();
                    for(var i = 0; i< chunks.Count; i++)
                    {
                        if (i == chunks.Count - 1)
                        {
                            orderedChunks.Add(new VerseChunk(chunks[i], 0));
                            continue;
                        }
                        orderedChunks.Add(new VerseChunk(chunks[i], chunks[i + 1]));
                    }
                }
                output.Add(chapter, orderedChunks);
            }
            return output;

        }
        public static Dictionary<string,Dictionary<int,List<VerseChunk>>> GetChunksFromUSFM(List<USFMDocument> documents)
        {
            var output = new Dictionary<string, Dictionary<int, List<VerseChunk>>>();
            USFMParser parser = new USFMParser();
            foreach(var document in documents)
            {
                var chapterChunkMapping = new Dictionary<int, List<VerseChunk>>();
                int currentChapter = 0;
                var tmpDocument = new USFMDocument();
                var bookId = document.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation?.ToUpper();
                var stack = new Stack<Marker>(document.Contents.Count * 2);
                stack.Push(document);
                while(stack.Count > 0)
                {
                    var current = stack.Pop();
                    if (current is SMarker section)
                    {
                        // if this is a s5 then use that chunk information
                        if (section.Weight == 5)
                        {
                            // skip if there is no content in the tmp chunk or chapter wasn't hit yet
                            if (tmpDocument.Contents.Count!= 0 && currentChapter != 0)
                            {
                                var verses = tmpDocument.GetChildMarkers<VMarker>();
                                if (verses.Count == 0)
                                {
                                    var a = 1;
                                }
                                // This will handle verse bridges also
                                chapterChunkMapping[currentChapter].Add(new VerseChunk(verses[0].StartingVerse, verses[^1].EndingVerse));
                                tmpDocument = new USFMDocument();
                            }
                        }
                    } else if (current is CMarker chapter)
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
                        for(var i = current.Contents.Count - 1; i >= 0; i--)
                        {
                            if (! ((current.Contents[i] is TextBlock) || current.Contents[i] is FMarker))
                            {
                                stack.Push(current.Contents[i]);
                            }
                        }
                    }
                }
                output.Add(bookId, chapterChunkMapping);
            }

            return output;
        }
        public static Dictionary<string,Dictionary<int,List<VerseChunk>>> GetChunksFromUSFM(List<string> fileContents)
        {
            USFMParser parser = new USFMParser();
            return GetChunksFromUSFM(fileContents.Select(f => parser.ParseFromString(f)).ToList());
        }
        public static List<Door43Chunk> ConvertToD43Chunks(Dictionary<int,List<VerseChunk>> input)
        {
            var output = new List<Door43Chunk>();
            var maxChapterLength = input.Select(k => k.Key).Max().ToString().Length;
            foreach(var (chapter, verses) in input)
            {
                var maxVerseLength = verses.Select(k => k.EndingVerse).Max().ToString().Length;
                foreach(var verse in verses.OrderBy(v => v.StartingVerse))
                {
                    output.Add(new Door43Chunk()
                    {
                        Chapter = chapter.ToString().PadLeft(maxChapterLength, '0'),
                        FirstVerse = verse.StartingVerse.ToString().PadLeft(maxVerseLength, '0')
                    });
                }
            }
            return output;
        }
    }
}
