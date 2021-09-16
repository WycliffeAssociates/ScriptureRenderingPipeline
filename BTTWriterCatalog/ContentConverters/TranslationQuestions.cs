using System;
using System.Text;
using BTTWriterCatalog.Helpers;
using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.OutputFormats;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using System.Security.Cryptography;

namespace BTTWriterCatalog.ContentConverters
{
    public static class TranslationQuestions
    {
        public static void Convert(ZipFileSystem fileSystem, string basePath, string outputPath, ResourceContainer resourceContainer, Dictionary<string,List<InputChunk>> chunks)
        {
            var markdownFiles = ConversionUtils.LoadScriptureMarkdownFiles(fileSystem, basePath, resourceContainer);
            foreach(var (bookname,chapters) in markdownFiles)
            {
                var output = new List<TranslationQuestionChapter>();
                var maxChapterNumberLength = chapters.Max(c => c.ChapterNumber).ToString().Length;
                foreach(var chapter in chapters)
                {
                    var maxVerseNumberLength = chapter.Verses.Max(v => v.VerseNumber).ToString().Length;
                    var outputChapter = new TranslationQuestionChapter() { Identifier = chapter.ChapterNumber.ToString().PadLeft(maxChapterNumberLength,'0') };
                    var insertedQuestions = new Dictionary<string, TranslationQuestion>();
                    foreach(var verse in chapter.Verses)
                    {
                        foreach(var (title, content) in verse.Content)
                        {
                            var questionContent = ConversionUtils.RenderMarkdownToPlainText(content).Trim();
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
                string bookDir = Path.Join(outputPath,bookname);
                if (!Directory.Exists(bookDir))
                {
                    Directory.CreateDirectory(bookDir);
                }
                File.WriteAllText(Path.Join(bookDir, "questions.json"), JsonConvert.SerializeObject(output));
            }
        }

        private static string BuildVerseReference(MarkdownChapter chapter, int maxChapterNumberLength, MarkdownVerseContainer verse, int maxVerseNumberLength)
        {
            return $"{chapter.ChapterNumber.ToString().PadLeft(maxChapterNumberLength, '0')}-{verse.VerseNumber.ToString().PadLeft(maxVerseNumberLength,'0')}";
        }
    }
}
