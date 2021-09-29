using Newtonsoft.Json;

namespace PipelineCommon.Models.Webhook
{
    public class Internal_Tracker
    {

        [JsonProperty("enable_time_tracker")]
        public bool EnableTimeTracker { get; set; }
        [JsonProperty("allow_only_contributors_to_track_time")]
        public bool AllowOnlyContributorsToTrackTime { get; set; }
        [JsonProperty("enable_issue_dependencies")]
        public bool EnableIssueDependencies { get; set; }
    }

}
