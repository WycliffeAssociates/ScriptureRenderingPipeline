using Newtonsoft.Json;
using System;

namespace PipelineCommon.Models.Webhook
{
    public class GitUser
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("login")]
        public string Login { get; set; }
        [JsonProperty("full_name")]
        public string FullName { get; set; }
        [JsonProperty("email")]
        public string Email { get; set; }
        [JsonProperty("avatar_url")]
        public string AvatarUrl { get; set; }
        [JsonProperty("language")]
        public string Language { get; set; }
        [JsonProperty("is_admin")]
        public bool IsAdmin { get; set; }
        [JsonProperty("last_login")]
        public DateTime LastLogin { get; set; }
        [JsonProperty("created")]
        public DateTime Created { get; set; }
        [JsonProperty("username")]
        public string Username { get; set; }
    }

}
