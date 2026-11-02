namespace InsureChatBot.Api.Models.Admin
{
    // ===== Theme =====

    /// <summary>ธีมสีแบรนด์ที่ส่งให้ client (GET /Settings/Theme = public, ADR 0010)</summary>
    public class ThemeDto
    {
        public string Brand { get; set; }
    }

    // ===== System prompt (4 ช่องแก้ได้รายช่อง + locked preview, ADR 0014) =====

    /// <summary>1 ช่องของ system prompt ที่ admin แก้ได้</summary>
    public class PromptSectionDto
    {
        /// <summary>คีย์อ้างอิงช่อง: persona | salesApproach | compliance | style</summary>
        public string Key { get; set; }
        /// <summary>ชื่อช่องอ่านง่าย (ไทย)</summary>
        public string Title { get; set; }
        /// <summary>คำอธิบายสั้นว่าช่องนี้คุมอะไร</summary>
        public string Hint { get; set; }
        /// <summary>ค่าที่ใช้งานจริงตอนนี้ (admin-set หรือ default)</summary>
        public string Value { get; set; }
        /// <summary>ค่า default ในโค้ด — ให้ frontend โชว์/รีเซ็ตได้</summary>
        public string Default { get; set; }
        /// <summary>true ถ้า admin เคยตั้งช่องนี้เอง (ไม่ใช่ default)</summary>
        public bool IsCustom { get; set; }
        /// <summary>true = ช่องนี้เสี่ยง compliance → frontend เตือน+ยืนยันก่อนเซฟ (ADR 0014)</summary>
        public bool Sensitive { get; set; }
    }

    /// <summary>system prompt ทั้งหมดที่ส่งให้ admin โชว์ — 4 ช่องแก้ได้ + ส่วนล็อก read-only</summary>
    public class PromptSectionsDto
    {
        public List<PromptSectionDto> Sections { get; set; } = new();
        /// <summary>ส่วนล็อกในโค้ด (knowledge วิกิ + กลไก sentinel/tool) — โชว์อย่างเดียว แก้ไม่ได้</summary>
        public string LockedPreview { get; set; }
    }

    /// <summary>admin อัปเดต 1 ช่อง — Value null/ว่าง = รีเซ็ตช่องนั้นกลับ default</summary>
    public class PromptSectionUpdateDto
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    // ===== LLM connection (ADR 0011) =====

    /// <summary>
    /// LLM settings ที่ส่งให้ admin โชว์ในจอ — key เป็น masked เสมอ (write-only).
    /// </summary>
    public class LlmConfigDto
    {
        public bool Enabled { get; set; }
        public string BaseUrl { get; set; }
        public string Model { get; set; }
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
        public int TimeoutSeconds { get; set; }

        /// <summary>เช่น "••••a1b2" — ไม่เคยส่ง key เต็ม</summary>
        public string ApiKeyMasked { get; set; }
        /// <summary>"admin" (ตั้งผ่าน UI) | "env" (จาก OPENROUTER_API_KEY) | "none"</summary>
        public string KeySource { get; set; }
    }

    /// <summary>
    /// admin อัปเดต LLM connection.
    /// ApiKey: null/ไม่ส่ง = ไม่เปลี่ยน; มีค่า = ตั้ง key ใหม่.
    /// ClearApiKey = true → ล้าง key ที่ admin ตั้ง (กลับไป fallback env).
    /// </summary>
    public class LlmUpdateDto
    {
        public bool? Enabled { get; set; }
        public string BaseUrl { get; set; }
        public string Model { get; set; }
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public int? TimeoutSeconds { get; set; }
        public string ApiKey { get; set; }
        public bool ClearApiKey { get; set; }
    }

    // ===== Order email (การส่งคำสั่งซื้อ, ADR 0013) — password write-only + masked =====

    /// <summary>การตั้งค่าอีเมลที่ส่งให้ admin โชว์ — password เป็น masked เสมอ (write-only).</summary>
    public class EmailConfigDto
    {
        /// <summary>อีเมลผู้ส่ง (Gmail) — From ผูกตามนี้</summary>
        public string SenderEmail { get; set; }
        /// <summary>อีเมลปลายทาง (โบรคเกอร์)</summary>
        public string Recipient { get; set; }

        /// <summary>เช่น "••••wxyz" — ไม่เคยส่ง app password เต็ม</summary>
        public string PasswordMasked { get; set; }
        /// <summary>"admin" (ตั้งผ่าน UI) | "env" (จาก SMTP_PASSWORD) | "none"</summary>
        public string PasswordSource { get; set; }
    }

    /// <summary>
    /// admin อัปเดตการส่งอีเมล.
    /// Password: null/ไม่ส่ง = ไม่เปลี่ยน; มีค่า = ตั้ง app password ใหม่.
    /// ClearPassword = true → ล้าง password ที่ admin ตั้ง (กลับไป fallback env).
    /// </summary>
    public class EmailUpdateDto
    {
        public string SenderEmail { get; set; }
        public string Recipient { get; set; }
        public string Password { get; set; }
        public bool ClearPassword { get; set; }
    }
}
