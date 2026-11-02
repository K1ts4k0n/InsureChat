using System.Text.Json.Serialization;

namespace InsureChatBot.Api.Models.Line
{
    public class LineWebhookPayload
    {
        [JsonPropertyName("destination")]
        public string? Destination { get; set; }

        [JsonPropertyName("events")]
        public List<LineWebhookEvent> Events { get; set; } = new();
    }

    public class LineWebhookEvent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("source")]
        public LineSource? Source { get; set; }

        [JsonPropertyName("replyToken")]
        public string? ReplyToken { get; set; }

        [JsonPropertyName("message")]
        public LineMessageContent? Message { get; set; }
    }

    public class LineSource
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("userId")]
        public string? UserId { get; set; }
    }

    public class LineMessageContent
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}

