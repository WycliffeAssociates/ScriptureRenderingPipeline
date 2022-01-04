using DotLiquid;

namespace ScriptureRenderingPipeline.Models
{
    public class DownloadLink: ILiquidizable
    {
        public string Link { get; set; }
        public string Title { get; set; }
        public object ToLiquid()
        {
            return new
            {
                link = Link,
                title = Title,
            };
        }
    }
}