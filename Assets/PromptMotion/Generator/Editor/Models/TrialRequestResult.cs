using Newtonsoft.Json;

namespace PromptMotion.Models
{
    /// <summary>
    /// <c>data</c> payload of a successful <c>POST /prod/trial-request</c>
    /// response. No PII is echoed back — not even the code; the customer must
    /// read it from their email.
    /// </summary>
    public sealed class TrialRequestResult
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("expires_in_seconds")]
        public int ExpiresInSeconds { get; set; }
    }
}
