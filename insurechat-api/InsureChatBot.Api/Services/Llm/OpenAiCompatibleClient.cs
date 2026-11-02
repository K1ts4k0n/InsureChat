using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Services.Settings;

namespace InsureChatBot.Api.Services.Llm
{
    /// <summary>
    /// ลูกค้า Chat Completions แบบ OpenAI-compatible — ใช้กับ OpenRouter (และสลับ provider ได้ผ่าน config/admin).
    /// อ่าน effective settings จาก store ตอนสร้าง (typed client = transient → ค่าใหม่มีผลรอบถัดไป, ADR 0011).
    /// </summary>
    public class OpenAiCompatibleClient : IChatCompletionClient
    {
        private readonly HttpClient _http;
        private readonly LlmSettings _settings;

        public static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        public OpenAiCompatibleClient(HttpClient http, IAppSettingsStore settings)
        {
            _settings = settings.GetEffectiveLlm();
            _http = http;
            _http.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
                _http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            // OpenRouter optional attribution headers
            _http.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", "https://insurechat.local");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", "InsureChat");
        }

        public async Task<LlmMessage> CompleteAsync(List<LlmMessage> messages, List<ToolDef> tools, CancellationToken ct = default)
        {
            var req = new ChatCompletionRequest
            {
                Model = _settings.Model,
                Messages = messages,
                Tools = (tools != null && tools.Count > 0) ? tools : null,
                ToolChoice = (tools != null && tools.Count > 0) ? "auto" : null,
                MaxTokens = _settings.MaxTokens,
                Temperature = _settings.Temperature
            };

            var baseUrl = _settings.BaseUrl.TrimEnd('/');
            using var resp = await _http.PostAsJsonAsync($"{baseUrl}/chat/completions", req, Json, ct);

            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"LLM {(int)resp.StatusCode}: {Truncate(body, 500)}");

            var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, Json);
            var msg = parsed?.Choices?.FirstOrDefault()?.Message
                      ?? throw new InvalidOperationException("LLM response ว่าง/ผิดรูปแบบ");
            msg.Role ??= "assistant";
            return msg;
        }

        public async IAsyncEnumerable<LlmStreamChunk> CompleteStreamAsync(
            List<LlmMessage> messages, List<ToolDef> tools, [EnumeratorCancellation] CancellationToken ct = default)
        {
            var req = new ChatCompletionRequest
            {
                Model = _settings.Model,
                Messages = messages,
                Tools = (tools != null && tools.Count > 0) ? tools : null,
                ToolChoice = (tools != null && tools.Count > 0) ? "auto" : null,
                MaxTokens = _settings.MaxTokens,
                Temperature = _settings.Temperature,
                Stream = true
            };

            var baseUrl = _settings.BaseUrl.TrimEnd('/');
            using var httpReq = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
            {
                Content = JsonContent.Create(req, options: Json)
            };
            using var resp = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"LLM {(int)resp.StatusCode}: {Truncate(err, 500)}");
            }

            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            string line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (line.Length == 0 || !line.StartsWith("data:")) continue;
                var data = line.Substring(5).Trim();
                if (data == "[DONE]") break;

                ChatCompletionChunk parsed = null;
                try { parsed = JsonSerializer.Deserialize<ChatCompletionChunk>(data, Json); }
                catch { continue; } // ข้าม keep-alive / chunk ที่ parse ไม่ได้

                var choice = parsed?.Choices?.FirstOrDefault();
                if (choice == null) continue;

                var outChunk = new LlmStreamChunk { FinishReason = choice.FinishReason };
                if (!string.IsNullOrEmpty(choice.Delta?.Content))
                    outChunk.ContentDelta = choice.Delta.Content;
                if (choice.Delta?.ToolCalls is { Count: > 0 })
                    outChunk.ToolCallDeltas = choice.Delta.ToolCalls.Select(tc => new LlmToolCallDelta
                    {
                        Index = tc.Index, Id = tc.Id,
                        Name = tc.Function?.Name, ArgumentsDelta = tc.Function?.Arguments
                    }).ToList();

                yield return outChunk;
            }
        }

        private static string Truncate(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s[..n];
    }
}
