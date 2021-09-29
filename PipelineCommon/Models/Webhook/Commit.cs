using Newtonsoft.Json;
using System;

namespace PipelineCommon.Models.Webhook
{
    public class Commit
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
        [JsonProperty("url")]
        public string Url { get; set; }
        [JsonProperty("author")]
        public Author Author { get; set; }
        [JsonProperty("committer")]
        public Committer Committer { get; set; }
        [JsonProperty("verification")]
        public object Verification { get; set; }
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
        [JsonProperty("added")]
        public object Added { get; set; }
        [JsonProperty("removed")]
        public object Removed { get; set; }
        [JsonProperty("modified")]
        public object Modified { get; set; }
    }

}
