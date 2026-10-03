using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PromptMotion.Auth;
using UnityEngine.Networking;

namespace PromptMotion.Http
{
    /// <summary>
    /// Network-layer abstraction for the Asset Store generator. Test code injects
    /// a fake transport so request/response wiring (URLs, headers, envelope
    /// parsing) can be exercised without touching the network.
    /// </summary>
    public interface IHttpTransport
    {
        Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(
            string url,
            TRequest body,
            IDictionary<string, string> headers,
            int timeoutSeconds,
            CancellationToken ct);

        /// <summary>
        /// GET binary bytes. Used to download generated FBXes from signed URLs.
        /// On success returns <see cref="ApiResponse{T}.Data"/> with the raw bytes.
        /// On HTTP error the body is parsed as a JSON envelope when present
        /// (signed-URL backends return <c>{ok: false, error: ...}</c> for expired
        /// tokens or invoice mismatches); otherwise a synthesised envelope keyed
        /// off the HTTP status is returned.
        /// </summary>
        Task<ApiResponse<byte[]>> GetBytesAsync(
            string url,
            IDictionary<string, string> headers,
            int timeoutSeconds,
            CancellationToken ct);
    }

    /// <summary>
    /// Production <see cref="IHttpTransport"/> over <see cref="UnityWebRequest"/>.
    /// Adds <c>Content-Type: application/json</c>, applies the per-request timeout,
    /// and folds network-level failures into the same <see cref="ApiResponse{T}"/>
    /// envelope so callers don't have to special-case them.
    /// </summary>
    public sealed class UnityWebRequestHttpTransport : IHttpTransport
    {
        public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(
            string url,
            TRequest body,
            IDictionary<string, string> headers,
            int timeoutSeconds,
            CancellationToken ct)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("url required", nameof(url));

            var json = body == null ? "{}" : JsonConvert.SerializeObject(body);
            var bytes = Encoding.UTF8.GetBytes(json);

            using var uwr = new UnityWebRequest(url, "POST");
            uwr.uploadHandler = new UploadHandlerRaw(bytes) { contentType = "application/json" };
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");
            uwr.SetRequestHeader("Accept", "application/json");
            uwr.timeout = Math.Max(1, timeoutSeconds);

            if (headers != null)
            {
                foreach (var kv in headers)
                {
                    uwr.SetRequestHeader(kv.Key, kv.Value);
                }
            }

            var tcs = new TaskCompletionSource<bool>();
            var op = uwr.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);

            using (ct.Register(() =>
            {
                try { uwr.Abort(); } catch { /* best-effort cancel */ }
                tcs.TrySetCanceled(ct);
            }))
            {
                try
                {
                    await tcs.Task;
                }
                catch (OperationCanceledException)
                {
                    return MakeClientError<TResponse>(ApiErrorCode.NetworkError, "Request was cancelled.");
                }
            }

            // UnityWebRequest distinguishes ConnectionError (network unreachable),
            // ProtocolError (HTTP 4xx/5xx with body), and DataProcessingError. For
            // the protocol case the server still returned a JSON envelope; try to
            // parse it before falling back to a synthetic NetworkError.
            var status = (int)uwr.responseCode;
            var text = uwr.downloadHandler != null ? uwr.downloadHandler.text : null;

#if UNITY_2020_1_OR_NEWER
            var connection = uwr.result == UnityWebRequest.Result.ConnectionError;
            var dataError = uwr.result == UnityWebRequest.Result.DataProcessingError;
#else
            var connection = uwr.isNetworkError;
            var dataError = false;
#endif

            if (connection)
            {
                // uwr.timeout fires as a connection error with status 0.
                var isTimeout = string.Equals(uwr.error, "Request timeout",
                    StringComparison.OrdinalIgnoreCase);
                return MakeClientError<TResponse>(
                    isTimeout ? ApiErrorCode.Timeout : ApiErrorCode.NetworkError,
                    uwr.error ?? "Network error.");
            }

            // Try to parse whatever body we got (success or protocol error).
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    var parsed = JsonConvert.DeserializeObject<ApiResponse<TResponse>>(text);
                    if (parsed != null) return parsed;
                }
                catch (JsonException)
                {
                    // fall through to synthetic envelope below
                }
            }

            if (dataError)
            {
                return MakeClientError<TResponse>(ApiErrorCode.NetworkError,
                    "Failed to read server response.");
            }

            // Body wasn't parseable — synthesise an envelope keyed off the HTTP
            // status so callers can still react.
            return MakeStatusError<TResponse>(status,
                string.IsNullOrEmpty(text) ? "Empty response body." : text);
        }

        public async Task<ApiResponse<byte[]>> GetBytesAsync(
            string url,
            IDictionary<string, string> headers,
            int timeoutSeconds,
            CancellationToken ct)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("url required", nameof(url));

            using var uwr = UnityWebRequest.Get(url);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.timeout = Math.Max(1, timeoutSeconds);

            if (headers != null)
            {
                foreach (var kv in headers)
                {
                    uwr.SetRequestHeader(kv.Key, kv.Value);
                }
            }

            var tcs = new TaskCompletionSource<bool>();
            var op = uwr.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);

            using (ct.Register(() =>
            {
                try { uwr.Abort(); } catch { /* best-effort cancel */ }
                tcs.TrySetCanceled(ct);
            }))
            {
                try
                {
                    await tcs.Task;
                }
                catch (OperationCanceledException)
                {
                    return MakeClientError<byte[]>(ApiErrorCode.NetworkError, "Download was cancelled.");
                }
            }

            var status = (int)uwr.responseCode;

#if UNITY_2020_1_OR_NEWER
            var connection = uwr.result == UnityWebRequest.Result.ConnectionError;
#else
            var connection = uwr.isNetworkError;
#endif

            if (connection)
            {
                var isTimeout = string.Equals(uwr.error, "Request timeout",
                    StringComparison.OrdinalIgnoreCase);
                return MakeClientError<byte[]>(
                    isTimeout ? ApiErrorCode.Timeout : ApiErrorCode.NetworkError,
                    uwr.error ?? "Network error.");
            }

            // If the server returned a JSON envelope (signed URL expired,
            // invoice mismatch, etc.) parse it as an error.
            var contentType = uwr.GetResponseHeader("Content-Type");
            var isJson = !string.IsNullOrEmpty(contentType) &&
                contentType.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0;

            if (status >= 400 || isJson)
            {
                var text = uwr.downloadHandler != null ? uwr.downloadHandler.text : null;
                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        var parsed = JsonConvert.DeserializeObject<ApiResponse<byte[]>>(text);
                        if (parsed != null && parsed.Error != null) return parsed;
                    }
                    catch (JsonException)
                    {
                        // fall through to status-keyed envelope
                    }
                }
                return MakeStatusError<byte[]>(status,
                    string.IsNullOrEmpty(text) ? "Empty response body." : text);
            }

            var bytes = uwr.downloadHandler?.data ?? Array.Empty<byte>();
            return new ApiResponse<byte[]>
            {
                Ok = true,
                Data = bytes,
            };
        }

        private static ApiResponse<T> MakeClientError<T>(ApiErrorCode code, string message)
        {
            return new ApiResponse<T>
            {
                Ok = false,
                Error = new ApiError
                {
                    CodeRaw = code.ToString(),
                    Message = message,
                    Details = new Dictionary<string, object>(),
                },
            };
        }

        private static ApiResponse<T> MakeStatusError<T>(int httpStatus, string body)
        {
            ApiErrorCode code;
            if (httpStatus == 0) code = ApiErrorCode.NetworkError;
            else if (httpStatus == 408) code = ApiErrorCode.Timeout;
            else if (httpStatus == 429) code = ApiErrorCode.RateLimited;
            else if (httpStatus == 503) code = ApiErrorCode.ServiceUnavailable;
            else if (httpStatus >= 500) code = ApiErrorCode.ServerError;
            else if (httpStatus == 400) code = ApiErrorCode.BadRequest;
            else code = ApiErrorCode.Unknown;

            return new ApiResponse<T>
            {
                Ok = false,
                Error = new ApiError
                {
                    CodeRaw = code.ToString(),
                    Message = $"HTTP {httpStatus}: {Truncate(body, 200)}",
                    Details = new Dictionary<string, object> { ["http_status"] = httpStatus },
                },
            };
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }
    }
}
