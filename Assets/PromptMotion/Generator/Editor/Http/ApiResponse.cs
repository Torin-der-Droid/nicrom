using System.Collections.Generic;
using Newtonsoft.Json;
using PromptMotion.Auth;

namespace PromptMotion.Http
{
    /// <summary>
    /// Strongly-typed wrapper around the backend's standard envelope:
    /// <c>{ ok: bool, data: T, error: { code, message, retry_after_seconds, details } }</c>.
    /// </summary>
    public sealed class ApiResponse<T>
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("data")]
        public T Data { get; set; }

        [JsonProperty("error")]
        public ApiError Error { get; set; }
    }

    public sealed class ApiError
    {
        /// <summary>Raw <c>code</c> string from the server.</summary>
        [JsonProperty("code")]
        public string CodeRaw { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("retry_after_seconds")]
        public int? RetryAfterSeconds { get; set; }

        [JsonProperty("details")]
        public Dictionary<string, object> Details { get; set; }

        [JsonIgnore]
        public ApiErrorCode Code => ApiErrorCodes.FromString(CodeRaw);
    }
}
