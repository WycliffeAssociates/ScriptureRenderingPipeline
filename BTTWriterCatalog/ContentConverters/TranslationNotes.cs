using BTTWriterCatalog.Models;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTTWriterCatalog.ContentConverters
{
    public class TranslationNotes
    {
        public void Convert(ZipFileSystem fileSystem, string basePath, string outputPath, ResourceContainer container, Dictionary<string,List<Chunk>> chunks)
        {
            Dictionary<string,Dictionary<string,string>>
            foreach(var project in container.projects)
            {
                foreach(var chapter in fileSystem.GetFolders(fileSystem.Join(basePath, project.path)))
                {
                    if (!int.TryParse(chapter, out _))
                    {
                        continue;
                    }
                    foreach(var verse in fileSystem.GetFiles(fileSystem.Join(basePath, project.path, chapter), ".md"))
                    {
                        if (!int.TryParse(Path.GetFileNameWithoutExtension(verse), out _))
                        {
                            continue;
                        }
                        var verseContent = ParseMarkdownFile(Markdown.Parse(fileSystem.ReadAllText(verse)));
                    }
                }
            }
        }

        private static Dictionary<string, MarkdownDocument> ParseMarkdownFile(MarkdownDocument result)
        {
            var output = new Dictionary<string, MarkdownDocument>();
            var currentDocument = new MarkdownDocument();
            string currentTitle = null;
            foreach (var i in result.Descendants<Block>())
            {
                if (i is HeadingBlock heading)
                {
                    if (currentTitle != null)
                    {
                        output.Add(currentTitle, currentDocument);
                        currentDocument = new MarkdownDocument();
                    }

                    currentTitle = heading.Inline.FirstChild.ToString();
                }
                else
                {
                    result.Remove(i);
                    currentDocument.Add(i);
                }
            }

            // Handle the last item in the list if there was one
            if (currentTitle != null)
            {
                output.Add(currentTitle, currentDocument);
            }

            return output;
        }
        private static string RenderToPlainText(MarkdownDocument input)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            var renderer = new HtmlRenderer(writer) { EnableHtmlForBlock = false, EnableHtmlForInline = false, EnableHtmlEscape = false };
            renderer.Render(input);
            writer.Flush();
            stream.Position = 0;
            var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }


    }
}
