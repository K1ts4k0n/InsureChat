namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>
    /// ตาราง AppSettings — 1 แถว (Id=1). ย้ายจาก app-settings.json (ADR 0016).
    /// **ไม่มีคอลัมน์ secret** (LLM ApiKey / Email Password) — secret อยู่ env เท่านั้น.
    /// </summary>
    public class AppSettingsRow
    {
        public int Id { get; set; }                 // บังคับ = 1
        public string ThemeBrand { get; set; } = "#2563eb";

        // 4 ช่อง prompt (ADR 0014) — null = ใช้ default ในโค้ด
        public string Persona { get; set; }
        public string SalesApproach { get; set; }
        public string Compliance { get; set; }
        public string Style { get; set; }

        // LLM overrides (ไม่มี ApiKey) — null = ใช้ค่า base จาก config/env
        public bool? LlmEnabled { get; set; }
        public string LlmBaseUrl { get; set; }
        public string LlmModel { get; set; }
        public double? LlmTemperature { get; set; }
        public int? LlmMaxTokens { get; set; }
        public int? LlmTimeoutSeconds { get; set; }

        // Email overrides (ไม่มี Password)
        public string EmailUsername { get; set; }
        public string EmailTo { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
