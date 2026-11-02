namespace InsureChatBot.Api.Models.Admin
{
    public class ConversationSummaryDto
    {
        public long Id { get; set; }
        public string SessionKey { get; set; }
        public string CarBrand { get; set; }
        public string CarModel { get; set; }
        public int? CarYear { get; set; }
        public string ProspectName { get; set; }
        public string ProspectPhone { get; set; }
        public string OutcomeStatus { get; set; }
        public int TurnCount { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    public class ConversationTurnDto { public int Seq { get; set; } public string Role { get; set; } public string Text { get; set; } public DateTime Timestamp { get; set; } }

    public class ConversationDetailDto : ConversationSummaryDto
    {
        public string ProductType { get; set; }
        public string InsuranceClass { get; set; }
        public decimal? SumInsure { get; set; }
        public string ProspectEmail { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Needs { get; set; } = new();
        public List<ConversationTurnDto> Turns { get; set; } = new();
    }

    public class OrderListDto
    {
        public long Id { get; set; }
        public string OrderNo { get; set; }
        public string CustName { get; set; }
        public string CustPhone { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string InsurerName { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; }
        public string EmailStatus { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
