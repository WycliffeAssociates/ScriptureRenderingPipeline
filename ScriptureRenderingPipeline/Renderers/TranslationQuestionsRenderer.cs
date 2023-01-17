using ScriptureRenderingPipeline.Models;
using System.Text;

namespace ScriptureRenderingPipeline.Renderers
{
    class TranslationQuestionsRenderer : ScripturalMarkdownRendererBase
    {
        protected override string VerseFormatString => "tq-chapter-{0}-{1}-{2}";

        protected override string ChapterFormatString => "tq-chapter-{0}-{1}";

        protected override string ContentType => "tq";
        protected override void BeforeVerse(StringBuilder builder, TranslationMaterialsBook book, TranslationMaterialsChapter chapter, TranslationMaterialsVerse verse)
        {
            if (!(chapter.ChapterNumber == "front" || verse.VerseNumber == "intro"))
            {
                // Remove leading zeros from chapter and verse
                string printableChapterNumber = chapter.ChapterNumber.TrimStart('0');
                string printableVerseNumber = verse.VerseNumber.TrimStart('0');
                builder.AppendLine($"<h1 id=\"{string.Format(VerseFormatString, book.BookId, chapter.ChapterNumber, verse.VerseNumber)}\">{book.BookName} {printableChapterNumber}:{printableVerseNumber}</h2>");
            }
            else
            {
                builder.AppendLine($"<div id=\"{string.Format(VerseFormatString, book.BookId, chapter.ChapterNumber, verse.VerseNumber)}\"></div>");
            }
        }

        protected override void BeforeChapter(StringBuilder builder, TranslationMaterialsBook book, TranslationMaterialsChapter chapter)
        {
            if (chapter.ChapterNumber != "front")
            {
                // Remove leading zeros from chapter
                string printableChapterNumber = chapter.ChapterNumber.TrimStart('0');
                builder.AppendLine($"<h1 id=\"{string.Format(ChapterFormatString, book.BookId, chapter.ChapterNumber)}\">{book.BookName} {printableChapterNumber}</h2>");
            }
            else
            {
                builder.AppendLine($"<div id=\"{string.Format(ChapterFormatString, book.BookId, chapter.ChapterNumber)}\"></div>");
            }
        }
    }
}
