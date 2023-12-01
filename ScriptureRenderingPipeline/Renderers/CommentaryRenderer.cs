using DotLiquid;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using ScriptureRenderingPipeline.Models;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Markdig;
using Markdig.Syntax;
using System.IO;
using System.Linq;
using Markdig.Syntax.Inlines;
using JsonSerializer = System.Text.Json.JsonSerializer;
using System.Text.Json;

namespace ScriptureRenderingPipeline.Renderers
{
	public class CommentaryRenderer: IRenderer
	{
		private const string ChapterIdFormat = "chapter-{0}";

		public async Task RenderAsync(RendererInput input, IOutputInterface output)
		{
			var content = LoadMarkdownFiles(input.FileSystem, input.BasePath, input.ResourceContainer.projects);
			var articles = LoadArticles(input.FileSystem, input.BasePath);
			var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
			var outputTasks = new List<Task>();
			var printStringBuilder = new StringBuilder();
			var lastRendered = System.DateTime.UtcNow.ToString("o");

			var outputIndex = new OutputIndex()
			{
				TextDirection = input.LanguageTextDirection,
				LanguageCode = input.LanguageCode,
				LanguageName = input.LanguageName,
				ResourceType = "commentary",
				ResourceTitle = input.Title,
				RepoUrl = input.RepoUrl,
				Bible = new List<OutputBook>(),
				Navigation = null,
				LastRendered = lastRendered,
				AppMeta = input.AppsMeta
			};
			var downloadIndex = new DownloadIndex()
			{
				LastRendered = lastRendered
			};
			foreach (var book in content)
			{
				var bookStringBuilder = new StringBuilder();
				if (!output.DirectoryExists(book.BookId))
				{
					output.CreateDirectory(book.BookId);
				}

				var outputBook = new OutputBook()
				{
					Label = book.Title,
					Slug = book.BookId,
					LastRendered = lastRendered
				};
				var bookWithContent = new OutputBook()
				{
					Label = book.Title,
					Slug = book.BookId,
					LastRendered = lastRendered
				};

				foreach (var chapter in book.Chapters)
				{
					outputBook.Chapters.Add(new OutputChapters()
					{
						Label = chapter.Number,
						Number = chapter.Number
					});
					RewriteLinks(chapter.Content);
					bookStringBuilder.Append($"<div id=\"{(string.Format(ChapterIdFormat, chapter.Number))}\"></div>");
					var renderedContent = Markdown.ToHtml(chapter.Content, pipeline);
					var byteCount = Encoding.UTF8.GetBytes(renderedContent).Length;
					bookWithContent.Chapters.Add(new OutputChapters()
					{
						Label = chapter.Number,
						Number = chapter.Number,
						Content = renderedContent,
						ByteCount = byteCount
					});
					bookStringBuilder.Append(renderedContent);
					outputTasks.Add(output.WriteAllTextAsync(Path.Join(book.BookId, $"{chapter.Number}.html"), renderedContent));
					printStringBuilder.Append(renderedContent);
				}

				outputIndex.Bible.Add(outputBook);
				downloadIndex.Content.Add(bookWithContent);
				// Add whole.json for each chapter for book level fetching
				outputTasks.Add(output.WriteAllTextAsync(Path.Join(book.BookId, "whole.json"), JsonSerializer.Serialize(bookWithContent)));
			}
			outputTasks.Add(output.WriteAllTextAsync("index.json", JsonSerializer.Serialize(outputIndex)));

			// Add total bytes for someone to know how big the entire resource is
			long totalByteCount = downloadIndex.Content
					.SelectMany(outputBook => outputBook.Chapters)
					.Sum(chapter => chapter.ByteCount);
			outputIndex.ByteCount = totalByteCount;
			outputTasks.Add(output.WriteAllTextAsync("download.json", JsonSerializer.Serialize(downloadIndex)));

			foreach (var (title, article) in articles)
			{
				RewriteLinks(article);
				var tmpContent = article.ToHtml(pipeline);
				// Add articles to print copy
				printStringBuilder.Append(tmpContent);

				outputTasks.Add(output.WriteAllTextAsync($"{title}.html", tmpContent));
			}

			outputTasks.Add(output.WriteAllTextAsync("print_all.html", input.PrintTemplate.Render(Hash.FromAnonymousObject(new { content = printStringBuilder.ToString(), heading = input.Title }))));

			await Task.WhenAll(outputTasks);
		}

		/// <summary>
		/// Change the md links to popup:// links
		/// </summary>
		/// <param name="input"></param>
		/// <remarks>This modifies the input</remarks>
		private void RewriteLinks(MarkdownDocument input)
		{
			foreach (var (id, link) in GetLinks(input))
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

				output.Add((Path.GetFileNameWithoutExtension(link.Url), link));
			}
			return output;
		}

		private List<CommentaryBook> LoadMarkdownFiles(IZipFileSystem sourceDir, string basePath, Project[] projects)
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
		private Dictionary<string, MarkdownDocument> LoadArticles(IZipFileSystem sourceDir, string basePath)
		{
			var output = new Dictionary<string, MarkdownDocument>();
			var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
			foreach (var article in sourceDir.GetFiles(sourceDir.Join(basePath, "articles"), ".md"))
			{
				output.Add(Path.GetFileNameWithoutExtension(article), Markdown.Parse(sourceDir.ReadAllText(article), pipeline));
			}
			return output;
		}
		private IEnumerable<string> FilterAndOrderChapters(IEnumerable<string> input)
		{
			var chapters = input.ToList();
			var tmp = new List<(string fileName, string fileNameWithoutExtension, int Order)>(chapters.Count);
			foreach (var i in chapters)
			{
				var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(i);
				var tmpOrder = 0;
				if (fileNameWithoutExtension != "intro" && !int.TryParse(fileNameWithoutExtension, out tmpOrder))
				{
					continue;
				}
				tmp.Add((i, fileNameWithoutExtension, tmpOrder));
			}
			return tmp.OrderBy(i => i.Order).Select(i => i.fileName);
		}

	}
}
