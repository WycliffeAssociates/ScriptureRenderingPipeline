using Markdig.Syntax;

namespace ScriptureRenderingPipelineWorker.Models
{
    class CommentaryChapter
    {
        public string Number { get; set; }
        public MarkdownDocument Content { get; set; }
    }
}
