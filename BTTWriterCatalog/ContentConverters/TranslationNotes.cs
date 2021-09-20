using BTTWriterCatalog.Helpers;
using BTTWriterCatalog.Models;
using BTTWriterCatalog.Models.OutputFormats;
using Markdig;
using Markdig.Renderers;
using Markdig.Syntax;
using Newtonsoft.Json;
using PipelineCommon.Helpers;
using PipelineCommon.Models.ResourceContainer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTTWriterCatalog.ContentConverters
{
    public class TranslationNotes
    {
        public static void Convert(ZipFileSystem fileSystem, string basePath, string outputPath, ResourceContainer container, Dictionary<string,Dictionary<int,List<VerseChunk>>> chunks)
        {
            var files = ConversionUtils.LoadScriptureMarkdownFiles(fileSystem, basePath, container);
            foreach (var book in files)
            {
                var bookOutput = new List<TranslationNoteChunk>();
                if (!chunks.ContainsKey(book.Key.ToUpper()) || chunks[book.Key.ToUpper()].Count == 0)
                {
                    //TODO: We should probably warn at this point that chunks are missing for a book
                    continue;
                }

                var maxChapterNumberChars = book.Value.Max(i => i.ChapterNumber).ToString().Length;
                //var convertedChunks = ConversionUtils.ConvertChunks(chunks[book.Key.ToUpper()]);
                foreach (var chapter in book.Value)
                {
                    var maxVerseNumberChars = chapter.Verses.Max(v => v.VerseNumber).ToString().Length;
                    var verseChunks = chunks[book.Key.ToUpper()][chapter.ChapterNumber];
                    foreach(var chunk in verseChunks)
                    {
                        var currentChunk = new TranslationNoteChunk() { 
                            Id = $"{chapter.ChapterNumber.ToString().PadLeft(maxChapterNumberChars,'0')}-{chunk.StartingVerse.ToString().PadLeft(maxVerseNumberChars,'0')}" 
                        };
                        var content = new List<(string title, MarkdownDocument content)>();
                        if (chunk.EndingVerse == 0)
                        {
                            foreach(var i in chapter.Verses.Where(i => i.VerseNumber >= chunk.StartingVerse))
                            {
                                content.AddRange(i.Content);
                            }
                        }
                        foreach(var i in chapter.Verses.Where(i => i.VerseNumber >= chunk.StartingVerse && i.VerseNumber < chunk.EndingVerse))
                        {
                            content.AddRange(i.Content);
                        }

                        currentChunk.Notes.AddRange(content.Select(c => new TranslationNoteEntry() { Reference = c.title, Text = ConversionUtils.RenderMarkdownToPlainText(c.content).Trim() }));
                        bookOutput.Add(currentChunk);
                    }
                }
                string bookDir = Path.Join(outputPath,book.Key);
                if (!Directory.Exists(bookDir))
                {
                    Directory.CreateDirectory(bookDir);
                }
                File.WriteAllText(Path.Join(bookDir, "notes.json"), JsonConvert.SerializeObject(bookOutput));
            }
        }
    }
}
