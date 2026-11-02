using InsureChatBot.Api.Config;
using InsureChatBot.Api.Models.Admin;

namespace InsureChatBot.Api.Services.Settings
{
    /// <summary>
    /// ที่เก็บ settings ที่ admin แก้ได้ที่ runtime (theme/persona/LLM) — persist ลง app-settings.json.
    /// อ่านได้ทุก request, เขียนเมื่อ admin กดเซฟ → มีผลทันทีรอบถัดไป (ADR 0011).
    /// </summary>
    public interface IAppSettingsStore
    {
        /// <summary>snapshot ปัจจุบัน (อย่าแก้ object ที่คืนตรง ๆ — ใช้ Update)</summary>
        AppSettings Get();

        /// <summary>แก้แล้ว persist ลงไฟล์ (thread-safe)</summary>
        void Update(Action<AppSettings> mutate);

        /// <summary>
        /// LLM settings ที่ใช้งานจริง = base (config/env) merge กับ override ของ admin.
        /// ApiKey: admin-set ก่อน, ไม่มี → fallback env (base).
        /// </summary>
        LlmSettings GetEffectiveLlm();

        /// <summary>แหล่งที่มาของ key ปัจจุบัน: "admin" | "env" | "none"</summary>
        string GetKeySource();

        /// <summary>
        /// การส่งอีเมลคำสั่งซื้อที่ใช้งานจริง = base (config/env) merge override ของ admin (ADR 0013).
        /// From ผูก = Username เสมอ (Gmail); Password: admin-set ก่อน, ไม่มี → fallback env.
        /// </summary>
        OrderEmailSettings GetEffectiveEmail();

        /// <summary>แหล่งที่มาของ app password ปัจจุบัน: "admin" | "env" | "none"</summary>
        string GetEmailKeySource();
    }
}
