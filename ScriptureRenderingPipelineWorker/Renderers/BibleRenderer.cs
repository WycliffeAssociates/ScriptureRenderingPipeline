using System.Text;
using System.Text.Json;
using BTTWriterLib;
using DotLiquid;
using PipelineCommon.Helpers;
using ScriptureRenderingPipelineWorker.Models;
using USFMToolsSharp;
using USFMToolsSharp.Models.Markers;
using USFMToolsSharp.Renderers.HTML;
using USFMToolsSharp.Renderers.USFM;

namespace ScriptureRenderingPipelineWorker.Renderers
{
	public class BibleRenderer: IRenderer
	{
		private static readonly string ChapterFormatString = "ch-{0}";

		/// <summary>
		/// Build scripture HTML files
		/// </summary>
		/// <param name="source">A ZipFileSystem to load the </param>
		/// <param name="basePath">The base path inside of the zip file to pull data from</param>
		/// <param name="destinationDir">Where to put the resulting HTML files</param>
		/// <param name="printTemplate">The template to apply to the printable page</param>
		/// <param name="repoUrl">The URL to inject into the template for the "See in WACS" link</param>
		/// <param name="heading">The heading for the template</param>
		/// <param name="languageName">The language name of the project</param>
		/// <param name="textDirection">The direction of the script being used (either rtl or ltr)</param>
		/// <param name="isBTTWriterProject">Whether or not this is a BTTWriter project</param>
		/// <param name="languageCode">The language code for the project</param>
		public async Task RenderAsync(RendererInput input, IOutputInterface output)
		{
			List<USFMDocument> documents;
			var downloadLinks = new List<DownloadLink>();
			if (input.IsBTTWriterProject)
			{
				documents = new List<USFMDocument>() {
					BTTWriterLoader.CreateUSFMDocumentFromContainer(new ZipFileSystemBTTWriterLoader(input.FileSystem, input.BasePath),false, new USFMParser(ignoreUnknownMarkers: true))
					};
				var renderer = new USFMRenderer();
				await output.WriteAllTextAsync("source.usfm", renderer.Render(documents[0]));
				downloadLinks.Add(new DownloadLink() { Link = "source.usfm", Title = "USFM" });
			}
			else
			{
				documents = await LoadDirectoryAsync(input.FileSystem);
			}

			// Order by abbreviation
			documents = documents.OrderBy(d => Utils.BibleBookOrder.Contains(d.GetChildMarkers<TOC3Marker>().FirstOrDefault()
						?.BookAbbreviation.ToUpper()) ? Utils.BibleBookOrder.IndexOf(d.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation.ToUpper())
						: 99).ToList();

			var lastRendered = System.DateTime.UtcNow.ToString("o");
			var printBuilder = new StringBuilder();
			var outputTasks = new List<Task>();
			var index = new OutputIndex()
			{
				TextDirection = input.LanguageTextDirection,
				RepoUrl = input.RepoUrl,
				LanguageCode = input.LanguageCode,
				LanguageName = input.LanguageName,
				ResourceTitle = input.Title,
				ResourceType = "bible",
				Bible = new List<OutputBook>(),
				DownloadLinks = downloadLinks,
				LastRendered = lastRendered,
				AppMeta = input.AppsMeta
			};

			var downloadIndex = new DownloadIndex()
			{
				LastRendered = lastRendered
			};

			foreach (var document in documents)
			{
				var renderer = new HtmlRenderer(new HTMLConfig() { partialHTML = true, ChapterIdPattern = ChapterFormatString });
				var abbreviation = document.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
				var title = document.GetChildMarkers<TOC2Marker>().FirstOrDefault()?.ShortTableOfContentsText ?? abbreviation;
				var content = renderer.Render(document);
				var chapters = document.GetChildMarkers<CMarker>();
				output.CreateDirectory(abbreviation);
				var outputBook = new OutputBook()
				{
					Slug = abbreviation,
					Label = title,
					LastRendered = lastRendered
				};
				var bookWithContent = new OutputBook()
				{
					Slug = abbreviation,
					Label = title,
					LastRendered = lastRendered
				};

				var alreadyWrittenChapters = new List<int>();
				foreach (var chapter in chapters)
				{
					// If we've already written this chapter then skip it
                    if (alreadyWrittenChapters.Contains(chapter.Number))
                    {
                        continue;
                    }

					var tmp = new USFMDocument();
					tmp.Insert(chapter);
					var renderedContent = renderer.Render(tmp);
					var byteCount = System.Text.Encoding.UTF8.GetBytes(renderedContent).Length;
					outputTasks.Add(output.WriteAllTextAsync(Path.Join(abbreviation, $"{chapter.Number.ToString()}.html"), renderedContent));
					outputBook.Chapters.Add(new OutputChapters()
					{
						Number = chapter.Number.ToString(),
						Label = chapter.PublishedChapterMarker,
						VerseCount = chapter.GetChildMarkers<VMarker>().Count()
					});
					bookWithContent.Chapters.Add(new OutputChapters()
					{
						Number = chapter.Number.ToString(),
						Label = chapter.PublishedChapterMarker,
						Content = renderedContent,
						ByteCount = byteCount
					});

                    alreadyWrittenChapters.Add(chapter.Number);
				}
				index.Bible.Add(outputBook);
				downloadIndex.Content.Add(bookWithContent);

				// Add whole.json for each chapter for book level fetching
				outputTasks.Add(output.WriteAllTextAsync(Path.Join(abbreviation, "whole.json"), JsonSerializer.Serialize(bookWithContent, WorkerJsonContext.Default.OutputBook)));


				// Since the print all page isn't going to broken up then just write stuff out here
				printBuilder.AppendLine(content);
			}
			long totalByteCount = downloadIndex.Content
		.SelectMany(outputBook => outputBook.Chapters)
		.Sum(chapter => chapter.ByteCount);
			index.ByteCount = totalByteCount;


			// If we have something then create the print_all.html page and the index.html page
			if (documents.Count > 0)
			{
				outputTasks.Add(output.WriteAllTextAsync("print_all.html", input.PrintTemplate.Render(Hash.FromAnonymousObject(new { content = printBuilder.ToString(), heading = input.Title }))));
			}
			outputTasks.Add(output.WriteAllTextAsync("index.json", JsonSerializer.Serialize(index, WorkerJsonContext.Default.OutputIndex)));
			outputTasks.Add(output.WriteAllTextAsync("download.json", JsonSerializer.Serialize(downloadIndex, WorkerJsonContext.Default.DownloadIndex)));

			await Task.WhenAll(outputTasks);
		}
		/// <summary>
		/// Load all USFM files in a directory inside of the ZipFileSystem
		/// </summary>
		/// <param name="directory">A ZipFileSystem to load from</param>
		/// <returns>A list of USFM files</returns>
		static async Task<List<USFMDocument>> LoadDirectoryAsync(IZipFileSystem directory)
		{
			USFMParser parser = new USFMParser(new List<string> { "s5" }, true);
			var output = new List<USFMDocument>();
			foreach (var f in directory.GetAllFiles(".usfm"))
			{
				var tmp = parser.ParseFromString(await directory.ReadAllTextAsync(f));
				// If we don't have an abbreviation then try to figure it out from the file name
				var tableOfContentsMarkers = tmp.GetChildMarkers<TOC3Marker>();
				if (tableOfContentsMarkers.Count == 0)
				{
					var bookAbbreviation = GetBookAbbreviationFromFileName(f);
					if (bookAbbreviation != null)
					{
						tmp.Insert(new TOC3Marker() { BookAbbreviation = bookAbbreviation });
					}
				}
				else if (Utils.GetBookNumber(tableOfContentsMarkers[0].BookAbbreviation) == 0)
				{
					var bookAbbreviation = GetBookAbbreviationFromFileName(f);
					if (bookAbbreviation != null)
					{
						tableOfContentsMarkers[0].BookAbbreviation = bookAbbreviation;
					}
				}
				output.Add(tmp);
			}
			return output;
		}

		private static string GetBookAbbreviationFromFileName(string f)
		{
			string bookAbbreviation = null;
			var fileNameSplit = Path.GetFileNameWithoutExtension(f).Split('-');
			if (fileNameSplit.Length == 2)
			{
				if (Utils.BibleBookOrder.Contains(fileNameSplit[1].ToUpper()))
				{
					bookAbbreviation = fileNameSplit[1].ToUpper();
				}
			}
			else if (fileNameSplit.Length == 1)
			{
				if (Utils.BibleBookOrder.Contains(fileNameSplit[0].ToUpper()))
				{
					bookAbbreviation = fileNameSplit[0].ToUpper();
				}
			}

			return bookAbbreviation;
		}
	}
}
