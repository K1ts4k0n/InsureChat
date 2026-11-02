using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using InsureChatBot.Api.Models.Document;
using InsureChatBot.Api.Services.Llm;
using InsureChatBot.Api.Services.Settings;

namespace InsureChatBot.Api.Services.Document
{
    /// <summary>
    /// เรียก vision LLM (OpenAI-compatible) ด้วย image content-part + VisionModel แยก.
    /// ไม่ยุ่งกับ LlmMessage/IChatCompletionClient (chat pipeline). ephemeral — ไม่ log ภาพ. ADR 0018.
    /// </summary>
    public class DocumentExtractionService : IDocumentExtractionService
    {
        private readonly HttpClient _http;
        private readonly IAppSettingsStore _settings;

        private static readonly JsonSerializerOptions ParseOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private const string SystemPrompt =
            "คุณเป็นตัวช่วยอ่านเอกสารรถยนต์ของไทย (ตารางกรมธรรม์ประกันภัยรถยนต์ / เล่มทะเบียน / ป้ายภาษี). " +
            "ตอบกลับ 'เฉพาะ JSON' เท่านั้น (ห้ามมีข้อความอื่น) รูปแบบ: " +
            "{\"isVehicleDocument\": true/false, \"docType\": \"policy_schedule|registration|tax|other\", " +
            "\"confidence\": 0-1, \"fields\": {\"brand\": \"\", \"model\": \"\", \"year\": 0, \"plate\": \"\", " +
            "\"chassis\": \"\", \"insurer\": \"\"}, \"tableMarkdown\": \"ตาราง markdown สรุปทุกฟิลด์ที่อ่านได้\"}. " +
            "ถ้าไม่ใช่เอกสารเกี่ยวกับรถยนต์ ให้ isVehicleDocument=false. year เป็นปี ค.ศ. (แปลง พ.ศ.→ค.ศ. ให้ด้วย).";

        public DocumentExtractionService(HttpClient http, IAppSettingsStore settings)
        {
            _settings = settings;
            _http = http;
            var s = settings.GetEffectiveLlm();
            _http.Timeout = TimeSpan.FromSeconds(s.TimeoutSeconds);
        }

        public async Task<VisionExtractionResult> ExtractAsync(byte[] image, string mimeType, CancellationToken ct = default)
        {
            var s = _settings.GetEffectiveLlm();
            var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(image)}";

            var payload = new
            {
                model = s.VisionModel,
                temperature = 0.1,
                max_tokens = 1500,
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = new object[]
                    {
                        new { type = "text", text = "อ่านเอกสารนี้แล้วตอบเป็น JSON ตามรูปแบบที่กำหนด" },
                        new { type = "image_url", image_url = new { url = dataUrl } }
                    }}
                }
            };

            var baseUrl = s.BaseUrl.TrimEnd('/');
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
            {
                Content = JsonContent.Create(payload, options: OpenAiCompatibleClient.Json)
            };
            if (!string.IsNullOrWhiteSpace(s.ApiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"vision LLM {(int)resp.StatusCode}");

            var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, OpenAiCompatibleClient.Json);
            var content = parsed?.Choices is { Count: > 0 } ? parsed.Choices[0].Message?.Content : null;
            return ParseVisionJson(content);
        }

        /// <summary>ตัด code fence + คว้าเฉพาะบล็อก JSON แรก แล้ว deserialize. static → unit-testable</summary>
        public static VisionExtractionResult ParseVisionJson(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("vision ตอบว่าง");

            var json = content.Trim();
            if (json.StartsWith("```"))
            {
                int nl = json.IndexOf('\n');
                if (nl >= 0) json = json.Substring(nl + 1);
                int fence = json.LastIndexOf("```", StringComparison.Ordinal);
                if (fence >= 0) json = json.Substring(0, fence);
            }
            int a = json.IndexOf('{');
            int b = json.LastIndexOf('}');
            if (a >= 0 && b > a) json = json.Substring(a, b - a + 1);

            return JsonSerializer.Deserialize<VisionExtractionResult>(json, ParseOpts) ?? new VisionExtractionResult();
        }
    }
}
