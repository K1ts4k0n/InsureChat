namespace InsureChatBot.Api.Services.Llm
{
    public interface IChatCompletionClient
    {
        /// <summary>เรียก chat completion (รองรับ tool-use). คืน assistant message (อาจมี tool_calls).</summary>
        Task<LlmMessage> CompleteAsync(
            List<LlmMessage> messages,
            List<ToolDef> tools,
            CancellationToken ct = default);

        /// <summary>เรียกแบบ streaming (stream:true) — คืน chunk ทีละชิ้น (content delta + tool_call delta). ดู ADR 0009.</summary>
        IAsyncEnumerable<LlmStreamChunk> CompleteStreamAsync(
            List<LlmMessage> messages,
            List<ToolDef> tools,
            CancellationToken ct = default);
    }
}
