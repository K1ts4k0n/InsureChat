namespace InsureChatBot.Api.Models.Admin
{
    /// <summary>
    /// settings ที่ admin แก้ได้ที่ runtime — persist ใน app-settings.json
    /// ("JSON now, SQL later" ADR 0002; แก้ผ่าน /admin ADR 0010/0011).
    /// ค่าที่เป็น null = "ยังไม่ override" → ใช้ค่า base จาก config/env แทน.
    /// </summary>
    public class AppSettings
    {
        public ThemeSettings Theme { get; set; } = new();

        // ── ช่อง system prompt ที่ admin แก้ได้ (null/ว่าง = ใช้ default ในโค้ด) — ADR 0014 ─────
        // แตกจาก "Persona เดี่ยว" เดิม เป็น 4 ช่องแก้/รีเซ็ตรายช่อง. flat + nullable → backward-compatible
        // (app-settings.json เก่าที่มีแค่ Persona ยังอ่านได้ ฟิลด์ใหม่ = null = default)

        /// <summary>① บุคลิก/โทนของ "พิมพ์" (persona เดิม) — ADR 0011/0014</summary>
        public string Persona { get; set; }

        /// <summary>② วิธีการขาย & การสโคป (consultative/SPIN — เจตนาการขาย ไม่ใช่กลไก tool) — ADR 0014</summary>
        public string SalesApproach { get; set; }

        /// <summary>③ กฎเหล็ก compliance (ห้ามแต่งเบี้ย/verify/คปภ. — ย้ายจาก locked มาแก้ได้) — ADR 0014</summary>
        public string Compliance { get; set; }

        /// <summary>④ สไตล์การตอบ/ตาราง — ADR 0014</summary>
        public string Style { get; set; }

        public LlmOverrides Llm { get; set; } = new();

        public OrderEmailOverrides Email { get; set; } = new();
    }

    /// <summary>
    /// override ของ OrderEmailSettings ที่ admin ตั้งผ่าน UI (null = ใช้ค่า base จาก config/env) — ADR 0013.
    /// Password เป็นความลับ — เก็บตรงนี้แต่ "ไม่เคย" ส่งกลับ client (write-only เหมือน API key).
    /// host/port ไม่เปิดให้แก้ (Gmail-only); From ผูก = Username เสมอ (Gmail บังคับ).
    /// </summary>
    public class OrderEmailOverrides
    {
        /// <summary>อีเมลผู้ส่ง (Gmail) — From ผูกตามนี้</summary>
        public string Username { get; set; }
        /// <summary>app password (secret, write-only)</summary>
        public string Password { get; set; }
        /// <summary>อีเมลปลายทาง (โบรคเกอร์)</summary>
        public string To { get; set; }
    }

    /// <summary>สีแบรนด์ระดับแอป (global) — ADR 0010</summary>
    public class ThemeSettings
    {
        public string Brand { get; set; } = "#2563eb";
    }

    /// <summary>
    /// override ของ LlmSettings ที่ admin ตั้งผ่าน UI (null = ใช้ค่า base จาก config/env).
    /// ApiKey เป็นความลับ — เก็บตรงนี้แต่ "ไม่เคย" ส่งกลับ client (write-only, ADR 0011).
    /// </summary>
    public class LlmOverrides
    {
        public bool? Enabled { get; set; }
        public string BaseUrl { get; set; }
        public string Model { get; set; }
        public string VisionModel { get; set; }
        public string ApiKey { get; set; }
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public int? TimeoutSeconds { get; set; }
    }
}
