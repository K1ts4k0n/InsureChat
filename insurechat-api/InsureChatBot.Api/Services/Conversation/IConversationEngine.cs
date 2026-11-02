using InsureChatBot.Api.Models.Chatbot;

namespace InsureChatBot.Api.Services.Conversation
{
    /// <summary>
    /// เครื่องยนต์บทสนทนา 1 เทิร์น. มี 2 ตัว: LlmConversationEngine (หลัก) / StateMachineConversationEngine (fallback). ดู ADR 0003.
    /// </summary>
    public interface IConversationEngine
    {
        Task<ChatbotMessageResponse> ProcessMessageAsync(
            string userCode,
            string userName,
            string userEmail,
            string role,
            string message,
            ChatbotConversationContext context);
    }
}
