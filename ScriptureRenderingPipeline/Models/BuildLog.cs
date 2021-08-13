using System;

namespace ScriptureRenderingPipeline.Models
{

    public class BuildLog
    {
        public string commit_id { get; set; }
        public string commit_message { get; set; }
        public string commit_url { get; set; }
        public string committed_by { get; set; }
        public string compare_url { get; set; }
        public string convert_module { get; set; }
        public DateTime created_at { get; set; }
        public DateTime ended_at { get; set; }
        public string[] errors { get; set; }
        public DateTime eta { get; set; }
        public DateTime expires_at { get; set; }
        public string input_format { get; set; }
        public string lint_module { get; set; }
        public string[] log { get; set; }
        public string message { get; set; }
        public bool multiple { get; set; }
        public string output { get; set; }
        public string output_format { get; set; }
        public string repo_name { get; set; }
        public string repo_owner { get; set; }
        public string resource_type { get; set; }
        public string source { get; set; }
        public DateTime started_at { get; set; }
        public string status { get; set; }
        public bool success { get; set; }
        public DateTime updated_at { get; set; }
        public string user { get; set; }
        public string user_name { get; set; }
        public string[] warnings { get; set; }
    }
}
