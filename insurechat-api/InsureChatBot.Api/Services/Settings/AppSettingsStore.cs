using System.Text.Json;
using InsureChatBot.Api.Config;
using InsureChatBot.Api.Models.Admin;
using Microsoft.Extensions.Options;

namespace InsureChatBot.Api.Services.Settings
{
    /// <summary>
    /// อ่าน/เขียน app-settings.json (cache ในหน่วยความจำ + lock). ADR 0010/0011.
    /// base ของ LLM มาจาก IOptions&lt;LlmSettings&gt; (config + env key ที่ PostConfigure เติมแล้ว).
    /// </summary>
    public class AppSettingsStore : IAppSettingsStore
    {
        private readonly string _path;
        private readonly LlmSettings _baseLlm;
        private readonly OrderEmailSettings _baseEmail;
        private readonly object _lock = new();
        private AppSettings _settings;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public AppSettingsStore(IOptions<LlmSettings> baseLlm, IOptions<OrderEmailSettings> baseEmail, IOptions<UserStoreSettings> userStore, IWebHostEnvironment env)
        {
            _baseLlm = baseLlm.Value;
            _baseEmail = baseEmail.Value;

            // วาง app-settings.json คู่กับ users.json (โฟลเดอร์ Data)
            var dataDir = !string.IsNullOrWhiteSpace(userStore.Value?.JsonPath)
                ? Path.GetDirectoryName(userStore.Value.JsonPath)!
                : Path.Combine(env.ContentRootPath, "Data");
            Directory.CreateDirectory(dataDir);
            _path = Path.Combine(dataDir, "app-settings.json");

            _settings = Load();
        }

        private AppSettings Load()
        {
            try
            {
                if (File.Exists(_path))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOpts) ?? new AppSettings();
            }
            catch { /* ไฟล์เสีย → ใช้ default (แอปไม่พัง, ADR 0010 guardrail) */ }
            return new AppSettings();
        }

        public AppSettings Get()
        {
            lock (_lock) return _settings;
        }

        public void Update(Action<AppSettings> mutate)
        {
            lock (_lock)
            {
                mutate(_settings);
                File.WriteAllText(_path, JsonSerializer.Serialize(_settings, JsonOpts));
            }
        }

        public LlmSettings GetEffectiveLlm()
        {
            AppSettings s;
            lock (_lock) s = _settings;
            var o = s.Llm ?? new LlmOverrides();
            var b = _baseLlm;

            return new LlmSettings
            {
                Enabled = o.Enabled ?? b.Enabled,
                BaseUrl = string.IsNullOrWhiteSpace(o.BaseUrl) ? b.BaseUrl : o.BaseUrl,
                Model = string.IsNullOrWhiteSpace(o.Model) ? b.Model : o.Model,
                VisionModel = string.IsNullOrWhiteSpace(o.VisionModel) ? b.VisionModel : o.VisionModel,
                ApiKey = string.IsNullOrWhiteSpace(o.ApiKey) ? b.ApiKey : o.ApiKey,
                Temperature = o.Temperature ?? b.Temperature,
                MaxTokens = o.MaxTokens ?? b.MaxTokens,
                TimeoutSeconds = o.TimeoutSeconds ?? b.TimeoutSeconds,
                MaxToolIterations = b.MaxToolIterations // ไม่เปิดให้ admin แก้
            };
        }

        public string GetKeySource()
        {
            AppSettings s;
            lock (_lock) s = _settings;
            if (!string.IsNullOrWhiteSpace(s.Llm?.ApiKey)) return "admin";
            if (!string.IsNullOrWhiteSpace(_baseLlm.ApiKey)) return "env";
            return "none";
        }

        public OrderEmailSettings GetEffectiveEmail()
        {
            AppSettings s;
            lock (_lock) s = _settings;
            var o = s.Email ?? new OrderEmailOverrides();
            var b = _baseEmail;

            // อีเมลผู้ส่ง: admin-set ก่อน, ไม่มี → base/env. From ผูก = Username เสมอ (Gmail บังคับ)
            var username = string.IsNullOrWhiteSpace(o.Username) ? b.Username : o.Username.Trim();
            return new OrderEmailSettings
            {
                Host = b.Host,                 // host/port ตรึงที่ Gmail (ADR 0013) — ไม่เปิดให้แก้
                Port = b.Port,
                Username = username,
                From = username,
                Password = string.IsNullOrWhiteSpace(o.Password) ? b.Password : o.Password,
                To = string.IsNullOrWhiteSpace(o.To) ? b.To : o.To.Trim()
            };
        }

        public string GetEmailKeySource()
        {
            AppSettings s;
            lock (_lock) s = _settings;
            if (!string.IsNullOrWhiteSpace(s.Email?.Password)) return "admin";
            if (!string.IsNullOrWhiteSpace(_baseEmail.Password)) return "env";
            return "none";
        }
    }
}
