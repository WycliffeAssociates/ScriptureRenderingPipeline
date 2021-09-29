using Newtonsoft.Json;
using System;

namespace PipelineCommon.Models.Webhook
{
    public class Repository
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("owner")]
        public GitUser Owner { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("full_name")]
        public string FullName { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }
        [JsonProperty("empty")]
        public bool Empty { get; set; }
        [JsonProperty("_private")]
        public bool Private { get; set; }
        [JsonProperty("fork")]
        public bool Fork { get; set; }
        [JsonProperty("template")]
        public bool Template { get; set; }
        [JsonProperty("parent")]
        public object Parent { get; set; }
        [JsonProperty("mirror")]
        public bool Mirror { get; set; }
        [JsonProperty("size")]
        public int Size { get; set; }
        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; }
        [JsonProperty("ssh_url")]
        public string SSHUrl { get; set; }
        [JsonProperty("clone_url")]
        public string CloneUrl { get; set; }
        [JsonProperty("original_url")]
        public string OriginalUrl { get; set; }
        [JsonProperty("website")]
        public string Website { get; set; }
        [JsonProperty("stars_count")]
        public int StarsCount { get; set; }
        [JsonProperty("forks_count")]
        public int forks_count { get; set; }
        [JsonProperty("watchers_count")]
        public int watchers_count { get; set; }
        [JsonProperty("open_issues_count")]
        public int open_issues_count { get; set; }
        [JsonProperty("open_pr_counter")]
        public int open_pr_counter { get; set; }
        [JsonProperty("release_counter")]
        public int release_counter { get; set; }
        [JsonProperty("default_branch")]
        public string default_branch { get; set; }
        [JsonProperty("archived")]
        public bool archived { get; set; }
        [JsonProperty("created_at")]
        public DateTime created_at { get; set; }
        [JsonProperty("updated_at")]
        public DateTime updated_at { get; set; }
        [JsonProperty("permissions")]
        public Permissions permissions { get; set; }
        [JsonProperty("has_issues")]
        public bool has_issues { get; set; }
        [JsonProperty("internal_tracker")]
        public Internal_Tracker internal_tracker { get; set; }
        [JsonProperty("has_wiki")]
        public bool has_wiki { get; set; }
        [JsonProperty("has_pull_requests")]
        public bool has_pull_requests { get; set; }
        [JsonProperty("ignore_whitespace_conflicts")]
        public bool ignore_whitespace_conflicts { get; set; }
        [JsonProperty("allow_merge_commits")]
        public bool allow_merge_commits { get; set; }
        [JsonProperty("allow_rebase")]
        public bool allow_rebase { get; set; }
        [JsonProperty("allow_rebase_explicit")]
        public bool allow_rebase_explicit { get; set; }
        [JsonProperty("allow_squash_merge")]
        public bool AllowSquashMerge { get; set; }
        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }
    }

}
