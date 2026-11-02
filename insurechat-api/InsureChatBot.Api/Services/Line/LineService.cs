using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InsureChatBot.Api.Config;
using Microsoft.Extensions.Options;

namespace InsureChatBot.Api.Services.Line
{
    public interface ILineService
    {
        bool VerifySignature(string requestBody, string channelSecret, string signature);
        Task ReplyMessageAsync(string replyToken, List<object> messages, string? accessToken = null);
        Task PushMessageAsync(string toUserId, List<object> messages, string? accessToken = null);
    }

    public class LineService : ILineService
    {
        private readonly HttpClient _http;
        private readonly LineSettings _settings;
        private readonly ILogger<LineService> _logger;

        public LineService(HttpClient http, IOptions<LineSettings> settings, ILogger<LineService> logger)
        {
            _http = http;
            _settings = settings.Value;
            _logger = logger;
        }

        public bool VerifySignature(string requestBody, string channelSecret, string signature)
        {
            if (string.IsNullOrWhiteSpace(channelSecret) || string.IsNullOrWhiteSpace(signature))
                return true; // ถ้ายังไม่ตั้ง secret ใน dev ยอมให้ผ่านได้

            try
            {
                var secretBytes = Encoding.UTF8.GetBytes(channelSecret);
                var bodyBytes = Encoding.UTF8.GetBytes(requestBody);
                using var hmac = new HMACSHA256(secretBytes);
                var hash = hmac.ComputeHash(bodyBytes);
                var computedSignature = Convert.ToBase64String(hash);
                return computedSignature == signature;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying LINE signature");
                return false;
            }
        }

        public async Task ReplyMessageAsync(string replyToken, List<object> messages, string? accessToken = null)
        {
            var token = !string.IsNullOrWhiteSpace(accessToken) ? accessToken : _settings.ChannelAccessToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("LINE Channel Access Token is not set. Skipped replying to LINE.");
                return;
            }

            var payload = new
            {
                replyToken = replyToken,
                messages = messages
            };

            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/reply");
            req.Headers.Add("Authorization", $"Bearer {token}");
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var resp = await _http.SendAsync(req);
                var respContent = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to reply to LINE. Status: {StatusCode}, Response: {Response}", resp.StatusCode, respContent);
                }
                else
                {
                    _logger.LogInformation("Successfully replied message to LINE.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when sending reply to LINE");
            }
        }

        public async Task PushMessageAsync(string toUserId, List<object> messages, string? accessToken = null)
        {
            var token = !string.IsNullOrWhiteSpace(accessToken) ? accessToken : _settings.ChannelAccessToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogWarning("LINE Channel Access Token is not set. Skipped push to LINE.");
                return;
            }

            var payload = new
            {
                to = toUserId,
                messages = messages
            };

            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/push");
            req.Headers.Add("Authorization", $"Bearer {token}");
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var resp = await _http.SendAsync(req);
                var respContent = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to push to LINE. Status: {StatusCode}, Response: {Response}", resp.StatusCode, respContent);
                }
                else
                {
                    _logger.LogInformation("Successfully pushed message to LINE user: {UserId}", toUserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception when sending push to LINE");
            }
        }
    }
}

