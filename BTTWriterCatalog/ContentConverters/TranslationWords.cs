using BTTWriterCatalog.Helpers;
using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.OutputFormats;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using PipelineCommon.Helpers;
using PipelineCommon.Helpers.MarkdigExtensions;
using PipelineCommon.Models.ResourceContainer;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BTTWriterCatalog.ContentConverters
{
    public static class TranslationWords
    {
        /// <summary>
        /// Convert translation words to a format that BTTWriter understands
        /// </summary>
        /// <param name="sourceDir">A ZipFileSystem to use as the source</param>
        /// <param name="basePath">The base path inside of the source directory to get stuff from</param>
        /// <param name="outputPath">The path to put the resulting files in</param>
        /// <param name="resourceContainer">Resource container to find what folder the words exist in beyond the base path</param>
        /// <param name="log">An instance of ILogger to log warnings to</param>
        public static async Task ConvertAsync(ZipFileSystem fileSystem, string basePath, string outputPath, ResourceContainer resourceContainer, ILogger log)
        {
            var projectPath = resourceContainer.projects[0].path;
            var words = await LoadWordsAsync(fileSystem, fileSystem.Join(basePath, projectPath), log);
            await File.WriteAllTextAsync(Path.Join(outputPath, "words.json"), JsonConvert.SerializeObject(words));
        }
        /// <summary>
        /// Generate a list of all of the words for this project
        /// </summary>
        /// <param name="sourceDir">A ZipFileSystem to use as the source</param>
        /// <param name="basePath">The base path inside of the source directory to get stuff from</param>
        /// <param name="log">An instance of ILogger to log warnings to</param>
        /// <returns>A list of translation words</returns>
        private static async Task<List<TranslationWord>> LoadWordsAsync(ZipFileSystem sourceDir, string basePath, ILogger log)
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
                        var content = Markdown.Parse(await sourceDir.ReadAllTextAsync(file), markdownPipeline);
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

        /// <summary>
        /// Creates a tw_cat for this specific language based on chunking information
        /// </summary>
        /// <param name="outputPath">The path to output the file to</param>
        /// <param name="mapping">A mapping for whch words are in whitch verse</param>
        /// <param name="chunks">Chunking information that is used to map words to chunks</param>
        /// <returns></returns>
        public static async Task<List<string>> ConvertWordsCatalogAsync(string outputPath, Dictionary<string, List<WordCatalogCSVRow>> mapping, Dictionary<string, Dictionary<int,List<VerseChunk>>> chunks)
        {
            foreach(var (book,chapters) in chunks)
            {
                if (!mapping.ContainsKey(book.ToLower()))
                {
                    continue;
                }
                var output = new TranslationWordsCatalogRoot();
                var maxChapterNumberLength = ConversionUtils.GetMaxStringLength(chapters.Select(c => c.Key));
                foreach (var (chapter, chapterChunks) in chapters)
                {
                    var outputChapter = new TranslationWordsCatalogChapter(chapter.ToString().PadLeft(maxChapterNumberLength, '0'));
                    // Loop through all of the chunks and add the words that exist in that chunk into it
                    foreach (var chunk in chapterChunks)
                    {
                        var maxVerseNumberLenth = ConversionUtils.GetMaxStringLength(chapterChunks.Select(c => c.StartingVerse));
                        outputChapter.Frames.Add(new TranslationWordCatalogFrame(chunk.StartingVerse.ToString().PadLeft(maxVerseNumberLenth, '0'))
                        {
                            Items = mapping[book.ToLower()].Where(r => r.Chapter == chapter && r.Verse >= chunk.StartingVerse && (r.Verse <= chunk.EndingVerse || chunk.EndingVerse == 0))
                            .Select(r => new TranslationWordCatalogItem(r.Word)).ToList()
                        });
                    }
                    output.Chapters.Add(outputChapter);
                }
                if (!Directory.Exists(Path.Join(outputPath, book.ToLower())))
                {
                    Directory.CreateDirectory(Path.Join(outputPath, book.ToLower()));
                }
                await File.WriteAllTextAsync(Path.Join(outputPath, book.ToLower(), "tw_cat.json"), JsonConvert.SerializeObject(output));
            }
            return mapping.Select(k => k.Key).ToList();
        }

        /// <summary>
        /// Get the text of a heading safely
        /// </summary>
        /// <param name="heading">The heading to get text from</param>
        /// <returns>The text from the heading</returns>
        private static string GetTitleHeading(HeadingBlock heading)
        {
            return heading.Inline.FirstChild?.ToString()?? "";
        }
        /// <summary>
        /// Get related words by finding links in the markdown to them
        /// </summary>
        /// <param name="input">A list of markdown links</param>
        /// <returns>A list of related words</returns>
        /// <remarks>This only works for relative links</remarks>
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
