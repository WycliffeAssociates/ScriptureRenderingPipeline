using DotLiquid;

namespace ScriptureRenderingPipelineWorker.Models
{
    public class TranslationManualNavigationSection: ILiquidizable
    {
        public string Title { get; set; }
        public string FileName { get; set; }
        public List<TranslationManaulNavigation> Navigation { get; set; }
        public TranslationManualNavigationSection()
        {
            Navigation = new List<TranslationManaulNavigation>();
        }

        public object ToLiquid()
        {
            return new
            {
                title = Title,
                fileName = FileName,
                navigation = Navigation,
            };
        }
    }
}
