using System;
using System.Threading;
using System.Threading.Tasks;
using PromptMotion.Auth;
using PromptMotion.Http;
using PromptMotion.Shared.Clips;
using UnityEditor;

namespace PromptMotion.Windows
{
    /// <summary>
    /// Drives the end-to-end "click Generate → applied clip" flow:
    /// POST /prod/generate, GET the signed FBX URL, write the bytes under
    /// <see cref="DefaultFbxSink.GeneratedFolder"/> and ImportAsset them.
    /// Returns an <see cref="Outcome"/> the window translates to status text +
    /// optional <see cref="ClipApplier"/> apply step. Splitting the file I/O
    /// into <see cref="IFbxSink"/> keeps unit tests deterministic without the
    /// AssetDatabase.
    /// </summary>
    public sealed class GenerationOrchestrator
    {
        public interface IFbxSink
        {
            /// <summary>
            /// Persist <paramref name="bytes"/> as the FBX for <paramref name="jobId"/>.
            /// Returns the asset path the file lives at (so callers can ImportAsset
            /// + locate the AnimationClip sub-asset).
            /// </summary>
            string Save(string jobId, byte[] bytes);
        }

        public sealed class DefaultFbxSink : IFbxSink
        {
            /// <summary>Generated FBXes live under Assets/ so they're usable in builds.</summary>
            public const string GeneratedFolder = "Assets/PromptMotion/Generated";

            public string Save(string jobId, byte[] bytes)
            {
                if (string.IsNullOrEmpty(jobId)) throw new ArgumentException("jobId required", nameof(jobId));
                ClipApplier.EnsureFolder(GeneratedFolder);
                var path = $"{GeneratedFolder}/{jobId}.fbx";
                var absPath = System.IO.Path.GetFullPath(path);
                System.IO.File.WriteAllBytes(absPath, bytes ?? Array.Empty<byte>());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return path;
            }
        }

        public sealed class Outcome
        {
            public bool Ok;
            public ApiError Error;
            public bool Cached;
            public int GenerationsRemaining;
            public string FbxPath;
            public string JobId;
            public int DurationSeconds;
        }

        private readonly AssetStoreApiClient _client;
        private readonly IFbxSink _sink;

        /// <summary>
        /// Called when the server returns a cached result, before downloading.
        /// Return <c>true</c> to accept the cached clip; <c>false</c> to
        /// regenerate with a fresh random seed. <c>null</c> means always accept.
        /// </summary>
        public Func<bool> OnCachedResult { get; set; }

        /// <summary>
        /// Reports real phase transitions during the pipeline: "downloading" (90%)
        /// when FBX download starts, "importing" (99%) when AssetDatabase import
        /// starts. The window uses these to override its time-based progress estimate.
        /// </summary>
        public Action<string, float> OnPhaseChanged { get; set; }

        public GenerationOrchestrator(AssetStoreApiClient client, IFbxSink sink)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public async Task<Outcome> RunAsync(
            string invoice,
            string prompt,
            int durationSeconds,
            int? seed,
            CancellationToken ct)
        {
            if (string.IsNullOrEmpty(invoice))
            {
                return Fail(new ApiError
                {
                    CodeRaw = ApiErrorCode.MachineNotBound.ToString(),
                    Message = "Trial not activated. Activate with your email to generate.",
                });
            }
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return Fail(new ApiError
                {
                    CodeRaw = ApiErrorCode.BadRequest.ToString(),
                    Message = "Prompt is empty.",
                });
            }

            ApiResponse<Models.GenerateResult> generateResp;
            try
            {
                generateResp = await _client.GenerateAsync(
                    invoice, prompt, durationSeconds, seed, ct: ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Fail(new ApiError
                {
                    CodeRaw = ApiErrorCode.NetworkError.ToString(),
                    Message = ex.Message,
                });
            }

            if (generateResp == null || !generateResp.Ok || generateResp.Data == null)
            {
                return Fail(generateResp?.Error ?? new ApiError
                {
                    CodeRaw = ApiErrorCode.Unknown.ToString(),
                    Message = "Empty response from server.",
                });
            }

            var data = generateResp.Data;
            if (string.IsNullOrEmpty(data.FbxUrl))
            {
                return Fail(new ApiError
                {
                    CodeRaw = ApiErrorCode.ServerError.ToString(),
                    Message = "Server returned no FBX URL.",
                });
            }

            if (data.Cached && OnCachedResult != null && !OnCachedResult())
            {
                var freshSeed = new System.Random().Next(0, int.MaxValue);
                try
                {
                    generateResp = await _client.GenerateAsync(
                        invoice, prompt, durationSeconds, freshSeed, ct: ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    return Fail(new ApiError
                    {
                        CodeRaw = ApiErrorCode.NetworkError.ToString(),
                        Message = ex.Message,
                    });
                }

                if (generateResp == null || !generateResp.Ok || generateResp.Data == null)
                {
                    return Fail(generateResp?.Error ?? new ApiError
                    {
                        CodeRaw = ApiErrorCode.Unknown.ToString(),
                        Message = "Empty response from server.",
                    });
                }

                data = generateResp.Data;
                if (string.IsNullOrEmpty(data.FbxUrl))
                {
                    return Fail(new ApiError
                    {
                        CodeRaw = ApiErrorCode.ServerError.ToString(),
                        Message = "Server returned no FBX URL.",
                    });
                }
            }

            OnPhaseChanged?.Invoke("downloading", 90f);

            ApiResponse<byte[]> dlResp;
            try
            {
                dlResp = await _client.DownloadFbxAsync(data.FbxUrl, invoice, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Fail(new ApiError
                {
                    CodeRaw = ApiErrorCode.NetworkError.ToString(),
                    Message = "FBX download failed: " + ex.Message,
                });
            }

            if (dlResp == null || !dlResp.Ok || dlResp.Data == null || dlResp.Data.Length == 0)
            {
                return Fail(dlResp?.Error ?? new ApiError
                {
                    CodeRaw = ApiErrorCode.NetworkError.ToString(),
                    Message = "FBX download returned no data.",
                });
            }

            OnPhaseChanged?.Invoke("importing", 99f);

            string path;
            try
            {
                path = _sink.Save(data.JobId, dlResp.Data);
            }
            catch (Exception ex)
            {
                return Fail(new ApiError
                {
                    CodeRaw = ApiErrorCode.Unknown.ToString(),
                    Message = "Saving FBX failed: " + ex.Message,
                });
            }

            return new Outcome
            {
                Ok = true,
                Cached = data.Cached,
                GenerationsRemaining = data.GenerationsRemaining,
                DurationSeconds = data.DurationSeconds,
                FbxPath = path,
                JobId = data.JobId,
            };
        }

        private static Outcome Fail(ApiError error) => new Outcome { Ok = false, Error = error };
    }
}
