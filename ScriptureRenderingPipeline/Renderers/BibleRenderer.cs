using BTTWriterLib;
using DotLiquid;
using PipelineCommon.Helpers;
using ScriptureRenderingPipeline.Helpers;
using ScriptureRenderingPipeline.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using USFMToolsSharp;
using USFMToolsSharp.Models.Markers;
using USFMToolsSharp.Renderers.HTML;
using USFMToolsSharp.Renderers.USFM;

namespace ScriptureRenderingPipeline.Renderers
{
	public static class BibleRenderer
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
		public static async Task RenderAsync(ZipFileSystem source, string basePath, string destinationDir, Template printTemplate, string repoUrl, string heading, string languageCode, string languageName, string textDirection, bool isBTTWriterProject = false)
		{
			List<USFMDocument> documents;
			var downloadLinks = new List<DownloadLink>();
			if (isBTTWriterProject)
			{
				documents = new List<USFMDocument>() {
					BTTWriterLoader.CreateUSFMDocumentFromContainer(new ZipFileSystemBTTWriterLoader(source, basePath),false, new USFMParser(ignoreUnknownMarkers: true))
					};
				USFMRenderer renderer = new USFMRenderer();
				await File.WriteAllTextAsync(Path.Join(destinationDir, "source.usfm"), renderer.Render(documents[0]));
				downloadLinks.Add(new DownloadLink() { Link = "source.usfm", Title = "USFM" });
			}
			else
			{
				documents = await LoadDirectoryAsync(source);
			}

			// Order by abbreviation
			documents = documents.OrderBy(d => Utils.BibleBookOrder.Contains(d.GetChildMarkers<TOC3Marker>().FirstOrDefault()
						?.BookAbbreviation.ToUpper()) ? Utils.BibleBookOrder.IndexOf(d.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation.ToUpper())
						: 99).ToList();

			var printBuilder = new StringBuilder();
			var outputTasks = new List<Task>();
			var index = new OutputIndex()
			{
				TextDirection = textDirection,
				RepoUrl = repoUrl,
				LanguageCode = languageCode,
				LanguageName = languageName,
				ResourceType = "bible",
				Bible = new List<OutputBook>(),
				DownloadLinks = downloadLinks
			};
			var downloadIndex = new DownloadIndex();

			foreach (var document in documents)
			{
				var renderer = new HtmlRenderer(new HTMLConfig() { partialHTML = true, ChapterIdPattern = ChapterFormatString });
				var abbreviation = document.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
				var title = document.GetChildMarkers<TOC2Marker>().FirstOrDefault()?.ShortTableOfContentsText ?? abbreviation;
				var content = renderer.Render(document);
				var chapters = document.GetChildMarkers<CMarker>();
				Directory.CreateDirectory(Path.Join(destinationDir, abbreviation));
				var outputBook = new OutputBook()
				{
					Slug = abbreviation,
					Label = title
				};
				var bookWithContent = new OutputBook()
				{
					Slug = abbreviation,
					Label = title
				};
				foreach (var chapter in chapters)
				{
					var tmp = new USFMDocument();
					tmp.Insert(chapter);
					var renderedContent = renderer.Render(tmp);
					var byteCount = System.Text.Encoding.UTF8.GetBytes(renderedContent).Length;
					outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, abbreviation, $"{chapter.Number.ToString()}.html"), renderedContent));
					outputBook.Chapters.Add(new OutputChapters()
					{
						Number = chapter.Number.ToString(),
						Label = chapter.PublishedChapterMarker
					});
					bookWithContent.Chapters.Add(new OutputChapters()
					{
						Number = chapter.Number.ToString(),
						Label = chapter.PublishedChapterMarker,
						Content = renderedContent,
						ByteCount = byteCount
					});
				}
				index.Bible.Add(outputBook);
				downloadIndex.Content.Add(bookWithContent);
				// Add whole.json for each chapter for book level fetching
				outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, abbreviation, "whole.json"), JsonSerializer.Serialize(bookWithContent)));


				// Since the print all page isn't going to broken up then just write stuff out here
				printBuilder.AppendLine(content);
			}
			long totalByteCount = downloadIndex.Content
		.SelectMany(outputBook => outputBook.Chapters)
		.Sum(chapter => chapter.ByteCount);
			downloadIndex.ByteCount = totalByteCount;

			// If we have something then create the print_all.html page and the index.html page
			if (documents.Count > 0)
			{
				outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "print_all.html"), printTemplate.Render(Hash.FromAnonymousObject(new { content = printBuilder.ToString(), heading }))));
			}
			outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "index.json"), JsonSerializer.Serialize(index)));
			outputTasks.Add(File.WriteAllTextAsync(Path.Join(destinationDir, "download.json"), JsonSerializer.Serialize(downloadIndex)));

			await Task.WhenAll(outputTasks);
		}
		/// <summary>
		/// Load all USFM files in a directory inside of the ZipFileSystem
		/// </summary>
		/// <param name="directory">A ZipFileSystem to load from</param>
		/// <returns>A list of USFM files</returns>
		static async Task<List<USFMDocument>> LoadDirectoryAsync(ZipFileSystem directory)
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
					var bookAbbreviation = GetBookAbberviationFromFileName(f);
					if (bookAbbreviation != null)
					{
						tmp.Insert(new TOC3Marker() { BookAbbreviation = bookAbbreviation });
					}
				}
				else if (Utils.GetBookNumber(tableOfContentsMarkers[0].BookAbbreviation) == 0)
				{
					var bookAbbreviation = GetBookAbberviationFromFileName(f);
					if (bookAbbreviation != null)
					{
						tableOfContentsMarkers[0].BookAbbreviation = bookAbbreviation;
					}
				}
				output.Add(tmp);
			}
			return output;
		}

		private static string GetBookAbberviationFromFileName(string f)
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
