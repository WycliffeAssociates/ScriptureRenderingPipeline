namespace ScriptureRenderingPipelineWorker.Models
{
    public class TranslationMaterialsBook
    {
        public string FileName { get; set; }
        public string BookId { get; set; }
        public string BookName { get; set; }
        public List<TranslationMaterialsChapter> Chapters { get; set; }
        public TranslationMaterialsBook()
        {
            Chapters = new List<TranslationMaterialsChapter>();
        }
    }
}
