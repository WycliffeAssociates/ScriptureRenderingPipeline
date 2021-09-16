using System.Collections.Generic;

namespace BTTWriterCatalog.Models
{
    public class MarkdownChapter
    {
        public int ChapterNumber {  get; set;}
        public List<MarkdownVerseContainer> Verses {  get; set; }
        public MarkdownChapter(int chapterNumber)
        {
            ChapterNumber = chapterNumber;
            Verses = new List<MarkdownVerseContainer>();
        }
    }
}
