using System;
using System.Text.Json.Serialization;

namespace PipelineCommon.Models.Webhook
{
    public class Repository
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("owner")]
        public GitUser Owner { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("full_name")]
        public string FullName { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("empty")]
        public bool Empty { get; set; }
        [JsonPropertyName("_private")]
        public bool Private { get; set; }
        [JsonPropertyName("fork")]
        public bool Fork { get; set; }
        [JsonPropertyName("template")]
        public bool Template { get; set; }
        [JsonPropertyName("parent")]
        public object Parent { get; set; }
        [JsonPropertyName("mirror")]
        public bool Mirror { get; set; }
        [JsonPropertyName("size")]
        public int Size { get; set; }
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }
        [JsonPropertyName("ssh_url")]
        public string SSHUrl { get; set; }
        [JsonPropertyName("clone_url")]
        public string CloneUrl { get; set; }
        [JsonPropertyName("original_url")]
        public string OriginalUrl { get; set; }
        [JsonPropertyName("website")]
        public string Website { get; set; }
        [JsonPropertyName("stars_count")]
        public int StarsCount { get; set; }
        [JsonPropertyName("forks_count")]
        public int forks_count { get; set; }
        [JsonPropertyName("watchers_count")]
        public int watchers_count { get; set; }
        [JsonPropertyName("open_issues_count")]
        public int open_issues_count { get; set; }
        [JsonPropertyName("open_pr_counter")]
        public int open_pr_counter { get; set; }
        [JsonPropertyName("release_counter")]
        public int release_counter { get; set; }
        [JsonPropertyName("default_branch")]
        public string default_branch { get; set; }
        [JsonPropertyName("archived")]
        public bool archived { get; set; }
        [JsonPropertyName("created_at")]
        public DateTime created_at { get; set; }
        [JsonPropertyName("updated_at")]
        public DateTime updated_at { get; set; }
        [JsonPropertyName("permissions")]
        public Permissions permissions { get; set; }
        [JsonPropertyName("has_issues")]
        public bool has_issues { get; set; }
        [JsonPropertyName("internal_tracker")]
        public Internal_Tracker internal_tracker { get; set; }
        [JsonPropertyName("has_wiki")]
        public bool has_wiki { get; set; }
        [JsonPropertyName("has_pull_requests")]
        public bool has_pull_requests { get; set; }
        [JsonPropertyName("ignore_whitespace_conflicts")]
        public bool ignore_whitespace_conflicts { get; set; }
        [JsonPropertyName("allow_merge_commits")]
        public bool allow_merge_commits { get; set; }
        [JsonPropertyName("allow_rebase")]
        public bool allow_rebase { get; set; }
        [JsonPropertyName("allow_rebase_explicit")]
        public bool allow_rebase_explicit { get; set; }
        [JsonPropertyName("allow_squash_merge")]
        public bool AllowSquashMerge { get; set; }
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }
    }

}
