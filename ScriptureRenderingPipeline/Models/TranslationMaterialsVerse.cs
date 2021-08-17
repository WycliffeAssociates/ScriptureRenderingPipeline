namespace ScriptureRenderingPipeline.Models
{
    public class TranslationMaterialsVerse
    {
        public string VerseNumber { get; set; }
        public string HtmlContent { get; set; }
        public TranslationMaterialsVerse(string verseNumber, string content)
        {
            VerseNumber = verseNumber;
            HtmlContent = content;
        }
    }
}
