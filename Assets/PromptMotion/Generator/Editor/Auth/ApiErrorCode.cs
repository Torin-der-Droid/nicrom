using System;

namespace PromptMotion.Auth
{
    /// <summary>
    /// Stable error codes mirroring the backend's <c>error.code</c> field plus
    /// client-side network failures. Used by the UI's <c>ErrorPresenter</c> to
    /// pick an appropriate banner message and recovery action.
    /// </summary>
    public enum ApiErrorCode
    {
        Unknown,
        InvalidInvoice,
        RefundedInvoice,
        MachineNotBound,
        MachineLimitReached,
        QuotaExhausted,
        RateLimited,
        UpstreamError,
        ServerError,
        BadRequest,
        ServiceUnavailable,
        // Free-trial activation codes (Lite tier). Sent by /prod/trial-request
        // and /prod/trial-verify.
        InvalidEmail,
        DisposableEmail,
        TrialAlreadyUsed,
        TrialNonceConflict,
        IpRateLimited,
        TrialCodeExpired,
        TrialCodeInvalid,
        // Client-side outcomes — never sent by the server.
        NetworkError,
        Timeout,
    }

    public static class ApiErrorCodes
    {
        public static ApiErrorCode FromString(string code)
        {
            if (string.IsNullOrEmpty(code)) return ApiErrorCode.Unknown;
            // Backend uses SCREAMING_SNAKE_CASE. Normalise for comparison.
            var normalized = code.Trim().ToUpperInvariant();
            switch (normalized)
            {
                case "INVALID_INVOICE": return ApiErrorCode.InvalidInvoice;
                case "REFUNDED_INVOICE": return ApiErrorCode.RefundedInvoice;
                case "MACHINE_NOT_BOUND": return ApiErrorCode.MachineNotBound;
                case "MACHINE_LIMIT_REACHED": return ApiErrorCode.MachineLimitReached;
                case "QUOTA_EXHAUSTED": return ApiErrorCode.QuotaExhausted;
                case "RATE_LIMITED": return ApiErrorCode.RateLimited;
                case "UPSTREAM_ERROR": return ApiErrorCode.UpstreamError;
                case "SERVER_ERROR": return ApiErrorCode.ServerError;
                case "BAD_REQUEST": return ApiErrorCode.BadRequest;
                case "SERVICE_UNAVAILABLE": return ApiErrorCode.ServiceUnavailable;
                case "INVALID_EMAIL": return ApiErrorCode.InvalidEmail;
                case "DISPOSABLE_EMAIL": return ApiErrorCode.DisposableEmail;
                case "TRIAL_ALREADY_USED": return ApiErrorCode.TrialAlreadyUsed;
                case "TRIAL_NONCE_CONFLICT": return ApiErrorCode.TrialNonceConflict;
                case "IP_RATE_LIMITED": return ApiErrorCode.IpRateLimited;
                case "TRIAL_CODE_EXPIRED": return ApiErrorCode.TrialCodeExpired;
                case "TRIAL_CODE_INVALID": return ApiErrorCode.TrialCodeInvalid;
                case "AUTH_ERROR": return ApiErrorCode.InvalidInvoice; // Generic auth → treat as invalid invoice for UI.
            }
            // Fallback: the transport emits client-side codes as the PascalCase
            // enum name (e.g. "NetworkError", "Timeout"). Parse those back.
            if (Enum.TryParse<ApiErrorCode>(code.Trim(), ignoreCase: true, out var parsed))
            {
                return parsed;
            }
            return ApiErrorCode.Unknown;
        }
    }
}
