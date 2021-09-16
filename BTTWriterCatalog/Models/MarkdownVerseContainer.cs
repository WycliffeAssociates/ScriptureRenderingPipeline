using Markdig.Syntax;
using System.Collections.Generic;

namespace BTTWriterCatalog.Models
{
    public class MarkdownVerseContainer
    {
        public int VerseNumber { get; set; }
        public List<(string title, MarkdownDocument content)> Content {  get; set; }

        public MarkdownVerseContainer(int verseNumber, List<(string title, MarkdownDocument content)> verseContent)
        {
            VerseNumber = verseNumber;
            Content = verseContent;
        }
    }
}
