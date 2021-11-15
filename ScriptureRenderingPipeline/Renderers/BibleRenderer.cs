using BTTWriterLib;
using DotLiquid;
using PipelineCommon.Helpers;
using ScriptureRenderingPipeline.Helpers;
using ScriptureRenderingPipeline.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using USFMToolsSharp;
using USFMToolsSharp.Models.Markers;
using USFMToolsSharp.Renderers.HTML;
using USFMToolsSharp.Renderers.USFM;

namespace ScriptureRenderingPipeline.Renderers
{
    public class BibleRenderer
    {
        private static readonly string ChapterFormatString = "ch-{0}";

        /// <summary>
        /// Build scripture HTML files
        /// </summary>
        /// <param name="source">A ZipFileSystem to load the </param>
        /// <param name="basePath">The base path inside of the zip file to pull data from</param>
        /// <param name="destinationDir">Where to put the resulting HTML files</param>
        /// <param name="template">The template to apply to the scripture files</param>
        /// <param name="printTemplate">The template to apply to the printable page</param>
        /// <param name="repoUrl">The URL to inject into the template for the "See in WACS" link</param>
        /// <param name="heading">The heading for the template</param>
        /// <param name="textDirection">The direction of the script being used (either rtl or ltr)</param>
        /// <param name="isBTTWriterProject">Whether or not this is a BTTWriter project</param>
        public void Render(ZipFileSystem source, string basePath, string destinationDir, Template template, Template printTemplate, string repoUrl, string heading, string textDirection, bool isBTTWriterProject = false)
        {
            List<USFMDocument> documents;
            var downloadLinks = new List<DownloadLink>();
            if (isBTTWriterProject)
            {
                documents = new List<USFMDocument>() { 
                    BTTWriterLoader.CreateUSFMDocumentFromContainer(new ZipFileSystemBTTWriterLoader(source, basePath),false, new USFMParser(ignoreUnknownMarkers: true)) 
                    };
                USFMRenderer renderer = new USFMRenderer();
                File.WriteAllText(Path.Join(destinationDir, "source.usfm"), renderer.Render(documents[0]));
                downloadLinks.Add(new DownloadLink(){Link = "source.usfm", Title = "USFM"});
            }
            else
            {
                documents = LoadDirectory(source);
            }

            // Order by abbreviation
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
                var templateResult = template.Render(Hash.FromDictionary(new Dictionary<string,object>()
                {
                    ["content"] = content,
                    ["scriptureNavigation"] = navigation,
                    ["contenttype"] = "bible",
                    ["currentBook"] = abbreviation,
                    ["heading"] = heading,
                    ["sourceLink"] = repoUrl,
                    ["textDirection"] = textDirection,
                    ["additionalDownloadLinks"] = downloadLinks
                }
                ));


                // Since the print all page isn't going to broken up then just write stuff out here
                printBuilder.AppendLine(content);
                File.WriteAllText($"{destinationDir}/{BuildFileName(abbreviation)}", templateResult);
            }

            // If we have something then create the print_all.html page and the index.html page
            if (documents.Count > 0)
            {
                File.Copy($"{destinationDir}/{BuildFileName(documents[0])}",$"{destinationDir}/index.html");
                File.WriteAllText(Path.Join(destinationDir, "print_all.html"), printTemplate.Render(Hash.FromAnonymousObject(new { content = printBuilder.ToString(), heading })));
            }
        }
        /// <summary>
        /// Load all USFM files in a directory inside of the ZipFileSystem
        /// </summary>
        /// <param name="directory">A ZipFileSystem to load from</param>
        /// <returns>A list of USFM files</returns>
        static List<USFMDocument> LoadDirectory(ZipFileSystem directory)
        {
            USFMParser parser = new USFMParser(new List<string> { "s5" }, true);
            var output = new List<USFMDocument>();
            foreach (var f in directory.GetAllFiles(".usfm"))
            {
                var tmp = parser.ParseFromString(directory.ReadAllText(f));
                // If we don't have an abberviation then try to figure it out from the file name
                if(tmp.GetChildMarkers<TOC3Marker>().Count == 0)
                {
                    var fileNameSplit = Path.GetFileNameWithoutExtension(f).Split('-');
                    string bookAbbrivation = null;
                    if (fileNameSplit.Length == 2)
                    {
                        if (Utils.BibleBookOrder.Contains(fileNameSplit[1].ToUpper()))
                        {
                            bookAbbrivation = fileNameSplit[1].ToUpper();
                        }
                    }
                    else if (fileNameSplit.Length == 1)
                    {
                        if (Utils.BibleBookOrder.Contains(fileNameSplit[0].ToUpper()))
                        {
                            bookAbbrivation = fileNameSplit[0].ToUpper();
                        }
                    }
                    if (bookAbbrivation != null)
                    {
                        tmp.Insert(new TOC3Marker() { BookAbbreviation = bookAbbrivation });
                    }
                }
                output.Add(tmp);
            }
            return output;
        }
        /// <summary>
        /// Build the filename for this document based on the contents of a USFM document
        /// </summary>
        /// <param name="document">The document to base the name off of</param>
        /// <returns>The file name in a format of {booknumber}-{abberviation}.html</returns>
        static string BuildFileName(USFMDocument document)
        {
            var abbreviation = document.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
            return BuildFileName(abbreviation);
        }

        /// <summary>
        /// Builds a filename for this document based on an abberviation
        /// </summary>
        /// <param name="abbreviation">The abbreviation to base this off of</param>
        /// <returns>The file name in a format of {booknumber}-{abberviation}.html</returns>
        static string BuildFileName(string abbreviation)
        {
            return $"{Utils.GetBookNumber(abbreviation):00}-{abbreviation.ToUpper()}.html";
        }

        /// <summary>
        /// Build navigation for the scripture document based on the contents of a List of USFM documents
        /// </summary>
        /// <param name="documents">Our content to build the navigation off of</param>
        /// <returns>A list of NavigationBooks which defines our navigation</returns>
        static List<NavigationBook> BuildNavigation(List<USFMDocument> documents)
        {
            var output = new List<NavigationBook>();
            foreach(var doc in documents)
            {
                var abbreviation = doc.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
                var fileName = $"{Utils.GetBookNumber(abbreviation):00}-{abbreviation.ToUpper()}";
                var title = doc.GetChildMarkers<TOC2Marker>().FirstOrDefault()?.ShortTableOfContentsText ?? abbreviation;
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
