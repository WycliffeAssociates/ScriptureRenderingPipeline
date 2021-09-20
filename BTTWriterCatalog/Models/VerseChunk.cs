namespace BTTWriterCatalog.Models
{
    public class VerseChunk
    {
        public int StartingVerse {  get; set; }
        public int EndingVerse {  get; set;}

        public VerseChunk(int startingVerse, int endingVerse)
        {
            StartingVerse = startingVerse;
            EndingVerse = endingVerse;
        }
        public VerseChunk()
        {

        }
    }
}
