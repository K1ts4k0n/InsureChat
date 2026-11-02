namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>1 turn ของบทสนทนา (transcript) — ADR 0016 Phase 2</summary>
    public class ConversationTurnRow
    {
        public long Id { get; set; }
        public long ConversationId { get; set; }
        public int Seq { get; set; }
        public string Role { get; set; }   // user | assistant
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
        public ConversationRow Conversation { get; set; }
    }
}
