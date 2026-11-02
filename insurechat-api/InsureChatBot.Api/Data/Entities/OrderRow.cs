namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>คำสั่งซื้อที่บันทึก (snapshot ลูกค้า/รถ/แผน/ราคา) เพิ่มจากการยิงอีเมล — ADR 0016 Phase 2</summary>
    public class OrderRow
    {
        public long Id { get; set; }
        public string OrderNo { get; set; }          // ORD-yyyy-000123 (unique)
        public long? ConversationId { get; set; }    // อ้างอิง session (ถ้ามี)

        public string CustName { get; set; }
        public string CustPhone { get; set; }
        public string CustEmail { get; set; }
        public string CustIdCard { get; set; }
        public string LicensePlate { get; set; }

        public string Brand { get; set; }
        public string Model { get; set; }
        public int Year { get; set; }
        public string InsuranceClass { get; set; }
        public int PackageId { get; set; }
        public string InsurerName { get; set; }
        public decimal SumInsure { get; set; }
        public decimal Premium { get; set; }
        public decimal Total { get; set; }

        public string Status { get; set; } = "ordered";
        public string EmailStatus { get; set; }            // sent | failed
        public DateTime? EmailSentAt { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
