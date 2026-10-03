using Newtonsoft.Json;

namespace PromptMotion.Models
{
    /// <summary>
    /// <c>data</c> payload of a successful <c>POST /prod/generate</c> response.
    /// Field names match the backend's snake_case JSON. <see cref="Cached"/>
    /// is true when the backend served a cached result (no quota debit).
    /// </summary>
    public sealed class GenerateResult
    {
        [JsonProperty("job_id")]
        public string JobId { get; set; }

        /// <summary>Signed URL the client GETs to download the FBX bytes.</summary>
        [JsonProperty("fbx_url")]
        public string FbxUrl { get; set; }

        [JsonProperty("duration_seconds")]
        public int DurationSeconds { get; set; }

        /// <summary>0 on cache hit, 1 on a fresh generation. Wire format
        /// remains <c>credits_charged</c> for backwards compatibility with the
        /// backend; the C# field uses the user-facing "generations" vocabulary.</summary>
        [JsonProperty("credits_charged")]
        public int GenerationsCharged { get; set; }

        [JsonProperty("credits_remaining")]
        public int GenerationsRemaining { get; set; }

        [JsonProperty("cached")]
        public bool Cached { get; set; }

        /// <summary>ISO-8601 UTC timestamp when the job completed.</summary>
        [JsonProperty("created_at")]
        public string CreatedAt { get; set; }
    }
}
