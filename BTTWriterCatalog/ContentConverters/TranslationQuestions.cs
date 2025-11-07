using BTTWriterCatalog.Helpers;
using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.OutputFormats;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Markdig;
using PipelineCommon.Helpers.MarkdigExtensions;
using System.Threading.Tasks;

namespace BTTWriterCatalog.ContentConverters
{
    public static class TranslationQuestions
    {
        /// <summary>
        /// Convert content into questions that BTTWriter can understand
        /// </summary>
        /// <param name="fileSystem">A ZipFileSytem holding the data</param>
        /// <param name="basePath">A base path inside of the zip file holding the information</param>
        /// <param name="resourceContainer">Resource Container for all of the project metadata</param>
        /// <param name="log">An instance of ILogger to log warnings and information</param>
        /// <returns>A list of books processed</returns>
        /// <remarks>The created JSON is organized by books using a question as a key and then just listing what verses use that question</remarks>
        public static async Task<List<string>> ConvertAsync(IZipFileSystem fileSystem, string basePath, IOutputInterface outputInterface, ResourceContainer resourceContainer, ILogger log)
        {
            MarkdownPipeline markdownPipeline = new MarkdownPipelineBuilder().Use(new RCLinkExtension(new RCLinkOptions() { RenderAsBTTWriterLinks = true })).Build();
            var markdownFiles = ConversionUtils.LoadScriptureMarkdownFiles(fileSystem, basePath, resourceContainer, markdownPipeline);
            var outputTasks = new List<Task>();
            foreach(var (bookname,chapters) in markdownFiles)
            {
                var output = new List<TranslationQuestionChapter>();
                var maxChapterNumberLength = chapters.Max(c => c.ChapterNumber).ToString().Length;
                foreach(var chapter in chapters)
                {
                    var maxVerseNumberLength = chapter.Verses.Max(v => v.VerseNumber).ToString().Length;
                    var outputChapter = new TranslationQuestionChapter() { Identifier = chapter.ChapterNumber.ToString().PadLeft(maxChapterNumberLength,'0') };
                    // The way that this handles a duplicate question is just to add another reference to it so keep a record of it here
                    var insertedQuestions = new Dictionary<string, TranslationQuestion>();
                    foreach(var verse in chapter.Verses)
                    {
                        foreach(var (title, content) in verse.Content)
                        {
                            var questionContent = ConversionUtils.RenderMarkdownToPlainText(content, markdownPipeline).Trim();
                            var reference = BuildVerseReference(chapter, maxChapterNumberLength, verse, maxVerseNumberLength);
                            var questionKey = title + questionContent;

                            // If we already have this question then just add another reference to it
                            if (insertedQuestions.ContainsKey(questionKey))
                            {
                                insertedQuestions[questionKey].References.Add(reference);
                                continue;
                            }

                            var question = new TranslationQuestion(title, questionContent);
                            question.References.Add(reference);
                            insertedQuestions.Add(questionKey, question);
                            outputChapter.Questions.Add(question);
                        }
                    }
                    output.Add(outputChapter);
                }
                
                if (!outputInterface.DirectoryExists(bookname))
                {
                    outputInterface.CreateDirectory(bookname);
                }
                
                outputTasks.Add(outputInterface.WriteAllTextAsync(Path.Join(bookname, "questions.json"), JsonSerializer.Serialize(output, CatalogJsonContext.Default.ListTranslationQuestionChapter)));
            }
            await Task.WhenAll(outputTasks);
            return markdownFiles.Keys.ToList();
        }

        private static string BuildVerseReference(MarkdownChapter chapter, int maxChapterNumberLength, MarkdownVerseContainer verse, int maxVerseNumberLength)
        {
            return $"{chapter.ChapterNumber.ToString().PadLeft(maxChapterNumberLength, '0')}-{verse.VerseNumber.ToString().PadLeft(maxVerseNumberLength,'0')}";
        }
    }
}
