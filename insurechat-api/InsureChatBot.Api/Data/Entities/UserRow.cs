namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>ตาราง Users — ย้ายจาก users.json (ADR 0016). โดเมน POCO ยังเป็น Models.Auth.User; map ใน SqlUserStore.</summary>
    public class UserRow
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }   // "iterations.saltB64.hashB64" (PBKDF2-SHA256)
        public string Role { get; set; }            // admin (agent/customer = legacy)
        public string CustCode { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
