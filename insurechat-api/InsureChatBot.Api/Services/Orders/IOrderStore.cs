using InsureChatBot.Api.Models.Chatbot;

namespace InsureChatBot.Api.Services.Orders
{
    /// <summary>บันทึกคำสั่งซื้อลง DB (ADR 0016 Phase 2) — snapshot ราคา ณ เวลาเสนอ (KPN เป็น live).</summary>
    public interface IOrderStore
    {
        /// <summary>insert Order จากแผนที่เลือก + ผูก conversation ถ้ามี; คืน OrderNo. set conversation = ordered.</summary>
        string CreateOrder(ChatbotConversationContext context, ChatbotProposalSummary selected, string emailStatus);
    }
}
