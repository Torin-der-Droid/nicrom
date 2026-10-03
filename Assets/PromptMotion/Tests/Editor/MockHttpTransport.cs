using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PromptMotion.Http;

namespace PromptMotion.Tests
{
    /// <summary>
    /// Test double for <see cref="IHttpTransport"/>. Records every outgoing
    /// request and returns canned responses keyed by URL path. Tests use it to
    /// drive the AssetStoreApiClient + TrialActivator + window flows without
    /// touching the network.
    /// </summary>
    internal sealed class MockHttpTransport : IHttpTransport
    {
        public sealed class SentRequest
        {
            public string Url;
            public string BodyJson;
            public IDictionary<string, string> Headers;
            public int TimeoutSeconds;
        }

        private sealed class CannedResponse
        {
            public bool Ok;
            public object Data;
            public ApiError Error;
        }

        private sealed class CannedBytesResponse
        {
            public bool Ok;
            public byte[] Data;
            public ApiError Error;
            public bool Throw;
        }

        private readonly Dictionary<string, Queue<CannedResponse>> _byPath
            = new Dictionary<string, Queue<CannedResponse>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Queue<CannedBytesResponse>> _bytesByPath
            = new Dictionary<string, Queue<CannedBytesResponse>>(StringComparer.OrdinalIgnoreCase);

        public List<SentRequest> Sent { get; } = new List<SentRequest>();
        public List<SentRequest> SentGets { get; } = new List<SentRequest>();

        public TaskCompletionSource<bool> PostGate;

        public void EnqueueOk<TResponse>(string urlPath, TResponse data)
        {
            Enqueue(urlPath, new CannedResponse { Ok = true, Data = data });
        }

        public void EnqueueError(string urlPath, ApiError error)
        {
            Enqueue(urlPath, new CannedResponse { Ok = false, Error = error });
        }

        public void EnqueueBytesOk(string urlPath, byte[] data)
        {
            EnqueueBytes(urlPath, new CannedBytesResponse { Ok = true, Data = data });
        }

        public void EnqueueBytesError(string urlPath, ApiError error)
        {
            EnqueueBytes(urlPath, new CannedBytesResponse { Ok = false, Error = error });
        }

        public void EnqueueBytesThrow(string urlPath)
        {
            EnqueueBytes(urlPath, new CannedBytesResponse { Throw = true });
        }

        private void EnqueueBytes(string urlPath, CannedBytesResponse r)
        {
            if (!_bytesByPath.TryGetValue(urlPath, out var q))
            {
                q = new Queue<CannedBytesResponse>();
                _bytesByPath[urlPath] = q;
            }
            q.Enqueue(r);
        }

        private void Enqueue(string urlPath, CannedResponse r)
        {
            if (!_byPath.TryGetValue(urlPath, out var q))
            {
                q = new Queue<CannedResponse>();
                _byPath[urlPath] = q;
            }
            q.Enqueue(r);
        }

        public Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(
            string url,
            TRequest body,
            IDictionary<string, string> headers,
            int timeoutSeconds,
            CancellationToken ct)
        {
            Sent.Add(new SentRequest
            {
                Url = url,
                BodyJson = body == null ? null : JsonConvert.SerializeObject(body),
                Headers = headers == null ? new Dictionary<string, string>() : new Dictionary<string, string>(headers),
                TimeoutSeconds = timeoutSeconds,
            });

            if (ct.IsCancellationRequested)
                return Task.FromCanceled<ApiResponse<TResponse>>(ct);

            CannedResponse canned = null;
            foreach (var kv in _byPath)
            {
                if (url.EndsWith(kv.Key, StringComparison.OrdinalIgnoreCase) && kv.Value.Count > 0)
                {
                    canned = kv.Value.Dequeue();
                    break;
                }
            }

            if (canned == null)
            {
                throw new InvalidOperationException(
                    "MockHttpTransport: no canned response for " + url);
            }

            ApiResponse<TResponse> envelope;
            if (canned.Ok)
            {
                envelope = new ApiResponse<TResponse>
                {
                    Ok = true,
                    Data = canned.Data is TResponse typed ? typed : default,
                };
            }
            else
            {
                envelope = new ApiResponse<TResponse>
                {
                    Ok = false,
                    Error = canned.Error,
                };
            }
            if (PostGate != null)
            {
                var g = PostGate;
                PostGate = null;
                async Task<ApiResponse<TResponse>> WaitAndReturn()
                {
                    await g.Task;
                    return envelope;
                }
                return WaitAndReturn();
            }

            return Task.FromResult(envelope);
        }

        public Task<ApiResponse<byte[]>> GetBytesAsync(
            string url,
            IDictionary<string, string> headers,
            int timeoutSeconds,
            CancellationToken ct)
        {
            SentGets.Add(new SentRequest
            {
                Url = url,
                BodyJson = null,
                Headers = headers == null ? new Dictionary<string, string>() : new Dictionary<string, string>(headers),
                TimeoutSeconds = timeoutSeconds,
            });

            if (ct.IsCancellationRequested)
                return Task.FromCanceled<ApiResponse<byte[]>>(ct);

            CannedBytesResponse canned = null;
            foreach (var kv in _bytesByPath)
            {
                // Match against both endsWith (path suffix) and contains
                // (since signed URLs include query params after the path).
                if ((url.EndsWith(kv.Key, StringComparison.OrdinalIgnoreCase) ||
                     url.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0) &&
                    kv.Value.Count > 0)
                {
                    canned = kv.Value.Dequeue();
                    break;
                }
            }

            if (canned == null)
            {
                throw new InvalidOperationException(
                    "MockHttpTransport: no canned bytes response for " + url);
            }

            if (canned.Throw)
            {
                return Task.FromException<ApiResponse<byte[]>>(
                    new System.IO.IOException("Simulated network failure."));
            }

            ApiResponse<byte[]> envelope;
            if (canned.Ok)
            {
                envelope = new ApiResponse<byte[]> { Ok = true, Data = canned.Data ?? Array.Empty<byte>() };
            }
            else
            {
                envelope = new ApiResponse<byte[]> { Ok = false, Error = canned.Error };
            }
            return Task.FromResult(envelope);
        }
    }
}
