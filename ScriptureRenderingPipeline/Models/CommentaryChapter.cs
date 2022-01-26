using Markdig.Syntax;

namespace ScriptureRenderingPipeline.Models
{
    class CommentaryChapter
    {
        public string Number { get; set; }
        public MarkdownDocument Content { get; set; }
    }
}
