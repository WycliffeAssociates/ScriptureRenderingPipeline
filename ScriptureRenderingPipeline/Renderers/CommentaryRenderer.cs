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

namespace ScriptureRenderingPipeline.Renderers
{
	internal class CommentaryRenderer
	{
		const string ChapterIdFormat = "chapter-{0}";

		public async Task RenderAsync(ZipFileSystem sourceDir, string basePath, string destinationDir, Template template, Template printTemplate, string repoUrl, string heading, ResourceContainer resourceContainer, string baseUrl, string userToRouteResourcesTo, string textDirection, string languageName, string languageCode, bool isBTTWriterProject = false)
		{
			var content = LoadMarkdownFiles(sourceDir, basePath, resourceContainer.projects);
			var articles = LoadArticles(sourceDir, basePath);
			var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
			var outputTasks = new List<Task>();
			var printStringBuilder = new StringBuilder();
			var outputIndex = new OutputIndex()
			{
				TextDirection = textDirection,
				LanguageCode = languageCode,
				LanguageName = languageName,
				ResourceType = "commentary",
				ResourceTitle = heading,
				RepoUrl = repoUrl,
				Bible = new List<OutputBook>(),
				Navigation = null,
			};
			var downloadIndex = new DownloadIndex();
			foreach (var book in content)
			{
				var bookStringBuilder = new StringBuilder();
				if (!Directory.Exists(Path.Join(destinationDir, book.BookId)))
				{
					Directory.CreateDirectory(Path.Join(destinationDir, book.BookId));
				}

				var outputBook = new OutputBook()
				{
					Label = book.Title,
					Slug = book.BookId
				};
				var bookWithContent = new OutputBook()
				{
					Label = book.Title,
					Slug = book.BookId
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
					var byteCount = System.Text.Encoding.UTF8.GetBytes(renderedContent).Length;
					bookWithContent.Chapters.Add(new OutputChapters()
					{
						Label = chapter.Number,
						Number = chapter.Number,
						Content = renderedContent,
						ByteCount = byteCount
					});
					bookStringBuilder.Append(renderedContent);
					outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, book.BookId, $"{chapter.Number}.html"), renderedContent));
					printStringBuilder.Append(renderedContent);
				}

				outputIndex.Bible.Add(outputBook);
				downloadIndex.Content.Add(bookWithContent);
				// Add whole.json for each chapter for book level fetching
				outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, book.BookId, "whole.json"), JsonSerializer.Serialize(bookWithContent)));
			}

			outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "index.json"), JsonSerializer.Serialize(outputIndex)));

			// Add total bytes for someone to know how big the entire resource is
			long totalByteCount = downloadIndex.Content
					.SelectMany(outputBook => outputBook.Chapters)
					.Sum(chapter => chapter.ByteCount);
			downloadIndex.ByteCount = totalByteCount;
			outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "download.json"), JsonSerializer.Serialize(downloadIndex)));

			foreach (var (title, article) in articles)
			{
				RewriteLinks(article);
				var tmpContent = Markdown.ToHtml(article, pipeline);
				// Add articles to print copy
				printStringBuilder.Append(tmpContent);

				outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, $"{title}.html"), tmpContent));
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
		private Dictionary<string, MarkdownDocument> LoadArticles(ZipFileSystem sourceDir, string basePath)
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
