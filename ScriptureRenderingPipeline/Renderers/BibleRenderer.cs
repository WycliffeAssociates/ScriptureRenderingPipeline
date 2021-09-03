using BTTWriterLib;
using DotLiquid;
using ScriptureRenderingPipeline.Helpers;
using ScriptureRenderingPipeline.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using USFMToolsSharp;
using USFMToolsSharp.Models.Markers;
using USFMToolsSharp.Renderers.HTML;

namespace ScriptureRenderingPipeline.Renderers
{
    public class BibleRenderer
    {
        private static readonly string ChapterFormatString = "ch-{0}";
        public void Render(ZipFileSystem source, string basePath, string destinationDir, Template template, Template printTemplate, string repoUrl, string heading, bool isBTTWriterProject = false)
        {
            List<USFMDocument> documents;
            if (isBTTWriterProject)
            {
                documents = new List<USFMDocument>() { BTTWriterLoader.CreateUSFMDocumentFromContainer(new ZipFileSystemBTTWriterLoader(source, basePath),false) };
            }
            else
            {
                documents = LoadDirectory(source);
            }
            documents.OrderBy(d => Utils.BibleBookOrder.Contains(d.GetChildMarkers<TOC3Marker>().FirstOrDefault()
                ?.BookAbbreviation.ToUpper()) ? Utils.BibleBookOrder.IndexOf(d.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation.ToUpper())
                : 99);
            var navigation = BuildNavigation(documents);
            var printBuilder = new StringBuilder();
            foreach(var document in documents)
            {
                HtmlRenderer renderer = new HtmlRenderer(new HTMLConfig() { partialHTML = true, ChapterIdPattern = ChapterFormatString });
                var abbreviation = document.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
                var content = renderer.Render(document);
                var templateResult = template.Render(Hash.FromAnonymousObject(new
                {
                    content = content,
                    scriptureNavigation = navigation,
                    contenttype = "bible",
                    currentBook = abbreviation,
                    heading,
                    sourceLink = repoUrl
                }
                ));
                printBuilder.AppendLine(content);
                File.WriteAllText($"{destinationDir}/{BuildFileName(abbreviation)}", templateResult);
            }

            if (documents.Count > 0)
            {
                File.Copy($"{destinationDir}/{BuildFileName(documents[0])}",$"{destinationDir}/index.html");
                File.WriteAllText(Path.Join(destinationDir, "print_all.html"), printTemplate.Render(Hash.FromAnonymousObject(new { content = printBuilder.ToString(), heading })));
            }
        }
        static List<USFMDocument> LoadDirectory(ZipFileSystem directory)
        {
            USFMParser parser = new USFMParser(new List<string> { "s5" });
            var output = new List<USFMDocument>();
            foreach (var f in directory.GetAllFiles(".usfm"))
            {
                var tmp = parser.ParseFromString(directory.ReadAllText(f));
                output.Add(tmp);
            }
            return output;
        }
        static string BuildFileName(USFMDocument document)
        {
            var abbreviation = document.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
            return BuildFileName(abbreviation);
        }
        static string BuildFileName(string abbreviation)
        {
            return $"{Utils.GetBookNumber(abbreviation):00}-{abbreviation.ToUpper()}.html";
        }
        static List<NavigationBook> BuildNavigation(List<USFMDocument> documents)
        {
            var output = new List<NavigationBook>();
            foreach(var doc in documents)
            {
                var abbreviation = doc.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
                var fileName = $"{Utils.GetBookNumber(abbreviation):00}-{abbreviation.ToUpper()}";
                var title = doc.GetChildMarkers<TOC2Marker>().FirstOrDefault()?.ShortTableOfContentsText;
                output.Add(new NavigationBook()
                {
                    abbreviation = abbreviation,
                    title = title,
                    file = BuildFileName(abbreviation),
                    chapters = doc.GetChildMarkers<CMarker>()
                    .OrderBy(c => c.Number)
                    .Select(i => new NavigationChapter() { id = string.Format(ChapterFormatString, i.Number.ToString()), title = i.PublishedChapterMarker})
                    .ToList()
                });
            }
            return output;
        }
    }
}
