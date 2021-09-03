using System.Collections.Generic;

namespace ScriptureRenderingPipeline.Renderers
{
    public class TranslationManualSection
    {
        public string title { get; set; }
        public string path { get; set; }
        public TableOfContents TableOfContents { get; set; }
        public List<TranslationManualContent> Content { get; set; }
        public string filename { get; set; }

        public TranslationManualSection(string title, string path, string filename)
        {
            this.title = title;
            this.path = path;
            this.filename = filename;
            Content = new List<TranslationManualContent>();
        }
    }
}
