using System;
using System.Text.Json.Serialization;

namespace PipelineCommon.Models.Webhook
{
    public class GitUser
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("login")]
        public string Login { get; set; }
        [JsonPropertyName("full_name")]
        public string FullName { get; set; }
        [JsonPropertyName("email")]
        public string Email { get; set; }
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }
        [JsonPropertyName("language")]
        public string Language { get; set; }
        [JsonPropertyName("is_admin")]
        public bool IsAdmin { get; set; }
        [JsonPropertyName("last_login")]
        public DateTime LastLogin { get; set; }
        [JsonPropertyName("created")]
        public DateTime Created { get; set; }
        [JsonPropertyName("username")]
        public string Username { get; set; }
    }

}
