using System.Text.Json.Serialization;

namespace InsureChatBot.Api.Services.Llm
{
    // DTO สำหรับ OpenAI-compatible Chat Completions API (OpenRouter).
    // ใช้ JsonNamingPolicy.SnakeCaseLower → MaxTokens→max_tokens, ToolCalls→tool_calls ฯลฯ

    public class ChatCompletionRequest
    {
        public string Model { get; set; }
        public List<LlmMessage> Messages { get; set; } = new();
        public List<ToolDef> Tools { get; set; }
        public string ToolChoice { get; set; }            // "auto" | null
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
        public bool? Stream { get; set; }                 // true → SSE token streaming (ADR 0009)
    }

    // ===== streaming (ADR 0009) =====
    /// <summary>1 ชิ้นจาก SSE stream: content เพิ่มทีละนิด และ/หรือ tool_call ที่ทยอยมา (args เป็นเศษ).</summary>
    public class LlmStreamChunk
    {
        public string ContentDelta { get; set; }
        public List<LlmToolCallDelta> ToolCallDeltas { get; set; }
        public string FinishReason { get; set; }
    }

    public class LlmToolCallDelta
    {
        public int Index { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string ArgumentsDelta { get; set; }
    }

    // DTO สำหรับ deserialize chunk ดิบ (delta) จาก OpenAI-compatible stream
    public class ChatCompletionChunk
    {
        public List<ChunkChoice> Choices { get; set; }

        public class ChunkChoice
        {
            public ChunkDelta Delta { get; set; }
            public string FinishReason { get; set; }
        }
        public class ChunkDelta
        {
            public string Content { get; set; }
            public List<ChunkToolCall> ToolCalls { get; set; }
        }
        public class ChunkToolCall
        {
            public int Index { get; set; }
            public string Id { get; set; }
            public ChunkFn Function { get; set; }
        }
        public class ChunkFn
        {
            public string Name { get; set; }
            public string Arguments { get; set; }
        }
    }

    public class LlmMessage
    {
        public string Role { get; set; }                  // system | user | assistant | tool
        public string Content { get; set; }
        public List<LlmToolCall> ToolCalls { get; set; }  // assistant → เรียก tool
        public string ToolCallId { get; set; }            // role=tool → ผูกกับ tool_call
    }

    public class LlmToolCall
    {
        public string Id { get; set; }
        public string Type { get; set; } = "function";
        public LlmFunctionCall Function { get; set; }
    }

    public class LlmFunctionCall
    {
        public string Name { get; set; }
        public string Arguments { get; set; }             // JSON string (อาร์กิวเมนต์)
    }

    public class ToolDef
    {
        public string Type { get; set; } = "function";
        public ToolFunction Function { get; set; }
    }

    public class ToolFunction
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public object Parameters { get; set; }            // JSON schema (Dictionary → คีย์ verbatim)
    }

    public class ChatCompletionResponse
    {
        public List<Choice> Choices { get; set; }

        public class Choice
        {
            public LlmMessage Message { get; set; }
            public string FinishReason { get; set; }
        }
    }
}
