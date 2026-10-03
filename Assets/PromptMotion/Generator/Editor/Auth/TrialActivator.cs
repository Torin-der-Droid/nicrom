using System.Threading;
using System.Threading.Tasks;
using PromptMotion.Http;
using PromptMotion.Models;

namespace PromptMotion.Auth
{
    /// <summary>
    /// Higher-level orchestrator over <see cref="AssetStoreApiClient"/> for the
    /// Lite tier's two-step email→code activation:
    ///   1. <see cref="RequestCodeAsync"/> — backend emails a 6-digit code.
    ///   2. <see cref="VerifyCodeAsync"/> — submit the code to activate the trial.
    /// Persistence (<see cref="SecureStore"/>) is intentionally NOT done here —
    /// the UI layer owns it so cancelled or failed flows don't leak state.
    /// </summary>
    public sealed class TrialActivator
    {
        private readonly AssetStoreApiClient _client;

        public TrialActivator(AssetStoreApiClient client)
        {
            _client = client;
        }

        public async Task<TrialRequestOutcome> RequestCodeAsync(string email, string activationNonce, CancellationToken ct)
        {
            var resp = await _client.TrialRequestAsync(email, activationNonce, ct);
            if (resp == null)
            {
                return new TrialRequestOutcome
                {
                    Ok = false,
                    Error = new ApiError { CodeRaw = ApiErrorCode.Unknown.ToString(), Message = "No response." },
                };
            }
            if (!resp.Ok || resp.Error != null)
            {
                return new TrialRequestOutcome { Ok = false, Error = resp.Error };
            }
            return new TrialRequestOutcome
            {
                Ok = true,
                ExpiresInSeconds = resp.Data?.ExpiresInSeconds ?? 0,
            };
        }

        public async Task<VerifierResult> VerifyCodeAsync(string email, string code, string activationNonce, CancellationToken ct)
        {
            var resp = await _client.TrialVerifyAsync(email, code, activationNonce, ct);
            return Interpret(resp);
        }

        private static VerifierResult Interpret(ApiResponse<VerifyResult> resp)
        {
            if (resp == null)
            {
                return new VerifierResult
                {
                    Outcome = VerifierOutcome.Failed,
                    Error = new ApiError { CodeRaw = ApiErrorCode.Unknown.ToString(), Message = "No response." },
                };
            }
            if (!resp.Ok || resp.Error != null)
            {
                return new VerifierResult { Outcome = VerifierOutcome.Failed, Error = resp.Error };
            }
            if (resp.Data == null)
            {
                return new VerifierResult
                {
                    Outcome = VerifierOutcome.Failed,
                    Error = new ApiError { CodeRaw = ApiErrorCode.Unknown.ToString(), Message = "Empty data envelope." },
                };
            }

            return new VerifierResult { Outcome = VerifierOutcome.Verified, Data = resp.Data };
        }
    }

    public enum VerifierOutcome
    {
        Verified,
        Failed,
    }

    public sealed class VerifierResult
    {
        public VerifierOutcome Outcome { get; set; }
        public VerifyResult Data { get; set; }
        public ApiError Error { get; set; }
    }

    /// <summary>Result of the trial-request (code-email) step.</summary>
    public sealed class TrialRequestOutcome
    {
        public bool Ok { get; set; }
        public int ExpiresInSeconds { get; set; }
        public ApiError Error { get; set; }
    }
}
