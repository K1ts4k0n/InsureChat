using InsureChatBot.Api.Models.Chatbot;

namespace InsureChatBot.Api.Services.Interfaces
{
    public interface IChatbotService
    {
        Task<ChatbotMessageResponse> ProcessMessageAsync(
            string userCode,
            string userName,
            string userEmail,
            string role,
            string message,
            ChatbotConversationContext conversationContext);

        // สตรีมคำตอบ (status/delta/suggestions/done) — LLM mode สตรีมจริง, ไม่ตั้งค่า LLM → SM one-shot. ดู ADR 0009
        IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(
            string userCode,
            string userName,
            string userEmail,
            string role,
            string message,
            ChatbotConversationContext conversationContext,
            CancellationToken ct = default);

        Task<ChatbotProposalResponse> SubmitProposalAsync(
            string userCode,
            string userName,
            string userEmail,
            string role,
            ChatbotConversationContext conversationContext);

        // ปุ่มถามด่วนชุดแรกตอนเปิดแชต (greeting เป็น template instant). ดู ADR 0008
        Task<List<string>> GenerateSuggestionsAsync(ChatbotConversationContext conversationContext);

        Task<ChatbotConversationHistory> GetConversationHistoryAsync(string userCode);
    }
}
