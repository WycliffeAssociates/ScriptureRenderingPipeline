using ScriptureRenderingPipeline.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptureRenderingPipeline.Renderers
{
    class TranslationQuestionsRenderer : MarkdownRendererBase
    {
        protected override string VerseFormatString => "tq-chapter-{0}-{1}-{2}";

        protected override string ChapterFormatString => "tq-chapter-{0}-{1}";

        protected override string ContentType => "tq";
        protected override void BeforeVerse(StringBuilder builder, TranslationMaterialsBook book, TranslationMaterialsChapter chapter, TranslationMaterialsVerse verse)
        {
            if (!(chapter.ChapterNumber == "front" || verse.VerseNumber == "intro"))
            {
                builder.AppendLine($"<h1 id=\"{string.Format(VerseFormatString, book.BookId, chapter.ChapterNumber, verse.VerseNumber)}\">{book.BookName} {chapter.ChapterNumber}:{verse.VerseNumber}</h2>");
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
                builder.AppendLine($"<h1 id=\"{string.Format(ChapterFormatString, book.BookId, chapter.ChapterNumber)}\">{book.BookName} {chapter.ChapterNumber}</h2>");
            }
            else
            {
                builder.AppendLine($"<div id=\"{string.Format(ChapterFormatString, book.BookId, chapter.ChapterNumber)}\"></div>");
            }
        }
    }
}
