using DotLiquid;

namespace ScriptureRenderingPipeline.Models
{
    public class TranslationManaulNavigation: ILiquidizable
    {
        public string title {  get; set; }
        public string filename { get; set; }
        public bool hasChildren { get; set; }
        public bool lastChild { get; set; }
        public string slug { get; set; }

        public object ToLiquid()
        {
            return new
            {
                title,
                filename,
                hasChildren,
                lastChild,
                slug
            };
        }
    }
}
