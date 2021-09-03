using DotLiquid;
using System.Collections.Generic;

namespace ScriptureRenderingPipeline.Models
{
    public class TranslationWordsCategory: ILiquidizable
    {
        public string Title {  get; set; }
        public string Slug { get; set; }
        public List<TranslationWordsEntry> Words {  get; set; }
        public TranslationWordsCategory()
        {
            Words = new List<TranslationWordsEntry>();
        }

        public object ToLiquid()
        {
            return new
            {
                title = Title,
                slug = Slug,
                words = Words,
            };
        }
    }
}
