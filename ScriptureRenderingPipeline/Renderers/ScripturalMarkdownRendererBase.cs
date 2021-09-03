using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DotLiquid;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using ScriptureRenderingPipeline.Helpers;
using ScriptureRenderingPipeline.Helpers.MarkdigExtensions;
using ScriptureRenderingPipeline.Models;

namespace ScriptureRenderingPipeline.Renderers
{
    public abstract class ScripturalMarkdownRendererBase
    {
        protected abstract string VerseFormatString { get; }
        protected abstract string ChapterFormatString { get; }
        protected abstract string ContentType { get; }
        protected abstract void BeforeVerse(StringBuilder builder, TranslationMaterialsBook book, TranslationMaterialsChapter chapter, TranslationMaterialsVerse verse);
        protected abstract void BeforeChapter(StringBuilder builder, TranslationMaterialsBook book, TranslationMaterialsChapter chapter);
        protected string BuildFileName(string bookName)
        {
            return $"{Utils.GetBookNumber(bookName):00}-{bookName.ToUpper()}.html";
        }
        protected IEnumerable<string> FilterAndOrderBooks(IEnumerable<string> input)
        {
            return input
                .Select(i => (book: i, order: Utils.BibleBookOrder.IndexOf(i.ToUpper())))
                .Where(i => i.order != -1)
                .OrderBy(i => i.order)
                .Select(i => i.book);
        }
        protected IEnumerable<string> FilterAndOrderChapters(IEnumerable<string> input)
        {
            return input.Where(i => i == "front" || int.TryParse(i, out _)).Select(i => (file: i, order: i == "front" ? 0 : int.Parse(i))).OrderBy(i => i.order).Select(i => i.file);
        }
        protected IEnumerable<string> OrderVerses(IEnumerable<string> input)
        {
            return input
                .Where(i => Path.GetFileName(i) == "intro.md" || int.TryParse(Path.GetFileNameWithoutExtension(i), out _))
                .Select(i => (book: i, index: Path.GetFileName(i) == "intro.md" ? 0 : int.Parse(Path.GetFileNameWithoutExtension(i))))
                .OrderBy(i => i.index)
                .Select(i => i.book);
        }
        protected string RewriteContentLinks(string link, TranslationMaterialsBook currentBook, TranslationMaterialsChapter currentChapter)
        {
            var splitLink = link.Split("/");
            if (splitLink.Length == 1)
            {
                return BuildFileName(currentBook.BookId) + "#" + string.Format(VerseFormatString, currentBook.BookId, currentChapter.ChapterNumber, splitLink[0][..^3]);
            }

            if (splitLink[0] == ".")
            {
                return BuildFileName(currentBook.BookId) + "#" + string.Format(VerseFormatString, currentBook.BookId, currentChapter.ChapterNumber, splitLink[1][..^3]);
            }
            else if (splitLink[0] == "..")
            {
                if (splitLink.Length == 3)
                {
                    return BuildFileName(currentBook.BookId) + "#" + string.Format(VerseFormatString, currentBook.BookId, splitLink[1], splitLink[2][..^3]);
                }
                else if (splitLink.Length == 4)
                {
                    return BuildFileName(currentBook.BookId) + "#" + string.Format(VerseFormatString, splitLink[1], splitLink[2], splitLink[3][..^3]);
                }
            }
            return link;
        }

        protected List<NavigationBook> BuildNavigation(List<TranslationMaterialsBook> input)
        {
            var output = new List<NavigationBook>(); 
            foreach(var book in input)
            {
                var navBook = new NavigationBook() { abbreviation = book.BookId, file = book.FileName, title = book.BookName };
                foreach(var chapter in book.Chapters)
                {
                    // Remove leading zeros from chapter
                    string printableChapterNumber = chapter.ChapterNumber.TrimStart('0');
                    navBook.chapters.Add(new NavigationChapter() { id = string.Format(ChapterFormatString,book.BookId,chapter.ChapterNumber), title = printableChapterNumber });
                }
                output.Add(navBook);
            }
            return output;
        }

        protected virtual List<TranslationMaterialsBook> LoadMarkDownFiles(ZipFileSystem fileSystem, string basePath)
        {
            RCLinkOptions options = new RCLinkOptions()
            {
                BaseUser = "WycliffeAssociates",
                ResourceOverrideMapping = new Dictionary<string, string>()
                {
                    ["ta"] = "tm"
                },
                // TODO: this needs to be changed to a configuration value
                ServerUrl = "https://content.bibletranslationtools.org"
            };
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Use<RCLinkExtension>(new RCLinkExtension(options)).Build();
            var output = new List<TranslationMaterialsBook>();

            foreach (var book in FilterAndOrderBooks(fileSystem.GetFolders(basePath)))
            {
                var tnBook = new TranslationMaterialsBook()
                {
                    FileName = BuildFileName(book),
                    BookId = book,
                    BookName = Utils.bookAbbrivationMappingToEnglish.ContainsKey(book.ToUpper()) ? Utils.bookAbbrivationMappingToEnglish[book.ToUpper()] : book,
                };

                var chapters = fileSystem.GetFolders(fileSystem.Join(basePath, book));
                foreach(var chapter in FilterAndOrderChapters(chapters))
                {
                    var tnChapter = new TranslationMaterialsChapter(chapter);
                    foreach(var file in OrderVerses(fileSystem.GetFiles(fileSystem.Join(basePath, book, chapter),".md")))
                    {
                        var parsedVerse = Markdown.Parse(fileSystem.ReadAllText(file),pipeline);
                        
                        // adjust the heading blocks up one level so I can put in chapter and verse sections as H1
                        foreach(var headingBlock in parsedVerse.Descendants<HeadingBlock>())
                        {
                            headingBlock.Level++;
                        }

                        foreach(var link in parsedVerse.Descendants<LinkInline>())
                        {
                            if (link.Url.EndsWith(".md"))
                            {
                                link.Url = RewriteContentLinks(link.Url, tnBook, tnChapter);
                            }
                        }

                        var tnVerse = new TranslationMaterialsVerse(Path.GetFileNameWithoutExtension(file), parsedVerse.ToHtml(pipeline));
                        tnChapter.Verses.Add(tnVerse);
                    }
                    tnBook.Chapters.Add(tnChapter);
                }
                output.Add(tnBook);
            }
            return output;
        }
        public virtual void Render(ZipFileSystem sourceDir, string basePath, string destinationDir, Template template, Template printTemplate, string repoUrl, string heading, bool isBTTWriterProject = false)
        {
            var books = LoadMarkDownFiles(sourceDir, basePath);
            var navigation = BuildNavigation(books);
            var printBuilder = new StringBuilder();
            foreach(var book in books)
            {
                var builder = new StringBuilder();
                foreach(var chapter in book.Chapters)
                {
                    BeforeChapter(builder, book, chapter);
                    foreach (var verse in chapter.Verses)
                    {
                        BeforeVerse(builder, book, chapter, verse);
                        builder.AppendLine(verse.HtmlContent);
                    }
                }
                var templateResult = template.Render(Hash.FromAnonymousObject(new
                {
                    content = builder.ToString(),
                    scriptureNavigation = navigation,
                    contenttype = ContentType,
                    currentBook = book.BookId,
                    heading,
                    sourceLink = repoUrl
                }
                ));
                File.WriteAllText(Path.Join(destinationDir, book.FileName),templateResult);
                printBuilder.Append(builder);
            }

            if (books.Count > 0)
            {
                File.Copy(Path.Join(destinationDir,books[0].FileName), Path.Combine(destinationDir, "index.html"));
                File.WriteAllText(Path.Join(destinationDir, "print_all.html"), printTemplate.Render(Hash.FromAnonymousObject(new { content = printBuilder.ToString(), heading })));
            }
        }
    }
}