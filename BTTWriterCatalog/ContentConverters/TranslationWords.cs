using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.OutputFormats;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BTTWriterCatalog.ContentConverters
{
    public class TranslationWords
    {
        public static void Convert(ZipFileSystem fileSystem, string basePath, string outputPath, ResourceContainer resourceContainer, ILogger log)
        {
            var projectPath = resourceContainer.projects[0].path;
            var words = LoadWords(fileSystem, fileSystem.Join(basePath, projectPath), log);
            File.WriteAllText(Path.Join(outputPath, "words.json"), JsonConvert.SerializeObject(words));
        }
        public static List<TranslationWord> LoadWords(ZipFileSystem sourceDir, string basePath, ILogger log)
        {
            MarkdownPipeline markdownPipeline = new MarkdownPipelineBuilder().Build();
            var output = new List<TranslationWord>();
            foreach( var dir in sourceDir.GetFolders(basePath))
            {
                if (Utils.TranslationWordsValidSections.Contains(dir))
                {
                    foreach(var file in sourceDir.GetFiles(sourceDir.Join(basePath, dir),".md"))
                    {
                        var slug = Path.GetFileNameWithoutExtension(file);
                        var content = Markdown.Parse(sourceDir.ReadAllText(file));
                        var headings = content.Descendants<HeadingBlock>().ToList();
                        var titleHeading = headings.FirstOrDefault(h => h.Level == 1);
                        if (titleHeading == null)
                        {
                            log.LogWarning("Missing title in {slug}", slug);
                        }
                        else
                        {
                            content.Remove(titleHeading);
                        }
                        var definitionTitleHeading = headings.FirstOrDefault( h=> h.Level == 2);
                        if (definitionTitleHeading == null)
                        {
                            log.LogWarning("Missing definition title in {slug}", slug);
                        }
                        else
                        {
                            content.Remove(definitionTitleHeading);
                        }

                        var links = content.Descendants<LinkInline>();
                        var relatedWords = GetRelatedWords(links);

                        // TODO: Get OBS examples and maybe scripture

                        var outputWord = new TranslationWord()
                        {
                            Definition = content.ToHtml(markdownPipeline),
                            RelatedWords = relatedWords,
                            WordId = slug,
                        };
                        if (definitionTitleHeading != null)
                        {
                            outputWord.DefinitionTitle = GetTitleHeading(definitionTitleHeading).TrimEnd(':');
                        }
                        if (titleHeading != null)
                        {
                            var title = GetTitleHeading(titleHeading);
                            outputWord.Term = title;
                            var aliases = title.Split(',');
                            if (aliases.Length <= 1)
                            {
                                outputWord.Aliases.AddRange(aliases.Select(a => a.Trim()).Where(a => a != slug));
                            }
                        }
                        output.Add(outputWord);
                    }
                }
            }
            return output;
        }
        private static string GetTitleHeading(HeadingBlock heading)
        {
            return heading.Inline.FirstChild?.ToString()?? "";
        }
        private static List<string> GetRelatedWords(IEnumerable<LinkInline> input)
        {
            var output = new List<string>();
            foreach (var link in input.Where(l => l.Url.EndsWith(".md")))
            {
                var linkComponents = link.Url.Split("/");
                if (linkComponents[0] == ".." || linkComponents[0] == ".")
                {
                    output.Add(Path.GetFileNameWithoutExtension(linkComponents[^1]));
                }
            }
            return output;
        }
    }
}
