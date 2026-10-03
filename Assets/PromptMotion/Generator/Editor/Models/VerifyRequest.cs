using Newtonsoft.Json;

namespace PromptMotion.Models
{
    /// <summary>
    /// Body for <c>POST /prod/verify</c>.
    /// </summary>
    public sealed class VerifyRequest
    {
        [JsonProperty("invoice")]
        public string Invoice { get; set; }
    }
}
