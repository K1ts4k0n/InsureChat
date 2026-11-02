using System.Security.Cryptography;
using System.Text;
using InsureChatBot.Api.Data;
using InsureChatBot.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using InsureChatBot.Api.Config;
using Microsoft.Extensions.Configuration;

namespace InsureChatBot.Api.Services.Knowledge
{
    /// <summary>IKnowledgeStore บน EF Core (Singleton + IDbContextFactory). อ่าน wiki dir, parse frontmatter, upsert.</summary>
    public class KnowledgeSyncService : IKnowledgeStore
    {
        private readonly IDbContextFactory<InsureChatDbContext> _factory;
        private readonly string _wikiDir;
        private readonly ILogger<KnowledgeSyncService> _logger;

        // ไฟล์ที่ inject เข้า prompt (ตรงกับ SystemPromptProvider.KnowledgeFiles) — mark IsInjected
        private static readonly HashSet<string> InjectedSlugs = new(StringComparer.OrdinalIgnoreCase)
        {
            "playbooks/spin-selling-การขายแบบที่ปรึกษา",
            "playbooks/รับมือข้อโต้แย้ง-laarc-feel-felt-found",
            "playbooks/เทคนิคปิดการขาย",
            "playbooks/เปรียบเทียบแผน-จับคู่-need",
            "concepts/ประกันรถยนต์",
            "concepts/ประเภทประกันวินาศภัย",
            "concepts/ประเภทประกันชีวิต",
            "concepts/ประกันกลุ่ม-สวัสดิการพนักงาน",
            "concepts/คปภ-การกำกับและจรรยาบรรณตัวแทน",
            "knowledge_base/07-faq-and-consultative-selling-guide",
            "knowledge_base/11-customer-question-keywords",
        };

        public KnowledgeSyncService(IDbContextFactory<InsureChatDbContext> factory, IWebHostEnvironment env, IConfiguration config, ILogger<KnowledgeSyncService> logger)
        {
            _factory = factory;
            _logger = logger;
            _wikiDir = KnowledgePaths.Resolve(env.ContentRootPath, config);
        }

        public int Sync()
        {
            if (!Directory.Exists(_wikiDir)) { _logger.LogWarning("Knowledge sync: หาโฟลเดอร์ Knowledge ไม่เจอ ({Dir})", _wikiDir); return 0; }
            using var db = _factory.CreateDbContext();
            var existing = db.KnowledgeArticles.ToDictionary(a => a.Slug, StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;
            int n = 0;

            foreach (var file in Directory.EnumerateFiles(_wikiDir, "*.md", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(_wikiDir, file).Replace('\\', '/');
                var slug = rel.EndsWith(".md") ? rel[..^3] : rel;
                if (slug is "index" or "log" or "overview") continue;   // ไฟล์ระบบ ไม่ใช่บทความ

                var text = File.ReadAllText(file);
                var hash = Hash(text);
                var fm = ParseFrontmatter(text);

                if (!existing.TryGetValue(slug, out var row))
                {
                    row = new KnowledgeArticleRow { Slug = slug };
                    db.KnowledgeArticles.Add(row);
                }
                else if (row.ContentHash == hash)
                {
                    continue;   // ไม่เปลี่ยน → ข้าม
                }

                row.Title = fm.Title ?? slug;
                row.Type = fm.Type;
                row.Content = fm.Body;
                row.Tags = fm.Tags;
                row.Aliases = fm.Aliases;
                row.SourceFile = rel;
                row.ContentHash = hash;
                row.IsInjected = InjectedSlugs.Contains(slug);
                row.UpdatedAt = now;
                row.SyncedAt = now;
                n++;
            }
            db.SaveChanges();
            return n;
        }

        public List<KnowledgeArticleRow> List()
        {
            using var db = _factory.CreateDbContext();
            return db.KnowledgeArticles.AsNoTracking().OrderBy(a => a.Type).ThenBy(a => a.Slug).ToList();
        }

        public KnowledgeArticleRow Get(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return null;
            using var db = _factory.CreateDbContext();
            return db.KnowledgeArticles.AsNoTracking().FirstOrDefault(a => a.Slug == slug);
        }

        // ===== helpers (static, testable) =====
        public sealed class Frontmatter { public string Type, Title, Aliases, Tags, Body; }

        /// <summary>parse YAML frontmatter แบบง่าย (ไม่ใช้ lib): คั่นด้วย --- บรรทัดแรก. คืน body หลัง frontmatter.</summary>
        public static Frontmatter ParseFrontmatter(string text)
        {
            var fm = new Frontmatter { Body = text ?? "" };
            if (text == null) return fm;
            var t = text.Replace("\r\n", "\n");
            if (!t.StartsWith("---\n")) return fm;
            var end = t.IndexOf("\n---", 4, StringComparison.Ordinal);
            if (end < 0) return fm;

            var head = t.Substring(4, end - 4);
            fm.Body = t[(end + 4)..].TrimStart('\n');
            foreach (var line in head.Split('\n'))
            {
                var idx = line.IndexOf(':');
                if (idx <= 0) continue;
                var key = line[..idx].Trim().ToLowerInvariant();
                var val = line[(idx + 1)..].Trim().Trim('[', ']').Trim();
                switch (key)
                {
                    case "type": fm.Type = val; break;
                    case "title": fm.Title = val; break;
                    case "aliases": fm.Aliases = val; break;
                    case "tags": fm.Tags = val; break;
                }
            }
            return fm;
        }

        public static string Hash(string s)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s ?? ""));
            return Convert.ToHexString(bytes);
        }

    }
}
