using System.Collections.Generic;
using System.Text;

namespace ScriptureRenderingPipeline.Models
{
    class WebhookEvent
    {
        public string secret { get; set; }
        public string _ref { get; set; }
        public string before { get; set; }
        public string after { get; set; }
        public string compare_url { get; set; }
        public Commit[] commits { get; set; }
        public object head_commit { get; set; }
        public Repository repository { get; set; }
        public GitUser pusher { get; set; }
        public GitUser sender { get; set; }
    }

}
