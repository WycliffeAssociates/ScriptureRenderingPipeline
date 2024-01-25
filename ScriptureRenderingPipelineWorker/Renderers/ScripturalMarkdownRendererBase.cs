using System.Text;
using System.Text.Json;
using DotLiquid;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PipelineCommon.Helpers;
using PipelineCommon.Helpers.MarkdigExtensions;
using ScriptureRenderingPipelineWorker.Models;

namespace ScriptureRenderingPipelineWorker.Renderers
{
	/// <summary>
	/// A base renderer for Markdown files that have a Book-Chapter-Verse format such as tn and tq
	/// </summary>
	public abstract class ScripturalMarkdownRendererBase: IRenderer
	{
		protected abstract string VerseFormatString { get; }
		protected abstract string ChapterFormatString { get; }
		protected abstract string ContentType { get; }
		protected abstract void BeforeVerse(StringBuilder builder, TranslationMaterialsBook book, TranslationMaterialsChapter chapter, TranslationMaterialsVerse verse);
		protected abstract void BeforeChapter(StringBuilder builder, TranslationMaterialsBook book, TranslationMaterialsChapter chapter);
		protected string BuildFileName(string bookName)
		{
			return $"{Utils.GetBookNumber(bookName):00}-{bookName.ToUpper()}.html";
		}
		protected IEnumerable<string> FilterAndOrderBooks(IEnumerable<string> input)
		{
			return input
					.Select(i => (book: i, order: Utils.BibleBookOrder.IndexOf(i.ToUpper())))
					.Where(i => i.order != -1)
					.OrderBy(i => i.order)
					.Select(i => i.book);
		}
		protected IEnumerable<string> FilterAndOrderChapters(IEnumerable<string> input)
		{
			return input.Where(i => i == "front" || int.TryParse(i, out _)).Select(i => (file: i, order: i == "front" ? 0 : int.Parse(i))).OrderBy(i => i.order).Select(i => i.file);
		}
		protected IEnumerable<string> OrderVerses(IEnumerable<string> input)
		{
			return input
					.Where(i => Path.GetFileName(i) == "intro.md" || int.TryParse(Path.GetFileNameWithoutExtension(i), out _))
					.Select(i => (book: i, index: Path.GetFileName(i) == "intro.md" ? 0 : int.Parse(Path.GetFileNameWithoutExtension(i))))
					.OrderBy(i => i.index)
					.Select(i => i.book);
		}
		protected string RewriteContentLink(string link, TranslationMaterialsBook currentBook, TranslationMaterialsChapter currentChapter)
		{
			var splitLink = link.Split("/");
			if (splitLink.Length == 1)
			{
				return BuildFileName(currentBook.BookId) + "#" + string.Format(VerseFormatString, currentBook.BookId, currentChapter.ChapterNumber, splitLink[0][..^3]);
			}

			if (splitLink[0] == ".")
			{
				if (splitLink.Length == 2)
				{
					return BuildFileName(currentBook.BookId) + "#" + string.Format(VerseFormatString, currentBook.BookId, currentChapter.ChapterNumber, splitLink[1][..^3]);
				}
			}
			else if (splitLink[0] == "..")
			{
				if (splitLink.Length == 3)
				{
					return BuildFileName(currentBook.BookId) + "#" + string.Format(VerseFormatString, currentBook.BookId, splitLink[1], splitLink[2][..^3]);
				}
				if (splitLink.Length == 5 && splitLink[1] == "..")
				{
					return BuildFileName(splitLink[2]) + "#" + string.Format(VerseFormatString, splitLink[2], splitLink[3], splitLink[4][..^3]);
				}
			}
			return link;
		}

		protected virtual async Task<List<TranslationMaterialsBook>> LoadMarkDownFilesAsync(IZipFileSystem fileSystem,
				string basePath, string baseUrl, string userToRouteResourcesTo, string languageCode)
		{
			RCLinkOptions options = new RCLinkOptions()
			{
				BaseUser = userToRouteResourcesTo,
				ServerUrl = baseUrl,
				LanguageCode = languageCode

			};
			var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Use(new RCLinkExtension(options)).Build();
			var output = new List<TranslationMaterialsBook>();

			foreach (var book in FilterAndOrderBooks(fileSystem.GetFolders(basePath)))
			{
				var tnBook = new TranslationMaterialsBook()
				{
					FileName = BuildFileName(book),
					BookId = book,
					BookName = Utils.bookAbbreviationMappingToEnglish.ContainsKey(book.ToUpper()) ? Utils.bookAbbreviationMappingToEnglish[book.ToUpper()] : book,
				};

				var chapters = fileSystem.GetFolders(fileSystem.Join(basePath, book));
				foreach (var chapter in FilterAndOrderChapters(chapters))
				{
					var tnChapter = new TranslationMaterialsChapter(chapter);
					foreach (var file in OrderVerses(fileSystem.GetFiles(fileSystem.Join(basePath, book, chapter), ".md")))
					{
						var parsedVerse = Markdown.Parse(await fileSystem.ReadAllTextAsync(file), pipeline);

						// adjust the heading blocks up one level so I can put in chapter and verse sections as H1
						IncreaseHeadingBlocks(parsedVerse);

						RewriteLinks(parsedVerse, tnBook, tnChapter);

						var tnVerse = new TranslationMaterialsVerse(Path.GetFileNameWithoutExtension(file), parsedVerse.ToHtml(pipeline));
						tnChapter.Verses.Add(tnVerse);
					}
					tnBook.Chapters.Add(tnChapter);
				}
				output.Add(tnBook);
			}
			return output;
		}

		private void RewriteLinks(MarkdownDocument parsedVerse, TranslationMaterialsBook tnBook,
			TranslationMaterialsChapter tnChapter)
		{
			foreach (var link in parsedVerse.Descendants<LinkInline>())
			{
				if (link.Url != null && link.Url.EndsWith(".md"))
				{
					link.Url = RewriteContentLink(link.Url, tnBook, tnChapter);
				}
			}
		}

		private static void IncreaseHeadingBlocks(MarkdownDocument parsedVerse)
		{
			foreach (var headingBlock in parsedVerse.Descendants<HeadingBlock>())
			{
				headingBlock.Level++;
			}
		}

		public virtual async Task RenderAsync(RendererInput input, IOutputInterface output)
		{
			var books = await LoadMarkDownFilesAsync(input.FileSystem, input.BasePath, input.BaseUrl, input.UserToRouteResourcesTo, input.LanguageCode);
			var printBuilder = new StringBuilder();
			var outputTasks = new List<Task>();
			var outputWrapper = new OutputAndLoggingWrapper(output, input.Logger);
			var lastRendered = System.DateTime.UtcNow.ToString("o");
			var outputIndex = new OutputIndex()
			{
				LanguageCode = input.LanguageCode,
				TextDirection = input.LanguageTextDirection,
				RepoUrl = input.RepoUrl,
				LanguageName = input.LanguageName,
				ResourceType = ContentType,
				ResourceTitle = input.Title,
				Bible = new List<OutputBook>(),
				LastRendered = lastRendered,
				AppMeta = input.AppsMeta
			};
			var downloadIndex = new DownloadIndex()
			{
				LastRendered = lastRendered
			};
			foreach (var book in books)
			{
				var outputBook = new OutputBook()
				{
					Label = book.BookName,
					Slug = book.BookId,
					LastRendered = lastRendered
				};
				var bookWithContent = new OutputBook()
				{
					Label = book.BookName,
					Slug = book.BookId,
					LastRendered = lastRendered
				};
				outputWrapper.LogTitle(book.BookId, book.BookName);
				foreach (var chapter in book.Chapters)
				{
					var builder = new StringBuilder();
					BeforeChapter(builder, book, chapter);
					foreach (var verse in chapter.Verses)
					{
						BeforeVerse(builder, book, chapter, verse);
						builder.AppendLine(verse.HtmlContent);
					}
					var builderContent = builder.ToString();
					var byteCount = System.Text.Encoding.UTF8.GetBytes(builderContent).Length;
					output.CreateDirectory(book.BookId);
					outputTasks.Add(outputWrapper.WriteAllTextAsync(Path.Join(book.BookId, $"{chapter.ChapterNumber}.html"), builderContent));
					printBuilder.Append(builder);
					outputBook.Chapters.Add(new OutputChapters()
					{
						Label = chapter.ChapterNumber,
						Number = chapter.ChapterNumber,
					});
					bookWithContent.Chapters.Add(new OutputChapters()
					{
						Label = chapter.ChapterNumber,
						Number = chapter.ChapterNumber,
						Content = builderContent,
						ByteCount = byteCount
					});

				}
				outputIndex.Bible.Add(outputBook);
				downloadIndex.Content.Add(bookWithContent);
				// Add whole.json for each chapter for book level fetching
				outputTasks.Add(outputWrapper.WriteAllTextAsync(Path.Join(book.BookId, "whole.json"), JsonSerializer.Serialize(bookWithContent, WorkerJsonContext.Default.OutputBook)));

			}
			long totalByteCount = downloadIndex.Content
				.SelectMany(outputBook => outputBook.Chapters)
				.Sum(chapter => chapter.ByteCount);
			outputIndex.ByteCount = totalByteCount;

			outputTasks.Add(outputWrapper.WriteAllTextAsync("index.json", JsonSerializer.Serialize(outputIndex, WorkerJsonContext.Default.OutputIndex)));
			outputTasks.Add(outputWrapper.WriteAllTextAsync("download.json", JsonSerializer.Serialize(downloadIndex, WorkerJsonContext.Default.DownloadIndex)));


			if (books.Count > 0)
			{
				outputTasks.Add(outputWrapper.WriteAllTextAsync("print_all.html", input.PrintTemplate.Render(Hash.FromAnonymousObject(new { content = printBuilder.ToString(), input.Title }))));
			}
			
			outputTasks.Add(outputWrapper.FinishAsync());

			await Task.WhenAll(outputTasks);
		}
	}
}