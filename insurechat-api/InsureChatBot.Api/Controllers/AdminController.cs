using System.Text.RegularExpressions;
using InsureChatBot.Api.Models.Admin;
using InsureChatBot.Api.Models.Auth;
using InsureChatBot.Api.Services.Conversation;
using InsureChatBot.Api.Services.Email;
using InsureChatBot.Api.Services.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsureChatBot.Api.Controllers
{
    /// <summary>
    /// หลังบ้าน /admin — จัดการ persona / LLM connection / theme (ADR 0010/0011).
    /// ทุก endpoint ต้องเป็น admin (JWT role=admin). chat/quote เป็น public — endpoint จัดการต้องล็อกแน่น.
    /// </summary>
    [Authorize(Roles = UserRoles.Admin)]
    [Route("api/Admin")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IAppSettingsStore _store;
        private readonly SystemPromptProvider _prompt;
        private readonly IOrderEmailService _email;

        private static readonly Regex HexColor = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        public AdminController(IAppSettingsStore store, SystemPromptProvider prompt, IOrderEmailService email)
        {
            _store = store;
            _prompt = prompt;
            _email = email;
        }

        // ===== Theme (สีแบรนด์ global, ADR 0010) =====

        [HttpGet("Theme")]
        public ActionResult<ThemeDto> GetTheme()
            => Ok(new ThemeDto { Brand = _store.Get().Theme?.Brand ?? "#2563eb" });

        [HttpPut("Theme")]
        public ActionResult<ThemeDto> UpdateTheme([FromBody] ThemeDto dto)
        {
            var brand = dto?.Brand?.Trim();
            if (string.IsNullOrEmpty(brand) || !HexColor.IsMatch(brand))
                return BadRequest(new { message = "สีต้องเป็นรูปแบบ #rrggbb" });

            _store.Update(s => (s.Theme ??= new ThemeSettings()).Brand = brand);
            return Ok(new ThemeDto { Brand = brand });
        }

        // ===== System prompt (4 ช่องแก้ได้รายช่อง + locked preview, ADR 0014) =====

        [HttpGet("Prompt")]
        public ActionResult<PromptSectionsDto> GetPrompt() => Ok(BuildPromptDto());

        /// <summary>อัปเดต 1 ช่อง — Value null/ว่าง = รีเซ็ตช่องนั้นกลับ default</summary>
        [HttpPut("Prompt")]
        public ActionResult<PromptSectionsDto> UpdatePrompt([FromBody] PromptSectionUpdateDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Key) || !PromptSectionKeys.Contains(dto.Key))
                return BadRequest(new { message = "key ของช่องไม่ถูกต้อง" });

            var value = string.IsNullOrWhiteSpace(dto.Value) ? null : dto.Value.Trim();
            _store.Update(s =>
            {
                switch (dto.Key)
                {
                    case "persona": s.Persona = value; break;
                    case "salesApproach": s.SalesApproach = value; break;
                    case "compliance": s.Compliance = value; break;
                    case "style": s.Style = value; break;
                }
            });
            return Ok(BuildPromptDto());
        }

        private static readonly HashSet<string> PromptSectionKeys =
            new() { "persona", "salesApproach", "compliance", "style" };

        private PromptSectionsDto BuildPromptDto()
        {
            var s = _store.Get();
            return new PromptSectionsDto
            {
                LockedPreview = _prompt.LockedPreview(),
                Sections = new List<PromptSectionDto>
                {
                    Section("persona", "① บุคลิก / โทน", "ตัวตน น้ำเสียง สไตล์การพูดของ \"พิมพ์\"",
                        _prompt.EffectivePersona(), SystemPromptProvider.DefaultPersona, s.Persona, sensitive: false),
                    Section("salesApproach", "② วิธีการขาย & การสโคป", "ลำดับการถาม/สโคปความต้องการ และจังหวะเสนอราคา (consultative/SPIN)",
                        _prompt.EffectiveSalesApproach(), SystemPromptProvider.DefaultSalesApproach, s.SalesApproach, sensitive: false),
                    Section("compliance", "③ กฎเหล็ก (Compliance)", "ห้ามแต่งเบี้ย / ยืนยันกับตัวแทน / จรรยาบรรณ คปภ. — แก้แล้วกระทบการกำกับ",
                        _prompt.EffectiveCompliance(), SystemPromptProvider.DefaultCompliance, s.Compliance, sensitive: true),
                    Section("style", "④ สไตล์การตอบ / ตาราง", "ความกระชับ รูปแบบตาราง การใช้อิโมจิ",
                        _prompt.EffectiveStyle(), SystemPromptProvider.DefaultStyle, s.Style, sensitive: false),
                }
            };
        }

        private static PromptSectionDto Section(
            string key, string title, string hint, string value, string def, string stored, bool sensitive)
            => new()
            {
                Key = key,
                Title = title,
                Hint = hint,
                Value = value,
                Default = def,
                IsCustom = !string.IsNullOrWhiteSpace(stored),
                Sensitive = sensitive
            };

        // ===== LLM connection (ADR 0011) — key เป็น write-only + masked =====

        [HttpGet("Llm")]
        public ActionResult<LlmConfigDto> GetLlm() => Ok(BuildLlmDto());

        [HttpPut("Llm")]
        public ActionResult<LlmConfigDto> UpdateLlm([FromBody] LlmUpdateDto dto)
        {
            if (dto == null) return BadRequest(new { message = "ข้อมูลไม่ถูกต้อง" });
            if (dto.Temperature is < 0 or > 2)
                return BadRequest(new { message = "temperature ต้องอยู่ระหว่าง 0–2" });
            if (dto.MaxTokens is < 1)
                return BadRequest(new { message = "max tokens ต้องมากกว่า 0" });

            _store.Update(s =>
            {
                s.Llm ??= new LlmOverrides();
                if (dto.Enabled.HasValue) s.Llm.Enabled = dto.Enabled;
                if (dto.BaseUrl != null) s.Llm.BaseUrl = Blank(dto.BaseUrl);
                if (dto.Model != null) s.Llm.Model = Blank(dto.Model);
                if (dto.Temperature.HasValue) s.Llm.Temperature = dto.Temperature;
                if (dto.MaxTokens.HasValue) s.Llm.MaxTokens = dto.MaxTokens;
                if (dto.TimeoutSeconds.HasValue) s.Llm.TimeoutSeconds = dto.TimeoutSeconds;

                // key: ClearApiKey ก่อน → ล้างกลับ fallback env; ไม่งั้นตั้งใหม่เฉพาะเมื่อส่งค่ามา
                if (dto.ClearApiKey) s.Llm.ApiKey = null;
                else if (!string.IsNullOrWhiteSpace(dto.ApiKey)) s.Llm.ApiKey = dto.ApiKey.Trim();
            });
            return Ok(BuildLlmDto());
        }

        private LlmConfigDto BuildLlmDto()
        {
            var eff = _store.GetEffectiveLlm();
            var src = _store.GetKeySource();
            return new LlmConfigDto
            {
                Enabled = eff.Enabled,
                BaseUrl = eff.BaseUrl,
                Model = eff.Model,
                Temperature = eff.Temperature,
                MaxTokens = eff.MaxTokens,
                TimeoutSeconds = eff.TimeoutSeconds,
                ApiKeyMasked = Mask(eff.ApiKey),
                KeySource = src
            };
        }

        // ===== Order email (การส่งคำสั่งซื้อ, ADR 0013) — password write-only + masked =====

        [HttpGet("Email")]
        public ActionResult<EmailConfigDto> GetEmail() => Ok(BuildEmailDto());

        [HttpPut("Email")]
        public ActionResult<EmailConfigDto> UpdateEmail([FromBody] EmailUpdateDto dto)
        {
            if (dto == null) return BadRequest(new { message = "ข้อมูลไม่ถูกต้อง" });

            var sender = Blank(dto.SenderEmail);
            var recipient = Blank(dto.Recipient);
            if (sender != null && !IsEmail(sender))
                return BadRequest(new { message = "อีเมลผู้ส่งไม่ถูกต้อง" });
            if (recipient != null && !IsEmail(recipient))
                return BadRequest(new { message = "อีเมลปลายทางไม่ถูกต้อง" });

            _store.Update(s =>
            {
                s.Email ??= new OrderEmailOverrides();
                // ส่งฟิลด์มา (ไม่ใช่ null) = อัปเดต; ค่าว่าง → กลับไป fallback env
                if (dto.SenderEmail != null) s.Email.Username = sender;
                if (dto.Recipient != null) s.Email.To = recipient;

                // password: ClearPassword ก่อน → ล้างกลับ fallback env; ไม่งั้นตั้งใหม่เฉพาะเมื่อส่งค่ามา
                if (dto.ClearPassword) s.Email.Password = null;
                else if (!string.IsNullOrWhiteSpace(dto.Password)) s.Email.Password = dto.Password.Trim();
            });
            return Ok(BuildEmailDto());
        }

        [HttpPost("Email/Test")]
        public async Task<IActionResult> TestEmail(CancellationToken ct)
        {
            var r = await _email.SendTestAsync(ct);
            return r.Success
                ? Ok(new { message = "ส่งอีเมลทดสอบสำเร็จ — ตรวจกล่องขาเข้าของปลายทาง" })
                : BadRequest(new { message = "ส่งไม่สำเร็จ: " + r.Error });
        }

        private EmailConfigDto BuildEmailDto()
        {
            var eff = _store.GetEffectiveEmail();
            return new EmailConfigDto
            {
                SenderEmail = eff.Username,
                Recipient = eff.To,
                PasswordMasked = Mask(eff.Password),
                PasswordSource = _store.GetEmailKeySource()
            };
        }

        private static bool IsEmail(string s) => System.Net.Mail.MailAddress.TryCreate(s, out _);

        private static string Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>คืน key แบบปิดบัง — โชว์แค่ 4 ตัวท้าย (ADR 0011: ไม่เคยส่ง key เต็มกลับ client)</summary>
        private static string Mask(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            return key.Length <= 4 ? "••••" : "••••" + key[^4..];
        }
    }
}
