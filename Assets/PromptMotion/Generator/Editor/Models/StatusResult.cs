using Newtonsoft.Json;

namespace PromptMotion.Models
{
    /// <summary>
    /// <c>data</c> payload of a successful <c>POST /prod/status</c> response. Used
    /// by the "Refresh status" button in the Ready banner to display current
    /// quota usage. Field names match the backend's snake_case JSON.
    /// </summary>
    public sealed class StatusResult
    {
        [JsonProperty("invoice")]
        public string Invoice { get; set; }

        [JsonProperty("tier")]
        public string Tier { get; set; }

        [JsonProperty("monthly_quota")]
        public int MonthlyQuota { get; set; }

        [JsonProperty("used_this_month")]
        public int UsedThisMonth { get; set; }

        [JsonProperty("remaining")]
        public int RemainingThisMonth { get; set; }

        // Lifetime trial quota has no reset, so the backend sends null here.
        [JsonProperty("resets_at")]
        public string WindowResetsAt { get; set; }
    }
}
