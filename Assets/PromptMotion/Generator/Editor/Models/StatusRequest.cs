using Newtonsoft.Json;

namespace PromptMotion.Models
{
    /// <summary>
    /// Body for <c>POST /prod/status</c>. The backend rejects unknown fields
    /// with 422, so this is intentionally minimal — client version travels as
    /// a header, not in the JSON.
    /// </summary>
    public sealed class StatusRequest
    {
        [JsonProperty("invoice")]
        public string Invoice { get; set; }
    }
}
