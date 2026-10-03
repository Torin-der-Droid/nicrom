using Newtonsoft.Json;

namespace PromptMotion.Models
{
    /// <summary>
    /// Body for <c>POST /prod/trial-request</c>. The customer's email plus a
    /// per-install activation nonce (see <see cref="PromptMotion.Auth.SecureStore"/>);
    /// the backend emails a 6-digit code. <c>X-Client-Version</c> rides as a header.
    /// </summary>
    public sealed class TrialRequestBody
    {
        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("activation_nonce")]
        public string ActivationNonce { get; set; }
    }
}
