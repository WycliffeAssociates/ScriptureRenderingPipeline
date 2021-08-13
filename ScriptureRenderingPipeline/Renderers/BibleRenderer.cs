﻿using BTTWriterLib;
using DotLiquid;
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
        public void Render(string sourceDir, string destinationDir, Template template, string repoUrl, string heading, bool isBTTWriterProject = false)
        {
            List<USFMDocument> documents;
            if (isBTTWriterProject)
            {
                documents = new List<USFMDocument>() { BTTWriterLoader.CreateUSFMDocumentFromContainer(new FileSystemResourceContainer(sourceDir), false) };
            }
            else
            {
                documents = LoadDirectory(sourceDir);
            }
            documents.OrderBy(d => Utils.BibleBookOrder.Contains(d.GetChildMarkers<TOC3Marker>().FirstOrDefault()
                ?.BookAbbreviation.ToUpper()) ? Utils.BibleBookOrder.IndexOf(d.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation.ToUpper())
                : 99);
            var navigation = BuildNavigation(documents);
            Parallel.ForEach(documents, (document) =>
            {
                HtmlRenderer renderer = new HtmlRenderer(new HTMLConfig() { partialHTML = true, ChapterIdPattern = "ch-{0}" });
                var abbreviation = document.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
                var templateResult = template.Render(Hash.FromAnonymousObject(new
                {
                    content = renderer.Render(document),
                    scriptureNavigation = navigation,
                    contenttype = "bible",
                    currentBook = abbreviation,
                    heading,
                    sourceLink = repoUrl
                }
                ));
                File.WriteAllText($"{destinationDir}/{BuildFileName(abbreviation)}", templateResult);
            });

            File.Copy($"{destinationDir}/{BuildFileName(documents[0])}",$"{destinationDir}/index.html");
        }
        static List<USFMDocument> LoadDirectory(string directory)
        {
            USFMParser parser = new USFMParser(new List<string> { "s5" });
            var output = new List<USFMDocument>();
            foreach (var f in Directory.EnumerateFiles(directory, "*.usfm", SearchOption.AllDirectories))
            {
                var tmp = parser.ParseFromString(File.ReadAllText(f));
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
            return $"{Utils.BibleBookOrder.IndexOf(abbreviation.ToUpper()):00}-{abbreviation.ToUpper()}.html";
        }
        static List<NavigationBook> BuildNavigation(List<USFMDocument> documents)
        {
            var output = new List<NavigationBook>();
            foreach(var doc in documents)
            {
                var abbreviation = doc.GetChildMarkers<TOC3Marker>().FirstOrDefault()?.BookAbbreviation;
                var fileName = $"{Utils.BibleBookOrder.IndexOf(abbreviation.ToUpper()):00}-{abbreviation.ToUpper()}";
                var title = doc.GetChildMarkers<TOC2Marker>().FirstOrDefault()?.ShortTableOfContentsText;
                output.Add(new NavigationBook()
                {
                    abbreviation = abbreviation,
                    title = title,
                    file = BuildFileName(abbreviation),
                    chapters = doc.GetChildMarkers<CMarker>()
                    .OrderBy(c => c.Number)
                    .Select(i => new NavigationChapter() { number = i.Number.ToString(), title = i.PublishedChapterMarker})
                    .ToList()
                });
            }
            return output;
        }
    }
}
