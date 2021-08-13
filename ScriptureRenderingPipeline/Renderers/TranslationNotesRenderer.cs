﻿using DotLiquid;
using Markdig;
using ScriptureRenderingPipeline.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ScriptureRenderingPipeline.Renderers
{
    public class TranslationNotesRenderer
    {
        public void Render(string sourceDir, string destinationDir, Template template, string repoUrl, string heading, bool isBTTWriterProject = false)
        {
            var books = LoadMarkDownFiles(sourceDir);
            var navigation = BuildNavigation(books);
            foreach(var book in books)
            {
                var builder = new StringBuilder();
                foreach(var chapter in book.Chapters)
                {
                    if (chapter.ChapterNumber != "front")
                    {
                        builder.AppendLine($"<h2 id=\"tn-chapter-{book.BookId}-{chapter.ChapterNumber}\">{book.BookName} {chapter.ChapterNumber}</h2>");
                    }
                    foreach (var verse in chapter.Verses)
                    {
                        if (chapter.ChapterNumber != "front" || verse.VerseNumber == "intro")
                        {
                            builder.AppendLine($"<h3 id=\"tn-chunk-{book.BookId}-{chapter.ChapterNumber}-{verse.VerseNumber}\">{book.BookName} {chapter.ChapterNumber}:{verse.VerseNumber}</h2>");
                        }
                        builder.AppendLine(verse.HtmlContent);
                    }
                }
                var templateResult = template.Render(Hash.FromAnonymousObject(new
                {
                    content = builder.ToString(),
                    scriptureNavigation = navigation,
                    contenttype = "tn",
                    currentBook = book.BookId,
                    heading,
                    sourceLink = repoUrl
                }
                ));
                File.WriteAllText(Path.Join(destinationDir, book.FileName),templateResult);
            }

            File.Copy(Path.Join(destinationDir,books[0].FileName), Path.Combine(destinationDir, "index.html"));
        }

        private List<NavigationBook> BuildNavigation(List<TranslationNotesBook> input)
        {
            var output = new List<NavigationBook>(); 
            foreach(var book in input)
            {
                var navBook = new NavigationBook() { abbreviation = book.BookId, file = book.FileName, title = book.BookName };
                foreach(var chapter in book.Chapters)
                {
                    navBook.chapters.Add(new NavigationChapter() { number = chapter.ChapterNumber, title = chapter.ChapterNumber });
                }
                output.Add(navBook);
            }
            return output;
        }
        private List<TranslationNotesBook> LoadMarkDownFiles(string sourceDir)
        {
            var output = new List<TranslationNotesBook>();

            foreach (var book in FilterAndOrderBooks(GetDirectoriesInDirectory(sourceDir)))
            {
                var tnBook = new TranslationNotesBook()
                {
                    FileName = BuildFileName(book),
                    BookId = book,
                    BookName = book,
                };

                var chapters = GetDirectoriesInDirectory(Path.Join(sourceDir, book));
                foreach(var chapter in FilterAndOrderChapters(chapters))
                {
                    var tnChapter = new TranslationNotesChapter(chapter);
                    foreach(var file in Directory.GetFiles(Path.Combine(sourceDir, book, chapter),"*.md"))
                    {
                        var tnVerse = new TranslationNotesVerse(Path.GetFileNameWithoutExtension(file), Markdown.ToHtml(File.ReadAllText(file)));
                        tnChapter.Verses.Add(tnVerse);
                    }
                    tnBook.Chapters.Add(tnChapter);
                }
                output.Add(tnBook);
            }
            return output;
        }
        private string BuildFileName(string bookName)
        {
            return $"{Utils.BibleBookOrder.IndexOf(bookName.ToUpper()):00}-{bookName.ToUpper()}.html";
        }
        private IEnumerable<string> FilterAndOrderBooks(IEnumerable<string> input)
        {
            return input.Select(i => (book: i, order: Utils.BibleBookOrder.IndexOf(i.ToUpper()))).Where(i => i.order != -1).OrderBy(i => i.order).Select(i => i.book);
        }
        private IEnumerable<string> GetDirectoriesInDirectory(string inputDir)
        {
            return Directory.GetDirectories(inputDir).Select(i => i.Split(Path.DirectorySeparatorChar)[^1]);
        }
        private IEnumerable<string> FilterAndOrderChapters(IEnumerable<string> input)
        {
            return input.Where(i => i == "front" || int.TryParse(i, out _)).Select(i => (file: i, order: i == "front" ? 0 : int.Parse(i))).OrderBy(i => i.order).Select(i => i.file);
        }
    }
    public class TranslationNotesBook
    {
        public string FileName { get; set; }
        public string BookId { get; set; }
        public string BookName { get; set; }
        public List<TranslationNotesChapter> Chapters { get; set; }
        public TranslationNotesBook()
        {
            Chapters = new List<TranslationNotesChapter>();
        }
    }
    public class TranslationNotesChapter
    {
        public string ChapterNumber { get; set; }
        public List<TranslationNotesVerse> Verses { get; set; }
        public TranslationNotesChapter(string chapterNumber)
        {
            ChapterNumber = chapterNumber;
            Verses = new List<TranslationNotesVerse>();
        }
    }
    public class TranslationNotesVerse
    {
        public string VerseNumber { get; set; }
        public string HtmlContent { get; set; }
        public TranslationNotesVerse(string verseNumber, string content)
        {
            VerseNumber = verseNumber;
            HtmlContent = content;
        }
    }
}
