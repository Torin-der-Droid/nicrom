using Newtonsoft.Json;

namespace PromptMotion.Models
{
    /// <summary>
    /// <c>data</c> payload of a successful <c>POST /prod/verify</c> response.
    /// Field names match the backend's snake_case JSON.
    /// </summary>
    public sealed class VerifyResult
    {
        [JsonProperty("invoice")]
        public string Invoice { get; set; }

        [JsonProperty("tier")]
        public string Tier { get; set; }

        [JsonProperty("monthly_quota")]
        public int MonthlyQuota { get; set; }

        [JsonProperty("used_this_month")]
        public int UsedThisMonth { get; set; }
    }
}
