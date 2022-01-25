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

namespace ScriptureRenderingPipeline.Renderers
{
    internal class CommentaryRenderer
    {
        const string ChapterIdFormat = "chapter-{0}";

        public async Task RenderAsync(ZipFileSystem sourceDir, string basePath, string destinationDir, Template template, Template printTemplate, string repoUrl, string heading, ResourceContainer resourceContainer, string baseUrl, string userToRouteResourcesTo, string textDirection, bool isBTTWriterProject = false)
        {
            var content = await LoadMarkdownFiles(sourceDir, basePath, resourceContainer.projects);
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

            outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "print_all.html"), printTemplate.Render(Hash.FromAnonymousObject(new { content = printStringBuilder.ToString(), heading }))));

            await Task.WhenAll(outputTasks);
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
                        id = String.Format(ChapterIdFormat, chapter.Number),
                        title = chapter.Number
                    });
                }
                output.Add(outputBook);
            }
            return output;
        }

        private async Task<List<CommentaryBook>> LoadMarkdownFiles(ZipFileSystem sourceDir, string basePath, Project[] projects)
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
                foreach (var chapter in sourceDir.GetFiles(sourceDir.Join(basePath, project.path), ".md"))
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
        private Dictionary<string, MarkdownDocument> LoadArticles(ZipFileSystem sourceDir, string basePath)
        {
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var output = new Dictionary<string, MarkdownDocument>();
            foreach(var file in sourceDir.GetFiles(sourceDir.JoinPath(basePath, "articles"), ".md"))
            {
                output.Add(Path.GetFileNameWithoutExtension(file), Markdown.Parse(sourceDir.ReadAllText(file)));
            }
            return output;
        }
        protected IEnumerable<string> FilterAndOrderChapters(IEnumerable<string> input)
        {
            return input.Select(i => Path.GetFileName(i)).Where(i => i == "intro.md" || int.TryParse(i.Split(".")[0], out _)).Select(i => (file: i, order: i == "intro.md" ? 0 : int.Parse(i.Split(".")[0]))).OrderBy(i => i.order).Select(i => i.file);
        }

    }
    class CommentaryBook
    {
        public string Title { get; set; }
        public List<CommentaryChapter> Chapters { get; set; }
        public string BookId { get; set; }

        public CommentaryBook()
        {
            Chapters = new List<CommentaryChapter>();
        }
    }
    class CommentaryChapter
    {
        public string Number { get; set; }
        public MarkdownDocument Content { get; set; }
    }
}
