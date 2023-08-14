using System;
using System.Text.Json.Serialization;

namespace PipelineCommon.Models.Webhook
{
    public class Commit
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("author")]
        public Author Author { get; set; }
        [JsonPropertyName("committer")]
        public Committer Committer { get; set; }
        [JsonPropertyName("verification")]
        public object Verification { get; set; }
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
        [JsonPropertyName("added")]
        public object Added { get; set; }
        [JsonPropertyName("removed")]
        public object Removed { get; set; }
        [JsonPropertyName("modified")]
        public object Modified { get; set; }
    }

}
