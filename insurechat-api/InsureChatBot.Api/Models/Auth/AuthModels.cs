namespace InsureChatBot.Api.Models.Auth
{
    /// <summary>
    /// role ของ User. ตั้งแต่ ADR 0011: chatbot เป็น public (ไม่ login) → เหลือ role เดียวที่ login คือ Admin.
    /// Agent/Customer คงไว้เป็น legacy (ข้อมูลเก่าใน users.json) แต่ไม่ออก token/ไม่มี flow แล้ว.
    /// </summary>
    public static class UserRoles
    {
        public const string Admin = "admin";        // ผู้ดูแลระบบ: login เข้า /admin (seed เท่านั้น) — ADR 0010/0011
        public const string Agent = "agent";        // legacy (ADR 0011 เลิกใช้)
        public const string Customer = "customer";  // legacy (ADR 0011 เลิกใช้)
    }

    /// <summary>บัญชีผู้ใช้ — เก็บใน users.json ตอนนี้ (อนาคต SQL Server, ดู ADR 0002)</summary>
    public class User
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; } // รูปแบบ: "iterations.saltB64.hashB64" (PBKDF2-SHA256)
        public string Role { get; set; }          // UserRoles.Agent | UserRoles.Customer
        public string CustCode { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    /// <summary>สมัครสมาชิกเอง — role บังคับเป็น customer เสมอ (ดู ADR 0002)</summary>
    public class RegisterRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
    }

    public class LoginResponse
    {
        public string Token { get; set; }
        public string Role { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string CustCode { get; set; }
    }
}
