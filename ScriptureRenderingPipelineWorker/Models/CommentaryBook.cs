namespace ScriptureRenderingPipelineWorker.Models
{
    class CommentaryBook
    {
        public string Title { get; set; }
        public List<CommentaryChapter> Chapters { get; set; }
        public string BookId { get; set; }

        public CommentaryBook()
        {
            Chapters = new List<CommentaryChapter>();
        }
    }
}
