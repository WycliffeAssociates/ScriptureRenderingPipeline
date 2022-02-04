using DotLiquid;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using ScriptureRenderingPipeline.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Markdig;
using Markdig.Syntax;
using System.IO;
using System.Linq;
using Markdig.Syntax.Inlines;
using Newtonsoft.Json;

namespace ScriptureRenderingPipeline.Renderers
{
    internal class CommentaryRenderer
    {
        const string ChapterIdFormat = "chapter-{0}";

        public async Task RenderAsync(ZipFileSystem sourceDir, string basePath, string destinationDir, Template template, Template printTemplate, string repoUrl, string heading, ResourceContainer resourceContainer, string baseUrl, string userToRouteResourcesTo, string textDirection, bool isBTTWriterProject = false)
        {
            var content = LoadMarkdownFiles(sourceDir, basePath, resourceContainer.projects);
            var articles = LoadArticles(sourceDir, basePath);
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var outputTasks = new List<Task>();
            var indexWritten = false;
            var printStringBuilder = new StringBuilder();
            var navigation = BuildNavigation(content);
            foreach(var book in content)
            {
                var bookStringBuilder = new StringBuilder(book.Chapters.Count * 2);

                foreach(var chapter in book.Chapters)
                {
                    RewriteLinks(chapter.Content);
                    bookStringBuilder.Append($"<div id=\"{(string.Format(ChapterIdFormat,chapter.Number))}\"></div>");
                    bookStringBuilder.Append(Markdown.ToHtml(chapter.Content, pipeline));
                    printStringBuilder.Append(Markdown.ToHtml(chapter.Content, pipeline));
                }

                var templateResult = template.Render(Hash.FromDictionary(new Dictionary<string,object>()
                {
                    ["content"] = bookStringBuilder.ToString(),
                    ["contenttype"] = "commentary",
                    ["scriptureNavigation"] = navigation,
                    ["currentPage"] = BuildFileName(book),
                    ["heading"] = heading,
                    ["sourceLink"] = repoUrl,
                    ["textDirection"] = textDirection
                }
                ));

                outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, BuildFileName(book)), templateResult));

                if (!indexWritten)
                {
                    outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "index.html"), templateResult));
                    indexWritten = true;
                }
            }

            foreach(var (title,article) in articles)
            {
                RewriteLinks(article);
                var tmpContent = Markdown.ToHtml(article, pipeline);
                // Add articles to print copy
                printStringBuilder.Append(tmpContent);

                outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, $"{title}.html"),tmpContent));
            }

            outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "print_all.html"), printTemplate.Render(Hash.FromAnonymousObject(new { content = printStringBuilder.ToString(), heading }))));

            await Task.WhenAll(outputTasks);
        }

        /// <summary>
        /// Change the md links to popup:// links
        /// </summary>
        /// <param name="input"></param>
        /// <remarks>This modifies the input</remarks>
        private void RewriteLinks(MarkdownDocument input)
        {
            foreach(var (id, link) in GetLinks(input))
            {
                link.Url = $"popup://{id}.html";
            }
        }

        private List<(string id, LinkInline link)> GetLinks(MarkdownDocument input)
        {
            var output = new List<(string id, LinkInline link)>();
            foreach (var link in input.Descendants<LinkInline>())
            {
                if (string.IsNullOrEmpty(link.Url))
                {
                    continue;
                }

                if (!link.Url.EndsWith(".md"))
                {
                    continue;
                }

                if (!link.Url.StartsWith("../articles/"))
                {
                    continue;
                }

                output.Add((Path.GetFileNameWithoutExtension(link.Url),link));
            }
            return output;
        }

        private string BuildFileName(CommentaryBook book)
        {
            return $"{Utils.GetBookNumber(book.BookId.ToLower())}-{book.BookId.ToLower()}.html";
        }

        private List<NavigationBook> BuildNavigation(List<CommentaryBook> input)
        {
            var output = new List<NavigationBook>();
            foreach(var book in input)
            {
                var outputBook = new NavigationBook()
                {
                    abbreviation = book.BookId,
                    title = book.Title,
                    file = BuildFileName(book),
                };
                foreach(var chapter in book.Chapters)
                {
                    outputBook.chapters.Add(new NavigationChapter()
                    {
                        id = string.Format(ChapterIdFormat, chapter.Number),
                        title = chapter.Number
                    });
                }
                output.Add(outputBook);
            }
            return output;
        }

        private List<CommentaryBook> LoadMarkdownFiles(ZipFileSystem sourceDir, string basePath, Project[] projects)
        {
            var output = new List<CommentaryBook>(projects.Length);
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            foreach (var project in projects.OrderBy(p => p.sort))
            {
                var outputBook = new CommentaryBook()
                {
                    Title = project.title,
                    BookId = project.identifier,
                };
                foreach (var chapter in FilterAndOrderChapters(sourceDir.GetFiles(sourceDir.Join(basePath, project.path), ".md")))
                {
                    outputBook.Chapters.Add(new CommentaryChapter()
                    {
                        Number = Path.GetFileNameWithoutExtension(chapter),
                        Content = Markdown.Parse(sourceDir.ReadAllText(chapter), pipeline)
                    });
                }
                output.Add(outputBook);
            }
            return output;
        }
        private Dictionary<string,MarkdownDocument> LoadArticles(ZipFileSystem sourceDir, string basePath)
        {
            var output = new Dictionary<string,MarkdownDocument>();
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            foreach(var article in sourceDir.GetFiles(sourceDir.Join(basePath, "articles"),".md"))
            {
                output.Add(Path.GetFileNameWithoutExtension(article),Markdown.Parse(sourceDir.ReadAllText(article), pipeline));
            }
            return output;
        }
        private IEnumerable<string> FilterAndOrderChapters(IEnumerable<string> input)
        {
            var tmp = new List<(string fileName, string fileNameWithoutExtension, int Order)>(input.Count());
            foreach(var i in input)
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(i);
                var tmpOrder = 0;
                if (fileNameWithoutExtension != "intro" && !int.TryParse(fileNameWithoutExtension, out tmpOrder))
                {
                    continue;
                }
                tmp.Add((i,fileNameWithoutExtension,tmpOrder));
            }
            return tmp.OrderBy(i => i.Order).Select(i => i.fileName);
        }

    }
}
