using BTTWriterCatalog.Helpers;
using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.OutputFormats;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PipelineCommon.Helpers;
using PipelineCommon.Helpers.MarkdigExtensions;
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
        private static List<TranslationWord> LoadWords(ZipFileSystem sourceDir, string basePath, ILogger log)
        {
            MarkdownPipeline markdownPipeline = new MarkdownPipelineBuilder().Use(new RCLinkExtension(new RCLinkOptions() { RenderAsBTTWriterLinks = true })).Build();
            var output = new List<TranslationWord>();
            foreach( var dir in sourceDir.GetFolders(basePath))
            {
                if (Utils.TranslationWordsValidSections.Contains(dir))
                {
                    foreach(var file in sourceDir.GetFiles(sourceDir.Join(basePath, dir),".md"))
                    {
                        var slug = Path.GetFileNameWithoutExtension(file);
                        var content = Markdown.Parse(sourceDir.ReadAllText(file), markdownPipeline);
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
                                outputWord.Aliases.AddRange(aliases.Select(a => a.Trim()).Where(a => a != slug.Trim()));
                            }
                        }
                        output.Add(outputWord);
                    }
                }
            }
            return output;
        }

        public static List<string> ConvertWordsCatalog(string outputPath, Dictionary<string, List<WordCatalogCSVRow>> input, Dictionary<string, Dictionary<int,List<VerseChunk>>> chunks)
        {
            foreach(var (book,chapters) in chunks)
            {
                if (!input.ContainsKey(book.ToLower()))
                {
                    continue;
                }
                var output = new TranslationWordsCatalogRoot();
                var maxChapterNumberLength = ConversionUtils.GetMaxStringLength(chapters.Select(c => c.Key));
                foreach (var (chapter, chapterChunks) in chapters)
                {
                    var outputChapter = new TranslationWordsCatalogChapter(chapter.ToString().PadLeft(maxChapterNumberLength, '0'));
                    foreach (var chunk in chapterChunks)
                    {
                        var maxVerseNumberLenth = ConversionUtils.GetMaxStringLength(chapterChunks.Select(c => c.StartingVerse));
                        outputChapter.Frames.Add(new TranslationWordCatalogFrame(chunk.StartingVerse.ToString().PadLeft(maxVerseNumberLenth, '0'))
                        {
                            Items = input[book.ToLower()].Where(r => r.Chapter == chapter && r.Verse >= chunk.StartingVerse && (r.Verse <= chunk.EndingVerse || chunk.EndingVerse == 0))
                            .Select(r => new TranslationWordCatalogItem(r.Word)).ToList()
                        });
                    }
                    output.Chapters.Add(outputChapter);
                }
                if (!Directory.Exists(Path.Join(outputPath, book.ToLower())))
                {
                    Directory.CreateDirectory(Path.Join(outputPath, book.ToLower()));
                }
                File.WriteAllText(Path.Join(outputPath, book.ToLower(), "tw_cat.json"), JsonConvert.SerializeObject(output));
            }
            return input.Select(k => k.Key).ToList();
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
