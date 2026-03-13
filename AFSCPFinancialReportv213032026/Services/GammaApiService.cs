using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PX.Data;

namespace FinancialReport.Services
{
    /// <summary>
    /// Optional settings that override the defaults used when creating a Gamma generation.
    /// Pass to <see cref="GammaApiService.GeneratePresentation"/> to customise output.
    /// </summary>
    public class GammaGenerationOptions
    {
        public int    NumCards    { get; set; } = 12;
        public string Tone       { get; set; } = "Professional, executive-level";
        public string Audience   { get; set; } = "Senior management and board members";
        public string ImageSource { get; set; } = "pictographic";
    }

    /// <summary>
    /// Calls the Gamma API (public-api.gamma.app) to generate a PPTX presentation from a text prompt.
    /// Standard flow: POST /generations → poll GET /generations/{id} until completed → download PPTX from exportUrl.
    /// Template flow:  POST /generations/from-template → same polling → download PPTX from exportUrl.
    /// </summary>
    public class GammaApiService
    {
        private const string BaseUrl        = "https://public-api.gamma.app/v1.0";
        private const int    PollIntervalMs = 5000;  // 5 seconds between polls
        private const int    MaxPolls       = 60;    // max 5 minutes

        // Shared static HttpClient — prevents socket exhaustion from per-instance creation.
        // API key is NOT set on DefaultRequestHeaders (shared state); it is added per-request instead.
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        private readonly string _apiKey;

        public GammaApiService(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException(nameof(apiKey));

            _apiKey = apiKey;
        }

        /// <summary>
        /// Generates a PPTX using the standard /generations endpoint (no template).
        /// Pass <paramref name="options"/> to override defaults (numCards, tone, audience, imageSource).
        /// </summary>
        public byte[] GeneratePresentation(string financialMarkdown, string presentationTitle,
            CancellationToken cancellationToken = default,
            GammaGenerationOptions options = null)
        {
            string generationId = SubmitGeneration(financialMarkdown, presentationTitle, options);
            PXTrace.WriteInformation($"[Gamma] Generation submitted. ID: {generationId}");

            string exportUrl = PollUntilCompleted(generationId, cancellationToken);
            PXTrace.WriteInformation($"[Gamma] Generation completed. Downloading PPTX from: {exportUrl}");

            byte[] pptBytes = DownloadFile(exportUrl);
            PXTrace.WriteInformation($"[Gamma] Downloaded {pptBytes.Length} bytes.");

            return pptBytes;
        }

        /// <summary>
        /// Generates a PPTX using /generations/from-template.
        /// gammaTemplateId is the gammaId from the template URL: gamma.app/docs/{gammaId}
        /// </summary>
        public byte[] GeneratePresentationFromTemplate(string financialMarkdown, string gammaTemplateId,
            CancellationToken cancellationToken = default)
        {
            string generationId = SubmitGenerationFromTemplate(financialMarkdown, gammaTemplateId);
            PXTrace.WriteInformation($"[Gamma] Template generation submitted. ID: {generationId}");

            string exportUrl = PollUntilCompleted(generationId, cancellationToken);
            PXTrace.WriteInformation($"[Gamma] Template generation completed. Downloading PPTX from: {exportUrl}");

            byte[] pptBytes = DownloadFile(exportUrl);
            PXTrace.WriteInformation($"[Gamma] Downloaded {pptBytes.Length} bytes.");

            return pptBytes;
        }

        // ── Per-request builder ───────────────────────────────────────────────────
        // Sets X-API-KEY on each individual HttpRequestMessage so the shared static
        // HttpClient is never mutated.

        private HttpRequestMessage CreateRequest(HttpMethod method, string url, string jsonBody = null)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("X-API-KEY", _apiKey);
            if (jsonBody != null)
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            return request;
        }

        private string SubmitGenerationFromTemplate(string prompt, string gammaTemplateId)
        {
            var payload = new
            {
                gammaId  = gammaTemplateId,
                prompt   = prompt,
                exportAs = "pptx"
            };

            string json  = JsonConvert.SerializeObject(payload);
            var response = Task.Run(() => _httpClient.SendAsync(CreateRequest(HttpMethod.Post, $"{BaseUrl}/generations/from-template", json))).GetAwaiter().GetResult();
            string body  = Task.Run(() => response.Content.ReadAsStringAsync()).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
                throw new PXException($"[Presentation] Template generation request failed ({(int)response.StatusCode}): {body}");

            var result   = JObject.Parse(body);
            string genId = result["generationId"]?.ToString();

            if (string.IsNullOrEmpty(genId))
                throw new PXException($"[Presentation] No generationId returned from template endpoint. Response: {body}");

            return genId;
        }

        private string SubmitGeneration(string inputText, string title, GammaGenerationOptions options = null)
        {
            var opt = options ?? new GammaGenerationOptions();
            var payload = new
            {
                inputText              = inputText,
                textMode               = "generate",
                format                 = "presentation",
                exportAs               = "pptx",
                numCards               = opt.NumCards,
                additionalInstructions = $"Professional CFO-level financial presentation titled '{title}' for senior management and board members. Highlight key trends, risks, and strategic recommendations.",
                textOptions            = new
                {
                    amount   = "detailed",
                    tone     = opt.Tone,
                    audience = opt.Audience
                },
                imageOptions           = new
                {
                    source = opt.ImageSource
                }
            };

            string json  = JsonConvert.SerializeObject(payload);
            var response = Task.Run(() => _httpClient.SendAsync(CreateRequest(HttpMethod.Post, $"{BaseUrl}/generations", json))).GetAwaiter().GetResult();
            string body  = Task.Run(() => response.Content.ReadAsStringAsync()).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
                throw new PXException($"[Presentation] Generation request failed ({(int)response.StatusCode}): {body}");

            var result   = JObject.Parse(body);
            string genId = result["generationId"]?.ToString();

            if (string.IsNullOrEmpty(genId))
                throw new PXException($"[Presentation] No generationId returned. Response: {body}");

            return genId;
        }

        private string PollUntilCompleted(string generationId, CancellationToken cancellationToken = default)
        {
            // Run async polling on a thread-pool thread to avoid blocking with Thread.Sleep
            return Task.Run(async () =>
            {
                for (int i = 0; i < MaxPolls; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(PollIntervalMs, cancellationToken);

                    var response = await _httpClient.SendAsync(
                        CreateRequest(HttpMethod.Get, $"{BaseUrl}/generations/{generationId}"),
                        cancellationToken);
                    string body  = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        int statusCode = (int)response.StatusCode;
                        // Transient gateway errors (502/503/504) — skip this poll and retry
                        if (statusCode >= 500)
                        {
                            PXTrace.WriteWarning($"[Gamma] Poll {i + 1}/{MaxPolls} — transient {statusCode}, retrying...");
                            continue;
                        }
                        // 4xx = real error (bad key, not found, etc.) — fail immediately
                        throw new PXException($"[Presentation] Poll failed ({statusCode}): {body}");
                    }

                    var result    = JObject.Parse(body);
                    string status = result["status"]?.ToString();

                    PXTrace.WriteInformation($"[Gamma] Poll {i + 1}/{MaxPolls} — status: {status}");

                    if (status == "completed")
                    {
                        // exportUrl contains the PPTX file URL when exportAs:"pptx" was requested.
                        // gammaUrl is the viewer URL (gamma.app/docs/...) — not downloadable directly.
                        string exportUrl = result["exportUrl"]?.ToString();
                        if (string.IsNullOrEmpty(exportUrl))
                            throw new PXException($"[Presentation] Generation completed but no exportUrl found. Response: {body}");
                        return exportUrl;
                    }

                    if (status == "failed")
                    {
                        string errorMsg = result["error"]?["message"]?.ToString() ?? "Unknown error";
                        throw new PXException($"[Presentation] Generation failed: {errorMsg}");
                    }
                }

                throw new PXException($"[Presentation] Timed out after {MaxPolls * PollIntervalMs / 1000} seconds waiting for generation to complete.");
            }).GetAwaiter().GetResult();
        }

        private byte[] DownloadFile(string url)
        {
            // CDN download URL is pre-signed — no API key header needed
            var response = Task.Run(() => _httpClient.GetAsync(url)).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
                throw new PXException($"[Presentation] File download failed ({(int)response.StatusCode}). URL: {url}");

            return Task.Run(() => response.Content.ReadAsByteArrayAsync()).GetAwaiter().GetResult();
        }
    }
}
