using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PromptMotion.Models;
using PromptMotion.Settings;

namespace PromptMotion.Http
{
    /// <summary>
    /// High-level HTTP client used by the editor window. Owns the per-request
    /// headers (client version + auth type) so callers don't need to know the
    /// wire format. Endpoint methods return the parsed envelope — network errors
    /// are mapped into the same envelope shape so callers have one code path.
    ///
    /// This is the Lite (free-trial) client: every request carries
    /// <c>X-PromptMotion-Auth-Type: trial</c>, and the <c>invoice</c> field on
    /// each request body carries the customer's email rather than an Asset Store
    /// invoice number. The backend resolves that email against its
    /// <c>email_trials</c> table. Activation is a two-step email→code flow
    /// (<see cref="TrialRequestAsync"/> then <see cref="TrialVerifyAsync"/>).
    /// </summary>
    public sealed class AssetStoreApiClient
    {
        /// <summary>Selects the backend's trial credential namespace.</summary>
        public const string TrialAuthType = "trial";

        private readonly PromptMotionSettings _settings;
        private readonly IHttpTransport _transport;

        public AssetStoreApiClient(PromptMotionSettings settings, IHttpTransport transport)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        /// <summary>
        /// Step 1 of activation: ask the backend to email a 6-digit code to
        /// <paramref name="email"/>. <paramref name="activationNonce"/> is the
        /// per-install nonce from <see cref="PromptMotion.Auth.SecureStore"/>.
        /// </summary>
        public Task<ApiResponse<TrialRequestResult>> TrialRequestAsync(
            string email, string activationNonce, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(email)) throw new ArgumentException("email required", nameof(email));
            if (string.IsNullOrEmpty(activationNonce)) throw new ArgumentException("activationNonce required", nameof(activationNonce));
            var body = new TrialRequestBody { Email = email, ActivationNonce = activationNonce };
            return _transport.PostAsync<TrialRequestBody, TrialRequestResult>(
                BuildUrl("/prod/trial-request"),
                body,
                BuildHeaders(),
                _settings.VerifyTimeoutSeconds,
                ct);
        }

        /// <summary>
        /// Step 2 of activation: submit the emailed <paramref name="code"/> to
        /// finish activation. On success the response carries the same shape as
        /// <c>/prod/verify</c> (<see cref="VerifyResult"/>) with the email in the
        /// <c>invoice</c> field, <c>tier = "trial"</c> and the lifetime quota.
        /// </summary>
        public Task<ApiResponse<VerifyResult>> TrialVerifyAsync(
            string email, string code, string activationNonce, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(email)) throw new ArgumentException("email required", nameof(email));
            if (string.IsNullOrEmpty(code)) throw new ArgumentException("code required", nameof(code));
            if (string.IsNullOrEmpty(activationNonce)) throw new ArgumentException("activationNonce required", nameof(activationNonce));
            var body = new TrialVerifyBody { Email = email, Code = code, ActivationNonce = activationNonce };
            return _transport.PostAsync<TrialVerifyBody, VerifyResult>(
                BuildUrl("/prod/trial-verify"),
                body,
                BuildHeaders(),
                _settings.VerifyTimeoutSeconds,
                ct);
        }

        /// <summary>
        /// Re-reads the trial's lifetime quota for <paramref name="email"/>.
        /// Same envelope as the paid status endpoint; the backend branches on
        /// the trial auth-type header.
        /// </summary>
        public Task<ApiResponse<StatusResult>> StatusAsync(string email, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(email)) throw new ArgumentException("email required", nameof(email));
            var body = new StatusRequest { Invoice = email };
            return _transport.PostAsync<StatusRequest, StatusResult>(
                BuildUrl("/prod/status"),
                body,
                BuildHeaders(),
                _settings.StatusTimeoutSeconds,
                ct);
        }

        public Task<ApiResponse<GenerateResult>> GenerateAsync(
            string email,
            string prompt,
            int durationSeconds,
            int? seed = null,
            List<object> constraints = null,
            string skeleton = "humanoid",
            string format = "fbx",
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(email)) throw new ArgumentException("email required", nameof(email));
            if (string.IsNullOrEmpty(prompt)) throw new ArgumentException("prompt required", nameof(prompt));

            var body = new GenerateRequest
            {
                Invoice = email,
                Prompt = prompt,
                DurationSeconds = durationSeconds,
                Seed = seed,
                Skeleton = string.IsNullOrEmpty(skeleton) ? "humanoid" : skeleton,
                Format = string.IsNullOrEmpty(format) ? "fbx" : format,
                Constraints = constraints ?? new List<object>(),
            };
            return _transport.PostAsync<GenerateRequest, GenerateResult>(
                BuildUrl("/prod/generate"),
                body,
                BuildHeaders(),
                _settings.GenerateTimeoutSeconds,
                ct);
        }

        /// <summary>
        /// GET the FBX bytes from a signed URL returned by <see cref="GenerateAsync"/>.
        /// </summary>
        public Task<ApiResponse<byte[]>> DownloadFbxAsync(string signedUrl, string email, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(signedUrl)) throw new ArgumentException("signedUrl required", nameof(signedUrl));
            if (string.IsNullOrEmpty(email)) throw new ArgumentException("email required", nameof(email));
            return _transport.GetBytesAsync(
                signedUrl,
                BuildHeaders(email),
                _settings.GenerateTimeoutSeconds,
                ct);
        }

        private string BuildUrl(string path)
        {
            var baseUrl = (_settings.BaseUrl ?? string.Empty).TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new InvalidOperationException("BaseUrl is not configured.");
            }
            return baseUrl + (path.StartsWith("/") ? path : "/" + path);
        }

        private IDictionary<string, string> BuildHeaders()
        {
            return new Dictionary<string, string>
            {
                ["X-Client-Version"] = _settings.ClientVersion,
                // Lite is always a trial client — the backend resolves the
                // request's email against email_trials rather than invoices.
                ["X-PromptMotion-Auth-Type"] = TrialAuthType,
            };
        }

        private IDictionary<string, string> BuildHeaders(string invoice)
        {
            var headers = BuildHeaders();
            if (!string.IsNullOrEmpty(invoice))
            {
                headers["X-Invoice"] = invoice;
            }
            return headers;
        }
    }
}
