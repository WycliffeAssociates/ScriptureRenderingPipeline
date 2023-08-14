using System.Text.Json.Serialization;

namespace PipelineCommon.Models.Webhook
{
    public class Internal_Tracker
    {

        [JsonPropertyName("enable_time_tracker")]
        public bool EnableTimeTracker { get; set; }
        [JsonPropertyName("allow_only_contributors_to_track_time")]
        public bool AllowOnlyContributorsToTrackTime { get; set; }
        [JsonPropertyName("enable_issue_dependencies")]
        public bool EnableIssueDependencies { get; set; }
    }

}
