using System.Collections.Generic;
using PromptMotion.Auth;
using PromptMotion.Http;

namespace PromptMotion.Views
{
    /// <summary>
    /// Recovery action paired with an <see cref="ErrorPresentation"/>. Drives the
    /// button label and side-effect when the user clicks the action button on a
    /// failed generation.
    /// </summary>
    public enum ErrorAction
    {
        None,
        Retry,
        EmailSupport,
        RefreshStatus,
        AutoRetry,
    }

    /// <summary>
    /// View-model returned by <see cref="ErrorPresenter.Map"/>. The window
    /// renders <see cref="Message"/> in red, shows a button labelled
    /// <see cref="ActionLabel"/> wired to <see cref="Action"/>, and — if
    /// <see cref="AutoRetrySeconds"/> is non-null — counts down before
    /// automatically retrying the previous request once.
    /// </summary>
    public sealed class ErrorPresentation
    {
        public string Message;
        public ErrorAction Action;
        public string ActionLabel;
        public int? AutoRetrySeconds;
    }

    /// <summary>
    /// Maps backend / client error codes to user-facing messages and recovery
    /// actions. Uses <see cref="ApiError.Details"/> + <see cref="ApiError.RetryAfterSeconds"/>
    /// to interpolate retry windows and quota info where available.
    /// </summary>
    public static class ErrorPresenter
    {
        public static ErrorPresentation Map(ApiError error)
        {
            if (error == null)
            {
                return new ErrorPresentation
                {
                    Message = "An unknown error occurred.",
                    Action = ErrorAction.Retry,
                    ActionLabel = "Try again",
                };
            }

            switch (error.Code)
            {
                case ApiErrorCode.InvalidInvoice:
                    return new ErrorPresentation
                    {
                        Message = "This invoice number isn't recognized. Double-check the value from your Unity Asset Store account.",
                        Action = ErrorAction.Retry,
                        ActionLabel = "Try again",
                    };

                case ApiErrorCode.RefundedInvoice:
                    return new ErrorPresentation
                    {
                        Message = "This invoice was refunded. Please contact us if this is unexpected.",
                        Action = ErrorAction.EmailSupport,
                        ActionLabel = "Email support",
                    };

                case ApiErrorCode.MachineNotBound:
                    return new ErrorPresentation
                    {
                        Message = "Verifying with the server…",
                        Action = ErrorAction.AutoRetry,
                        ActionLabel = "Try again",
                        AutoRetrySeconds = 3,
                    };

                case ApiErrorCode.MachineLimitReached:
                    return new ErrorPresentation
                    {
                        Message = "Re-verifying for this machine…",
                        Action = ErrorAction.AutoRetry,
                        ActionLabel = "Try again",
                        AutoRetrySeconds = 3,
                    };

                case ApiErrorCode.QuotaExhausted:
                {
                    // Lite is a lifetime trial — there's no monthly reset, so the
                    // message frames the next step as upgrading. The bundled
                    // starter animations remain usable regardless.
                    var quota = TryReadInt(error.Details, "monthly_quota");
                    var msg = quota.HasValue
                        ? $"You've used all {quota.Value} free generations."
                        : "You've used all of your free generations.";
                    msg += " Upgrade to PromptMotion (50/month) or PromptMotion Pro (150/month) " +
                           "to keep generating. Your already-generated clips remain yours.";
                    return new ErrorPresentation
                    {
                        Message = msg,
                        Action = ErrorAction.None,
                        ActionLabel = "OK",
                    };
                }

                case ApiErrorCode.RateLimited:
                {
                    var retry = error.RetryAfterSeconds ?? 30;
                    return new ErrorPresentation
                    {
                        Message = $"Too many requests. Trying again in {retry}s.",
                        Action = ErrorAction.AutoRetry,
                        ActionLabel = "Retry now",
                        AutoRetrySeconds = retry,
                    };
                }

                case ApiErrorCode.UpstreamError:
                    return new ErrorPresentation
                    {
                        Message = "Generation failed on our end. No quota was used. Please try again.",
                        Action = ErrorAction.Retry,
                        ActionLabel = "Try again",
                    };

                case ApiErrorCode.ServiceUnavailable:
                {
                    var retry = error.RetryAfterSeconds ?? 60;
                    return new ErrorPresentation
                    {
                        Message = $"Server is busy. Trying again in {retry}s.",
                        Action = ErrorAction.AutoRetry,
                        ActionLabel = "Retry now",
                        AutoRetrySeconds = retry,
                    };
                }

                case ApiErrorCode.ServerError:
                    return new ErrorPresentation
                    {
                        Message = "Unexpected server error. Please try again in a few minutes, or contact support if this persists.",
                        Action = ErrorAction.Retry,
                        ActionLabel = "Try again",
                    };

                case ApiErrorCode.BadRequest:
                    return new ErrorPresentation
                    {
                        Message = "Something's wrong with your request. Please try again.",
                        Action = ErrorAction.Retry,
                        ActionLabel = "Try again",
                    };

                case ApiErrorCode.NetworkError:
                    return new ErrorPresentation
                    {
                        Message = "Couldn't reach the server. Check your internet connection.",
                        Action = ErrorAction.Retry,
                        ActionLabel = "Try again",
                    };

                case ApiErrorCode.Timeout:
                    return new ErrorPresentation
                    {
                        Message = "Request took too long. The server might be overloaded. Try again in a moment.",
                        Action = ErrorAction.Retry,
                        ActionLabel = "Try again",
                    };

                default:
                    var fallback = string.IsNullOrEmpty(error.Message) ? "An unexpected error occurred." : error.Message;
                    return new ErrorPresentation
                    {
                        Message = fallback,
                        Action = ErrorAction.Retry,
                        ActionLabel = "Try again",
                    };
            }
        }

        public const string SupportEmailUrl = "mailto:ragendom@gmail.com?subject=%5BPromptMotion%20Lite%5D%20support%20request";

        private static int? TryReadInt(IDictionary<string, object> details, string key)
        {
            if (details == null || !details.TryGetValue(key, out var raw) || raw == null) return null;
            if (raw is int i) return i;
            if (raw is long l) return (int)l;
            if (raw is System.IConvertible)
            {
                try { return System.Convert.ToInt32(raw); } catch { /* fall through */ }
            }
            if (int.TryParse(raw.ToString(), out var parsed)) return parsed;
            return null;
        }

        private static int? TryReadDaysUntil(IDictionary<string, object> details, string key)
        {
            if (details == null || !details.TryGetValue(key, out var raw) || raw == null) return null;
            var s = raw.ToString();
            if (string.IsNullOrEmpty(s)) return null;
            if (!System.DateTime.TryParse(
                    s,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var when)) return null;
            var delta = when - System.DateTime.UtcNow;
            return (int)System.Math.Ceiling(delta.TotalDays);
        }
    }
}
