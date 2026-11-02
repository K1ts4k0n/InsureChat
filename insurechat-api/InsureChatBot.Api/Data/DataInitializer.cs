using System.Text.Json;
using InsureChatBot.Api.Data.Entities;
using InsureChatBot.Api.Models.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InsureChatBot.Api.Data
{
    /// <summary>
    /// เติมข้อมูลตั้งต้นแบบ idempotent (ADR 0016):
    ///  - ถ้าตาราง Users ว่าง → import จาก Data/users.json (ถ้ามี)
    ///  - ถ้ายังไม่มีแถว AppSettings → import จาก Data/app-settings.json (ถ้ามี) มิฉะนั้นสร้าง default (Id=1)
    /// secret ใน app-settings.json เดิม **ไม่ import** (อยู่ env เท่านั้น)
    /// </summary>
    public static class DataInitializer
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        /// <summary>
        /// เรียก <see cref="Run"/> แบบไม่ปล่อย exception ทะลุออกไปฆ่า process ตอน startup.
        /// ถ้าต่อ DB ไม่ได้ (เช่น .env ไม่ถูกอัปโหลดขึ้นเซิร์ฟเวอร์) แอปต้องยังขึ้นได้
        /// และบอกสาเหตุผ่าน log — ไม่ใช่ตายเงียบเป็น HTTP 502.5 ที่ debug ไม่ได้
        /// </summary>
        public static bool TryRun(IDbContextFactory<InsureChatDbContext> factory, string contentRoot, ILogger logger)
        {
            try
            {
                Run(factory, contentRoot);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "DataInitializer ล้มเหลว — แอปจะสตาร์ตต่อ แต่ฟีเจอร์ที่ใช้ DB จะใช้งานไม่ได้จนกว่าจะเชื่อม DB ได้ " +
                    "(ตรวจ EF_DB_CONNECTION ใน .env และการเข้าถึง SQL Server จากเครื่องนี้)");
                return false;
            }
        }

        public static void Run(IDbContextFactory<InsureChatDbContext> factory, string contentRoot)
        {
            using var db = factory.CreateDbContext();

            // สำหรับ SQLite หรือ DB ที่ยังไม่มีตาราง ให้ EnsureCreated อัตโนมัติ
            db.Database.EnsureCreated();

            // ── Users ──
            if (!db.Users.Any())
            {
                var usersPath = Path.Combine(contentRoot, "Data", "users.json");
                if (File.Exists(usersPath))
                {
                    var list = JsonSerializer.Deserialize<List<User>>(File.ReadAllText(usersPath), JsonOpts) ?? new();
                    var now = DateTime.UtcNow;
                    foreach (var u in list)
                        if (!string.IsNullOrWhiteSpace(u.Username))
                            db.Users.Add(new UserRow
                            {
                                Username = u.Username, PasswordHash = u.PasswordHash, Role = u.Role,
                                CustCode = u.CustCode, Name = u.Name, Email = u.Email,
                                CreatedAt = now, UpdatedAt = now
                            });
                    db.SaveChanges();
                }
            }

            // ── AppSettings (1 แถว) ──
            if (!db.AppSettings.Any())
            {
                var row = new AppSettingsRow { Id = 1, ThemeBrand = "#2563eb", UpdatedAt = DateTime.UtcNow };
                var settingsPath = Path.Combine(contentRoot, "Data", "app-settings.json");
                if (File.Exists(settingsPath))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                    var r = doc.RootElement;
                    if (r.TryGetProperty("Theme", out var t) && t.TryGetProperty("Brand", out var brand))
                        row.ThemeBrand = brand.GetString() ?? "#2563eb";
                    row.Persona = GetStr(r, "Persona");
                    row.SalesApproach = GetStr(r, "SalesApproach");
                    row.Compliance = GetStr(r, "Compliance");
                    row.Style = GetStr(r, "Style");
                    // ไม่ import secret (Llm.ApiKey / Email.Password) — env-only
                }
                db.AppSettings.Add(row);
                db.SaveChanges();
            }
        }

        private static string GetStr(JsonElement r, string name)
            => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
