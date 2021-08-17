using System.Collections.Generic;

namespace ScriptureRenderingPipeline.Renderers
{
    public class TranslationMaterialsChapter
    {
        public string ChapterNumber { get; set; }
        public List<TranslationMaterialsVerse> Verses { get; set; }
        public TranslationMaterialsChapter(string chapterNumber)
        {
            ChapterNumber = chapterNumber;
            Verses = new List<TranslationMaterialsVerse>();
        }
    }
}
