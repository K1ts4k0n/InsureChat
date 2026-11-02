using InsureChatBot.Api.Models.Chatbot;

namespace InsureChatBot.Api.Services.Conversation
{
    /// <summary>เก็บบทสนทนาลง DB (ADR 0016 Phase 2). candidate set/proposals ไม่ persist.</summary>
    public interface IConversationStore
    {
        /// <summary>upsert conversation ตาม SessionId — แทนที่ turns/needs ด้วยชุดล่าสุดจาก context</summary>
        void Upsert(ChatbotConversationContext context);

        /// <summary>set OutcomeStatus = "ordered" (เรียกหลังคำสั่งซื้อสำเร็จ)</summary>
        void MarkOrdered(string sessionId);

        /// <summary>หา Id ของ conversation จาก SessionId (null ถ้าไม่พบ)</summary>
        long? FindConversationId(string sessionId);
    }
}
