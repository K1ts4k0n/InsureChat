namespace InsureChatBot.Api.Config
{
    public class JwtSettings
    {
        public string Key { get; set; }
        public string Issuer { get; set; } = "InsureChatBot";
        public string Audience { get; set; } = "InsureChatBot";
        public int ExpiresMinutes { get; set; } = 480;
    }

    /// <summary>ตำแหน่งของเครื่องมือ motor query (Python) — ชี้ไป InsureChat/tools/motor</summary>
    public class MotorToolSettings
    {
        public string PythonExe { get; set; } = "python";
        public string ScriptPath { get; set; }       // .../tools/motor/query.py
        public string WorkingDirectory { get; set; } // .../tools/motor
    }

    /// <summary>ปลายทาง T Broker — mock = ไม่ยิงจริง (default), live = POST จริง (ดู ADR 0001)</summary>
    public class TBrokerSettings
    {
        public string SubmitMode { get; set; } = "mock"; // "mock" | "live"
        public string ApiUrl { get; set; }
    }

    /// <summary>
    /// ส่งคำสั่งซื้อทางอีเมลถึงโบรคเกอร์ (Gmail SMTP, ดู ADR 0012).
    /// Password ว่าง → mock (log เนื้ออีเมล + คืน success); ตั้งแล้ว → ส่งจริง.
    /// Password มาจาก env SMTP_PASSWORD, To override ได้ด้วย ORDER_EMAIL_TO.
    /// </summary>
    public class OrderEmailSettings
    {
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;               // STARTTLS
        public string Username { get; set; } = "clickto.helper@gmail.com";
        public string Password { get; set; } = "";          // app password (env SMTP_PASSWORD)
        public string From { get; set; } = "clickto.helper@gmail.com";
        public string To { get; set; } = "clickto.helper@gmail.com";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Password);
    }

    public class UserStoreSettings
    {
        public string JsonPath { get; set; } // .../Data/users.json
    }

    /// <summary>เชื่อม SQL DB ของ KPN (read-only) สำหรับ facet/แพคเกจสด — connstring จาก env KPN_DB_CONNECTION (ADR 0004)</summary>
    public class KpnDbSettings
    {
        public string ConnectionString { get; set; } = "";
        // base URL โลโก้บริษัท (ตรงกับ DataService.GetUrlLogo ของ KPN)
        public string LogoBaseUrl { get; set; } = "https://kpnnewapi.clicktodesign.net/image/logo/";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
    }

    /// <summary>
    /// LLM conversation engine (OpenAI-compatible). Default = OpenRouter + gpt-oss-120b (free).
    /// ApiKey มาจาก env OPENROUTER_API_KEY (โหลดผ่าน .env). ดู ADR 0003.
    /// </summary>
    public class LlmSettings
    {
        public bool Enabled { get; set; } = true;
        public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
        public string ApiKey { get; set; } = "";       // เติมจาก env OPENROUTER_API_KEY ใน Program.cs
        public string Model { get; set; } = "openai/gpt-oss-120b:free";

        /// <summary>โมเดล vision แยกสำหรับ Document intake — โมเดลสนทนา (Model) อาจ text-only. ดู ADR 0018</summary>
        public string VisionModel { get; set; } = "google/gemini-2.5-flash";
        public int MaxTokens { get; set; } = 1024;
        public double Temperature { get; set; } = 0.7;
        public int TimeoutSeconds { get; set; } = 60;
        public int MaxToolIterations { get; set; } = 5;

        public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ApiKey);
    }
}
