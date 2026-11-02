namespace InsureChatBot.Api.Services.Email
{
    /// <summary>ข้อมูลคำสั่งซื้อที่ใส่ในอีเมลถึงโบรคเกอร์ (ADR 0012)</summary>
    public class OrderEmailModel
    {
        // ลูกค้า
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerEmail { get; set; }   // optional
        public string IdCard { get; set; }           // optional
        public string LicensePlate { get; set; }     // optional

        // แพคเกจที่เลือก
        public string Brand { get; set; }
        public string Model { get; set; }
        public int Year { get; set; }
        public string InsuranceClass { get; set; }
        public decimal SumInsure { get; set; }
        public decimal PremiumNet { get; set; }
        public decimal PremiumTotal { get; set; }
        public string InsurerName { get; set; }
    }

    public record OrderEmailResult(bool Success, string Error);

    public interface IOrderEmailService
    {
        /// <summary>ส่งอีเมลคำสั่งซื้อ. mock เมื่อยังไม่ตั้ง password (คืน Success=true); ส่งจริงล้ม → Success=false.</summary>
        Task<OrderEmailResult> SendAsync(OrderEmailModel model, CancellationToken ct = default);

        /// <summary>ส่งอีเมลทดสอบไปยังปลายทางด้วยค่าปัจจุบัน — ตรวจ App Password (ADR 0013). ไม่มี mock: ยังไม่ตั้ง password → Success=false.</summary>
        Task<OrderEmailResult> SendTestAsync(CancellationToken ct = default);
    }
}
