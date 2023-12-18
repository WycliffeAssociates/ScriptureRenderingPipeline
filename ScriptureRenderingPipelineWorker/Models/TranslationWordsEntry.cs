using DotLiquid;

namespace ScriptureRenderingPipelineWorker.Models
{
    public class TranslationWordsEntry : ILiquidizable
    {
        public string Title {  get; set; }
        public string Slug { get; set; }
        public string Content { get; set; }

        public object ToLiquid()
        {
            return new
            {
                title = Title,
                slug = Slug,
            };
        }
    }
}
