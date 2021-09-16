using BTTWriterCatalog.Models;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

        public static Dictionary<int,List<(int start, int end)>> ConvertChunks(List<InputChunk> input)
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
    }
}
