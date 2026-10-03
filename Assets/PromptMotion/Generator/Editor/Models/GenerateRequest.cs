using System.Collections.Generic;
using Newtonsoft.Json;

namespace PromptMotion.Models
{
    /// <summary>
    /// Body for <c>POST /prod/generate</c>. Field names match the backend's
    /// snake_case JSON. <c>X-Client-Version</c> rides as a header.
    /// </summary>
    public sealed class GenerateRequest
    {
        [JsonProperty("invoice")]
        public string Invoice { get; set; }

        [JsonProperty("prompt")]
        public string Prompt { get; set; }

        [JsonProperty("duration_seconds")]
        public int DurationSeconds { get; set; }

        /// <summary>RNG seed; null asks the backend to pick one.</summary>
        [JsonProperty("seed", NullValueHandling = NullValueHandling.Ignore)]
        public int? Seed { get; set; }

        [JsonProperty("skeleton")]
        public string Skeleton { get; set; } = "humanoid";

        [JsonProperty("format")]
        public string Format { get; set; } = "fbx";

        /// <summary>Reserved for future motion constraints; send an empty array.</summary>
        [JsonProperty("constraints")]
        public List<object> Constraints { get; set; } = new List<object>();
    }
}
