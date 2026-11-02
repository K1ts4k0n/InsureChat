namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>ตาราง Conversations — 1 session = 1 แถว (ADR 0016 Phase 2). candidate set ไม่เก็บ.</summary>
    public class ConversationRow
    {
        public long Id { get; set; }
        public string SessionKey { get; set; }      // = ChatbotConversationContext.SessionId (unique)
        public string ProductType { get; set; }
        public string CarBrand { get; set; }
        public string CarModel { get; set; }
        public int? CarYear { get; set; }
        public string InsuranceClass { get; set; }
        public decimal? SumInsure { get; set; }
        public string CurrentStep { get; set; }
        public string SpinReason { get; set; }
        public string ProspectName { get; set; }
        public string ProspectPhone { get; set; }
        public string ProspectEmail { get; set; }
        public string ProspectIdCard { get; set; }
        public string ProspectLicensePlate { get; set; }
        public string OutcomeStatus { get; set; } = "open"; // open | ordered
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }

        public ICollection<ConversationTurnRow> Turns { get; set; } = new List<ConversationTurnRow>();
        public ICollection<ConversationNeedRow> Needs { get; set; } = new List<ConversationNeedRow>();
    }
}
