namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>1 ความต้องการที่ค้นพบ (SPIN) — ADR 0016 Phase 2</summary>
    public class ConversationNeedRow
    {
        public long Id { get; set; }
        public long ConversationId { get; set; }
        public string Need { get; set; }
        public ConversationRow Conversation { get; set; }
    }
}
