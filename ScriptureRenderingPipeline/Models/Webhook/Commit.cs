using System;

namespace ScriptureRenderingPipeline.Models.Webhook
{
    public class Commit
    {
        public string id { get; set; }
        public string message { get; set; }
        public string url { get; set; }
        public Author author { get; set; }
        public Committer committer { get; set; }
        public object verification { get; set; }
        public DateTime timestamp { get; set; }
        public object added { get; set; }
        public object removed { get; set; }
        public object modified { get; set; }
    }

}
