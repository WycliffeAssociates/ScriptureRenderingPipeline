using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.OutputFormats;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Newtonsoft.Json;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTTWriterCatalog.ContentConverters
{
    public class TranslationNotes
    {
        public static void Convert(ZipFileSystem fileSystem, string basePath, string outputPath, ResourceContainer container, Dictionary<string,List<InputChunk>> chunks)
        {
            var files = LoadMarkdownFiles(fileSystem, basePath, container);
            foreach (var book in files)
            {
                var bookOutput = new List<TranslationNoteChunk>();
                if (!chunks.ContainsKey(book.Key.ToUpper()) || chunks[book.Key.ToUpper()].Count == 0)
                {
                    //TODO: We should probably warn at this point that chunks are missing for a book
                    continue;
                }
                // TODO: Need to figure out formatting of numbers
                var maxChapterNumberChars = book.Value.Max(i => i.ChapterNumber).ToString().Length;
                var convertedChunks = ConvertChunks(chunks[book.Key.ToUpper()]);
                foreach (var chapter in book.Value)
                {
                    var maxVerseNumberChars = chapter.Verses.Max(v => v.VerseNumber).ToString().Length;
                    var verseChunks = convertedChunks[chapter.ChapterNumber];
                    foreach(var (start, end) in verseChunks)
                    {
                        var currentChunk = new TranslationNoteChunk() { 
                            Id = $"{chapter.ChapterNumber.ToString().PadLeft(maxChapterNumberChars,'0')}-{start.ToString().PadLeft(maxVerseNumberChars,'0')}" 
                        };
                        var content = new List<(string title, MarkdownDocument content)>();
                        if (end == 0)
                        {
                            foreach(var i in chapter.Verses.Where(i => i.VerseNumber >= start))
                            {
                                content.AddRange(i.Content);
                            }
                        }
                        foreach(var i in chapter.Verses.Where(i => i.VerseNumber >= start && i.VerseNumber < end))
                        {
                            content.AddRange(i.Content);
                        }

                        currentChunk.Notes.AddRange(content.Select(c => new TranslationNoteEntry() { Reference = c.title, Text = RenderToPlainText(c.content) }));
                        bookOutput.Add(currentChunk);
                    }
                }
                string bookDir = Path.Join(outputPath,book.Key);
                if (!Directory.Exists(bookDir))
                {
                    Directory.CreateDirectory(bookDir);
                }
                File.WriteAllText(Path.Join(bookDir, "notes.json"), JsonConvert.SerializeObject(bookOutput));
            }
        }

        private static Dictionary<string, List<MarkdownChapter>> LoadMarkdownFiles(ZipFileSystem fileSystem, string basePath, ResourceContainer container)
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

                            var verseContent = ParseMarkdownFile(Markdown.Parse(fileSystem.ReadAllText(verse)));
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

        private static Dictionary<int,List<(int start, int end)>> ConvertChunks(List<InputChunk> input)
        {
            var output = new Dictionary<int,List<(int start, int end)>>();
            var tmp = new Dictionary<int, List<int>>();
            foreach(var chunk in input)
            {
                if (int.TryParse(chunk.Chapter, out int chapter))
                {
                    if (!tmp.ContainsKey(chapter))
                    {
                        tmp.Add(chapter, new List<int>());
                    }

                    if (int.TryParse(chunk.FirstVerse,out int firstVerse))
                    {
                        if (!tmp[chapter].Contains(firstVerse))
                        {
                            tmp[chapter].Add(firstVerse);
                        }
                    }
                }
            }

            // Order and set verse start/end
            foreach(var item in tmp)
            {
                var orderedChunks = new List<(int start, int end)>();
                if (item.Value.Count > 0)
                {
                    item.Value.Sort();
                    for(var i = 0; i< item.Value.Count; i++)
                    {
                        if (i == item.Value.Count - 1)
                        {
                            orderedChunks.Add((item.Value[i], 0));
                            continue;
                        }
                        orderedChunks.Add((item.Value[i], item.Value[i + 1]));
                    }
                }
                output.Add(item.Key, orderedChunks);
            }
            return output;

        }

        private static List<(string title, MarkdownDocument content)> ParseMarkdownFile(MarkdownDocument result)
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
        private static string RenderToPlainText(MarkdownDocument input, MarkdownPipeline pipeline = null)
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
    }
    public class MarkdownVerseContainer
    {
        public int VerseNumber { get; set; }
        public List<(string title, MarkdownDocument content)> Content {  get; set; }

        public MarkdownVerseContainer(int verseNumber, List<(string title, MarkdownDocument content)> verseContent)
        {
            VerseNumber = verseNumber;
            Content = verseContent;
        }
    }
    public class MarkdownChapter
    {
        public int ChapterNumber {  get; set;}
        public List<MarkdownVerseContainer> Verses {  get; set; }
        public MarkdownChapter(int chapterNumber)
        {
            ChapterNumber = chapterNumber;
            Verses = new List<MarkdownVerseContainer>();
        }
    }
}
