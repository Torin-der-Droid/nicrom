using Newtonsoft.Json;

namespace PromptMotion.Models
{
    /// <summary>
    /// Body for <c>POST /prod/trial-verify</c>. Carries the 6-digit code the
    /// customer received by email plus the same email + activation nonce sent to
    /// <c>/prod/trial-request</c>. A successful response uses the same envelope
    /// shape as <c>/prod/verify</c> (see <see cref="VerifyResult"/>).
    /// </summary>
    public sealed class TrialVerifyBody
    {
        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("activation_nonce")]
        public string ActivationNonce { get; set; }
    }
}
