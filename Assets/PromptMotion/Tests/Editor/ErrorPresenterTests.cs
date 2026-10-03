using System.Collections.Generic;
using NUnit.Framework;
using PromptMotion.Auth;
using PromptMotion.Http;
using PromptMotion.Views;

namespace PromptMotion.Tests
{
    public class ErrorPresenterTests
    {
        private static ApiError E(string code, string msg = null, int? retry = null, Dictionary<string, object> details = null)
            => new ApiError { CodeRaw = code, Message = msg, RetryAfterSeconds = retry, Details = details ?? new Dictionary<string, object>() };

        [Test]
        public void Map_InvalidInvoice_Retry()
        {
            var p = ErrorPresenter.Map(E("INVALID_INVOICE"));
            StringAssert.Contains("invoice number isn't recognized", p.Message);
            Assert.AreEqual(ErrorAction.Retry, p.Action);
            Assert.IsNull(p.AutoRetrySeconds);
        }

        [Test]
        public void Map_RefundedInvoice_EmailSupport()
        {
            var p = ErrorPresenter.Map(E("REFUNDED_INVOICE"));
            StringAssert.Contains("refunded", p.Message);
            Assert.AreEqual(ErrorAction.EmailSupport, p.Action);
        }

        [Test]
        public void Map_MachineNotBound_AutoRetry()
        {
            var p = ErrorPresenter.Map(E("MACHINE_NOT_BOUND"));
            Assert.AreEqual(ErrorAction.AutoRetry, p.Action);
            Assert.IsNotNull(p.AutoRetrySeconds);
        }

        [Test]
        public void Map_MachineLimitReached_AutoRetry()
        {
            var p = ErrorPresenter.Map(E("MACHINE_LIMIT_REACHED"));
            Assert.AreEqual(ErrorAction.AutoRetry, p.Action);
            Assert.IsNotNull(p.AutoRetrySeconds);
        }

        [Test]
        public void Map_QuotaExhausted_Lifetime_UpsellsWithQuotaInterpolation()
        {
            // Lite's trial is a lifetime quota: no reset, and the message steers
            // the customer toward upgrading rather than waiting for an allowance.
            var p = ErrorPresenter.Map(E("QUOTA_EXHAUSTED", details: new Dictionary<string, object>
            {
                ["monthly_quota"] = 5,
            }));
            Assert.AreEqual(ErrorAction.None, p.Action);
            StringAssert.Contains("5 free generations", p.Message);
            StringAssert.Contains("Pro", p.Message);
            StringAssert.DoesNotContain("next allowance", p.Message);
            StringAssert.DoesNotContain("this month", p.Message);
        }

        [Test]
        public void Map_RateLimited_AutoRetry_UsesRetryAfter()
        {
            var p = ErrorPresenter.Map(E("RATE_LIMITED", retry: 15));
            Assert.AreEqual(ErrorAction.AutoRetry, p.Action);
            Assert.AreEqual(15, p.AutoRetrySeconds);
            StringAssert.Contains("15s", p.Message);
        }

        [Test]
        public void Map_UpstreamError_NoQuotaUsed()
        {
            var p = ErrorPresenter.Map(E("UPSTREAM_ERROR"));
            Assert.AreEqual(ErrorAction.Retry, p.Action);
            StringAssert.Contains("No quota was used", p.Message);
        }

        [Test]
        public void Map_ServiceUnavailable_AutoRetry_DefaultsTo60()
        {
            var p = ErrorPresenter.Map(E("SERVICE_UNAVAILABLE")); // no retry-after
            Assert.AreEqual(ErrorAction.AutoRetry, p.Action);
            Assert.AreEqual(60, p.AutoRetrySeconds);
        }

        [Test]
        public void Map_ServerError_Retry()
        {
            var p = ErrorPresenter.Map(E("SERVER_ERROR"));
            Assert.AreEqual(ErrorAction.Retry, p.Action);
        }

        [Test]
        public void Map_BadRequest_Retry()
        {
            var p = ErrorPresenter.Map(E("BAD_REQUEST"));
            Assert.AreEqual(ErrorAction.Retry, p.Action);
        }

        [Test]
        public void Map_NetworkError_Retry()
        {
            var p = ErrorPresenter.Map(E("NetworkError"));
            Assert.AreEqual(ErrorAction.Retry, p.Action);
            StringAssert.Contains("internet connection", p.Message);
        }

        [Test]
        public void Map_Timeout_Retry()
        {
            var p = ErrorPresenter.Map(E("Timeout"));
            Assert.AreEqual(ErrorAction.Retry, p.Action);
            StringAssert.Contains("too long", p.Message);
        }

        [Test]
        public void Map_NullError_FallsBackToRetry()
        {
            var p = ErrorPresenter.Map(null);
            Assert.AreEqual(ErrorAction.Retry, p.Action);
            Assert.IsNotNull(p.Message);
        }
    }
}
