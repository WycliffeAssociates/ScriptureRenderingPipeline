using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DotLiquid;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PipelineCommon.Helpers;
using PipelineCommon.Helpers.MarkdigExtensions;
using ScriptureRenderingPipeline.Models;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScriptureRenderingPipeline.Renderers
{
	/// <summary>
	/// A base renderer for Markdown files that have a Book-Chapter-Verse format such as tn and tq
	/// </summary>
	public abstract class ScripturalMarkdownRendererBase
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
		protected string RewriteContentLinks(string link, TranslationMaterialsBook currentBook, TranslationMaterialsChapter currentChapter)
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
				else if (splitLink.Length == 4)
				{
					return BuildFileName(currentBook.BookId) + "#" + string.Format(VerseFormatString, splitLink[1], splitLink[2], splitLink[3][..^3]);
				}
			}
			return link;
		}

		protected List<NavigationBook> BuildNavigation(List<TranslationMaterialsBook> input)
		{
			var output = new List<NavigationBook>();
			foreach (var book in input)
			{
				var navBook = new NavigationBook() { abbreviation = book.BookId, file = book.FileName, title = book.BookName };
				foreach (var chapter in book.Chapters)
				{
					// Remove leading zeros from chapter
					string printableChapterNumber = chapter.ChapterNumber.TrimStart('0');
					navBook.chapters.Add(new NavigationChapter() { id = string.Format(ChapterFormatString, book.BookId, chapter.ChapterNumber), title = printableChapterNumber });
				}
				output.Add(navBook);
			}
			return output;
		}

		protected virtual async Task<List<TranslationMaterialsBook>> LoadMarkDownFilesAsync(ZipFileSystem fileSystem,
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
						foreach (var headingBlock in parsedVerse.Descendants<HeadingBlock>())
						{
							headingBlock.Level++;
						}

						foreach (var link in parsedVerse.Descendants<LinkInline>())
						{
							if (link.Url == null)
							{
								continue;
							}
							if (link.Url != null && link.Url.EndsWith(".md"))
							{
								link.Url = RewriteContentLinks(link.Url, tnBook, tnChapter);
							}
						}

						var tnVerse = new TranslationMaterialsVerse(Path.GetFileNameWithoutExtension(file), parsedVerse.ToHtml(pipeline));
						tnChapter.Verses.Add(tnVerse);
					}
					tnBook.Chapters.Add(tnChapter);
				}
				output.Add(tnBook);
			}
			return output;
		}
		public virtual async Task RenderAsync(ZipFileSystem sourceDir, string basePath, string destinationDir,
				 Template printTemplate, string repoUrl, string heading, string baseUrl,
				string userToRouteResourcesTo, string textDirection, string languageCode, string languageName, bool isBTTWriterProject = false)
		{
			var books = await LoadMarkDownFilesAsync(sourceDir, basePath, baseUrl, userToRouteResourcesTo, languageCode);
			var printBuilder = new StringBuilder();
			var outputTasks = new List<Task>();
			var outputIndex = new OutputIndex()
			{
				LanguageCode = languageCode,
				TextDirection = textDirection,
				RepoUrl = repoUrl,
				LanguageName = languageName,
				ResourceType = ContentType,
				ResourceTitle = heading,
				Bible = new List<OutputBook>(),
			};
			var downloadIndex = new DownloadIndex();
			foreach (var book in books)
			{
				var outputBook = new OutputBook()
				{
					Label = book.BookName,
					Slug = book.BookId
				};
				var bookWithContent = new OutputBook()
				{
					Slug = book.BookName,
					Label = book.BookId
				};
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
					Directory.CreateDirectory(Path.Join(destinationDir, book.BookId));
					outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, book.BookId, $"{chapter.ChapterNumber}.html"), builderContent));
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
				outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, book.BookId, "whole.json"), JsonSerializer.Serialize(bookWithContent)));


			}
			long totalByteCount = downloadIndex.Content
				.SelectMany(outputBook => outputBook.Chapters)
				.Sum(chapter => chapter.ByteCount);
			downloadIndex.ByteCount = totalByteCount;

			outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "index.json"), JsonSerializer.Serialize(outputIndex)));
			outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "download.json"), JsonSerializer.Serialize(downloadIndex)));


			if (books.Count > 0)
			{
				outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "print_all.html"), printTemplate.Render(Hash.FromAnonymousObject(new { content = printBuilder.ToString(), heading }))));
			}

			await Task.WhenAll(outputTasks);
		}
	}
}