namespace ScriptureRenderingPipelineWorker.Models
{
    public class TableOfContents
    {
        public string title { get; set;  }
        public string link { get; set; }
        public List<TableOfContents> sections {  get; set; }
        public TableOfContents(string title, string link)
        {
            this.title = title;
            this.link = link;
            sections = new List<TableOfContents>();
        }
        public TableOfContents()
        {
            sections = new List<TableOfContents>();
        }
    }
}
