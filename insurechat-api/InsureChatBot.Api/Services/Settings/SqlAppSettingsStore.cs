using InsureChatBot.Api.Config;
using InsureChatBot.Api.Data;
using InsureChatBot.Api.Data.Entities;
using InsureChatBot.Api.Models.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InsureChatBot.Api.Services.Settings
{
    /// <summary>
    /// IAppSettingsStore บน SQL (EF Core) — แทน AppSettingsStore (ADR 0016).
    /// Singleton + IDbContextFactory + cache ในหน่วยความจำ (โหลด 1 แถว Id=1).
    /// **secret env-only**: ไม่โหลด/ไม่เก็บ ApiKey &amp; Password ลง DB และ null ทิ้งใน cache เสมอ
    /// → GetEffective* fallback env, GetKeySource คืน "env"/"none".
    /// </summary>
    public class SqlAppSettingsStore : IAppSettingsStore
    {
        private const int RowId = 1;
        private readonly IDbContextFactory<InsureChatDbContext> _factory;
        private readonly LlmSettings _baseLlm;
        private readonly OrderEmailSettings _baseEmail;
        private readonly object _lock = new();
        private AppSettings _settings;

        public SqlAppSettingsStore(
            IDbContextFactory<InsureChatDbContext> factory,
            IOptions<LlmSettings> baseLlm,
            IOptions<OrderEmailSettings> baseEmail)
        {
            _factory = factory;
            _baseLlm = baseLlm.Value;
            _baseEmail = baseEmail.Value;
            _settings = Load();
        }

        private AppSettings Load()
        {
            using var db = _factory.CreateDbContext();
            var row = db.AppSettings.AsNoTracking().FirstOrDefault(x => x.Id == RowId);
            return row == null ? new AppSettings() : ToPoco(row);
        }

        public AppSettings Get() { lock (_lock) return _settings; }

        public void Update(Action<AppSettings> mutate)
        {
            lock (_lock)
            {
                mutate(_settings);
                StripSecrets(_settings);   // env-only — ทิ้ง secret ที่ใครพยายามตั้ง

                using var db = _factory.CreateDbContext();
                var row = db.AppSettings.FirstOrDefault(x => x.Id == RowId);
                if (row == null) { row = new AppSettingsRow { Id = RowId }; db.AppSettings.Add(row); }
                WriteRow(row, _settings);
                row.UpdatedAt = DateTime.UtcNow;
                db.SaveChanges();
            }
        }

        public LlmSettings GetEffectiveLlm()
        {
            AppSettings s; lock (_lock) s = _settings;
            var o = s.Llm ?? new LlmOverrides();
            var b = _baseLlm;
            return new LlmSettings
            {
                Enabled = o.Enabled ?? b.Enabled,
                BaseUrl = string.IsNullOrWhiteSpace(o.BaseUrl) ? b.BaseUrl : o.BaseUrl,
                Model = string.IsNullOrWhiteSpace(o.Model) ? b.Model : o.Model,
                VisionModel = string.IsNullOrWhiteSpace(o.VisionModel) ? b.VisionModel : o.VisionModel,
                ApiKey = string.IsNullOrWhiteSpace(o.ApiKey) ? b.ApiKey : o.ApiKey, // o.ApiKey = null เสมอ → env
                Temperature = o.Temperature ?? b.Temperature,
                MaxTokens = o.MaxTokens ?? b.MaxTokens,
                TimeoutSeconds = o.TimeoutSeconds ?? b.TimeoutSeconds,
                MaxToolIterations = b.MaxToolIterations
            };
        }

        public string GetKeySource()
        {
            AppSettings s; lock (_lock) s = _settings;
            if (!string.IsNullOrWhiteSpace(s.Llm?.ApiKey)) return "admin"; // ไม่เกิดขึ้น (strip) — กันไว้
            if (!string.IsNullOrWhiteSpace(_baseLlm.ApiKey)) return "env";
            return "none";
        }

        public OrderEmailSettings GetEffectiveEmail()
        {
            AppSettings s; lock (_lock) s = _settings;
            var o = s.Email ?? new OrderEmailOverrides();
            var b = _baseEmail;
            var username = string.IsNullOrWhiteSpace(o.Username) ? b.Username : o.Username.Trim();
            return new OrderEmailSettings
            {
                Host = b.Host,
                Port = b.Port,
                Username = username,
                From = username,
                Password = string.IsNullOrWhiteSpace(o.Password) ? b.Password : o.Password, // o.Password = null → env
                To = string.IsNullOrWhiteSpace(o.To) ? b.To : o.To.Trim()
            };
        }

        public string GetEmailKeySource()
        {
            AppSettings s; lock (_lock) s = _settings;
            if (!string.IsNullOrWhiteSpace(s.Email?.Password)) return "admin"; // ไม่เกิดขึ้น (strip)
            if (!string.IsNullOrWhiteSpace(_baseEmail.Password)) return "env";
            return "none";
        }

        // ===== mapping =====
        private static void StripSecrets(AppSettings s)
        {
            if (s.Llm != null) s.Llm.ApiKey = null;
            if (s.Email != null) s.Email.Password = null;
        }

        private static AppSettings ToPoco(AppSettingsRow r)
        {
            var s = new AppSettings
            {
                Theme = new ThemeSettings { Brand = string.IsNullOrWhiteSpace(r.ThemeBrand) ? "#2563eb" : r.ThemeBrand },
                Persona = r.Persona,
                SalesApproach = r.SalesApproach,
                Compliance = r.Compliance,
                Style = r.Style,
                Llm = new LlmOverrides
                {
                    Enabled = r.LlmEnabled, BaseUrl = r.LlmBaseUrl, Model = r.LlmModel,
                    Temperature = r.LlmTemperature, MaxTokens = r.LlmMaxTokens, TimeoutSeconds = r.LlmTimeoutSeconds,
                    ApiKey = null
                },
                Email = new OrderEmailOverrides { Username = r.EmailUsername, To = r.EmailTo, Password = null }
            };
            return s;
        }

        private static void WriteRow(AppSettingsRow r, AppSettings s)
        {
            r.ThemeBrand = s.Theme?.Brand;
            r.Persona = s.Persona;
            r.SalesApproach = s.SalesApproach;
            r.Compliance = s.Compliance;
            r.Style = s.Style;
            r.LlmEnabled = s.Llm?.Enabled;
            r.LlmBaseUrl = s.Llm?.BaseUrl;
            r.LlmModel = s.Llm?.Model;
            r.LlmTemperature = s.Llm?.Temperature;
            r.LlmMaxTokens = s.Llm?.MaxTokens;
            r.LlmTimeoutSeconds = s.Llm?.TimeoutSeconds;
            r.EmailUsername = s.Email?.Username;
            r.EmailTo = s.Email?.To;
            // ไม่เขียน ApiKey/Password — ไม่มีคอลัมน์ (env-only)
        }
    }
}
