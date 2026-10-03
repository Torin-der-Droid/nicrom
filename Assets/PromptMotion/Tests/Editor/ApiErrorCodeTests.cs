using NUnit.Framework;
using PromptMotion.Auth;

namespace PromptMotion.Tests
{
    public class ApiErrorCodeTests
    {
        [TestCase("INVALID_INVOICE", ApiErrorCode.InvalidInvoice)]
        [TestCase("invalid_invoice", ApiErrorCode.InvalidInvoice)]
        [TestCase("Invalid_Invoice", ApiErrorCode.InvalidInvoice)]
        [TestCase("REFUNDED_INVOICE", ApiErrorCode.RefundedInvoice)]
        [TestCase("MACHINE_NOT_BOUND", ApiErrorCode.MachineNotBound)]
        [TestCase("MACHINE_LIMIT_REACHED", ApiErrorCode.MachineLimitReached)]
        [TestCase("QUOTA_EXHAUSTED", ApiErrorCode.QuotaExhausted)]
        [TestCase("RATE_LIMITED", ApiErrorCode.RateLimited)]
        [TestCase("UPSTREAM_ERROR", ApiErrorCode.UpstreamError)]
        [TestCase("SERVER_ERROR", ApiErrorCode.ServerError)]
        [TestCase("BAD_REQUEST", ApiErrorCode.BadRequest)]
        [TestCase("SERVICE_UNAVAILABLE", ApiErrorCode.ServiceUnavailable)]
        public void FromString_ParsesKnownCodes(string raw, ApiErrorCode expected)
        {
            Assert.AreEqual(expected, ApiErrorCodes.FromString(raw));
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("totally_unknown")]
        [TestCase("FOO_BAR_BAZ")]
        public void FromString_UnknownReturnsUnknown(string raw)
        {
            Assert.AreEqual(ApiErrorCode.Unknown, ApiErrorCodes.FromString(raw));
        }
    }
}
