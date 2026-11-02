# InsureChat SQL — Phase 3 Implementation Plan (Knowledge sync + Admin CRM/viewer)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development for **Part A (backend)**. **Part B (frontend)** is built with the `design-taste-frontend-v1` skill (design-first), not bite-sized TDD. Steps use `- [ ]` checkboxes.

**Goal:** (A) sync วิกิ markdown → ตาราง `KnowledgeArticles` (file = source of truth) + เปิด admin read APIs สำหรับ conversations/orders/leads/knowledge; (B) หน้า admin ใหม่ให้ดู transcript บทสนทนา + CRM คำสั่งซื้อ/lead + คลังความรู้

**Architecture:** Part A = backend ล้วน (EF read-only query service + importer + endpoints ใต้ `[Authorize(Roles=admin)]`), pattern เดียวกับ Phase 1/2 (Singleton + `IDbContextFactory`). Part B = เพิ่มแท็บใน Angular admin component (signals + HttpClient) ด้วย design-taste.

**Tech Stack:** ASP.NET Core net10 + EF Core 10; xUnit + SQLite (test); Angular 21 (admin). ต่อจาก Phase 2 (HEAD `2cc4c86`).

---

## หมายเหตุ
- รันจาก root `c:\CTD\ChatBot\InsureChat` (PowerShell). commit ต่อ task; จบ body ด้วย `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- DB remote เข้าถึงได้; `sqlcmd` ใช้ได้; migration apply ได้จริง
- **admin endpoints ทดสอบ HTTP เต็มไม่ได้** (ไม่รู้รหัส admin) → คุมด้วย unit test ของ query/importer + build; HTTP smoke เท่าที่ทำได้
- knowledge ปัจจุบัน **inject 6 ไฟล์เข้า prompt** ผ่าน `SystemPromptProvider` (locked) — Phase 3 **ไม่เปลี่ยน** การ inject นั้น; `KnowledgeArticles` เป็น mirror (file→DB) สำหรับ admin ดู + future search (ไม่ re-wire chatbot — YAGNI). ไฟล์ markdown ยังเป็น source of truth
- ทุก endpoint ใหม่ `[Authorize(Roles = UserRoles.Admin)]`

---

# PART A — Backend (subagent-driven)

## โครงไฟล์ (Part A)
**สร้าง:** `Data/Entities/KnowledgeArticleRow.cs` · `Services/Knowledge/IKnowledgeStore.cs` + `KnowledgeSyncService.cs` (importer+read) · `Services/Admin/IAdminQueryService.cs` + `EfAdminQueryService.cs` · `Models/Admin/AdminDataDtos.cs` · `Controllers/AdminDataController.cs` · tests `KnowledgeSyncServiceTests.cs`, `EfAdminQueryServiceTests.cs` · `Migrations/*`
**แก้:** `Data/InsureChatDbContext.cs` (DbSet+config) · `Program.cs` (DI)

---

## Task A1: KnowledgeArticleRow + DbContext + migration

**Files:** Create `app/api/Data/Entities/KnowledgeArticleRow.cs`; Modify `app/api/Data/InsureChatDbContext.cs`; Create `Migrations/*`

- [ ] **Step 1: create `app/api/Data/Entities/KnowledgeArticleRow.cs`**
```csharp
namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>mirror วิกิ 1 หน้า (file→DB sync ทางเดียว) — ADR 0016 Phase 3. ไฟล์ markdown = source of truth.</summary>
    public class KnowledgeArticleRow
    {
        public long Id { get; set; }
        public string Slug { get; set; }        // path สัมพัทธ์ไม่รวม .md เช่น "concepts/ประกันรถยนต์" (unique)
        public string Title { get; set; }
        public string Type { get; set; }        // concept | playbook | source | product | ...
        public string Content { get; set; }     // markdown body (หลัง frontmatter)
        public string Tags { get; set; }        // csv
        public string Aliases { get; set; }     // csv
        public string SourceFile { get; set; }  // path สัมพัทธ์รวม .md
        public string ContentHash { get; set; } // SHA256 hex ของไฟล์เต็ม (ตรวจ change)
        public bool IsInjected { get; set; }    // เป็น 1 ใน 6 ไฟล์ที่ป้อน prompt ไหม
        public DateTime UpdatedAt { get; set; }
        public DateTime SyncedAt { get; set; }
    }
}
```

- [ ] **Step 2: DbSet + config in `InsureChatDbContext.cs`.** Add DbSet after `Orders`:
```csharp
        public DbSet<KnowledgeArticleRow> KnowledgeArticles => Set<KnowledgeArticleRow>();
```
Append config in `OnModelCreating`:
```csharp
            b.Entity<KnowledgeArticleRow>(e =>
            {
                e.ToTable("KnowledgeArticles");
                e.HasKey(x => x.Id);
                e.Property(x => x.Slug).HasMaxLength(256).IsRequired();
                e.HasIndex(x => x.Slug).IsUnique();
                e.Property(x => x.Title).HasMaxLength(256);
                e.Property(x => x.Type).HasMaxLength(32);
                e.Property(x => x.SourceFile).HasMaxLength(512);
                e.Property(x => x.ContentHash).HasMaxLength(64);
            });
```

- [ ] **Step 3: build** `dotnet build app/api/InsureChatBot.Api.csproj` → succeeded
- [ ] **Step 4: migration** `dotnet ef migrations add Phase3_KnowledgeArticles --project app/api/InsureChatBot.Api.csproj --output-dir Migrations` — inspect `Up()`: only `CreateTable("KnowledgeArticles")` + unique index on `Slug` (no other table touched). commit `git add app/api/Data app/api/Migrations` → `git commit -m "feat: add KnowledgeArticleRow + Phase3 migration"`
- [ ] **Step 5: apply** `dotnet ef database update --project app/api/InsureChatBot.Api.csproj` → "Done." then `sqlcmd -S 210.1.60.34,14333 -U clicktodesign -P "c1!ckt0" -d InsureChat -Q "SELECT name FROM sys.tables WHERE name='KnowledgeArticles'"` → returns the table

---

## Task A2: KnowledgeSyncService (importer + read) — TDD on frontmatter parser

**Files:** Create `app/api/Services/Knowledge/IKnowledgeStore.cs`, `app/api/Services/Knowledge/KnowledgeSyncService.cs`, `app/api.Tests/KnowledgeSyncServiceTests.cs`

- [ ] **Step 1: write tests `app/api.Tests/KnowledgeSyncServiceTests.cs`** (test the static frontmatter parser — pure, no DB):
```csharp
using InsureChatBot.Api.Services.Knowledge;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class KnowledgeSyncServiceTests
    {
        private const string Sample =
            "---\n" +
            "type: concept\n" +
            "title: ประกันรถยนต์ (ชั้น 1/2+/3+)\n" +
            "aliases: [ประกันรถ, ชั้น 1, motor]\n" +
            "tags: [concept, motor]\n" +
            "sources: [S-007]\n" +
            "---\n" +
            "\n# ประกันรถยนต์\n\nเนื้อหา body ตรงนี้\n";

        [Fact]
        public void Parse_extracts_frontmatter_and_body()
        {
            var p = KnowledgeSyncService.ParseFrontmatter(Sample);
            Assert.Equal("concept", p.Type);
            Assert.Equal("ประกันรถยนต์ (ชั้น 1/2+/3+)", p.Title);
            Assert.Equal("ประกันรถ, ชั้น 1, motor", p.Aliases);
            Assert.Equal("concept, motor", p.Tags);
            Assert.Contains("# ประกันรถยนต์", p.Body);
            Assert.DoesNotContain("type: concept", p.Body);   // body ไม่รวม frontmatter
        }

        [Fact]
        public void Parse_handles_missing_frontmatter()
        {
            var p = KnowledgeSyncService.ParseFrontmatter("# หัวข้อ\nเนื้อหา");
            Assert.Null(p.Type);
            Assert.Contains("# หัวข้อ", p.Body);
        }

        [Fact]
        public void Hash_is_stable_and_changes_with_content()
        {
            Assert.Equal(KnowledgeSyncService.Hash("abc"), KnowledgeSyncService.Hash("abc"));
            Assert.NotEqual(KnowledgeSyncService.Hash("abc"), KnowledgeSyncService.Hash("abd"));
        }
    }
}
```

- [ ] **Step 2: run → fail** (`KnowledgeSyncService` missing): `dotnet test app/api.Tests/InsureChatBot.Api.Tests.csproj`

- [ ] **Step 3: create `app/api/Services/Knowledge/IKnowledgeStore.cs`**
```csharp
using InsureChatBot.Api.Data.Entities;

namespace InsureChatBot.Api.Services.Knowledge
{
    /// <summary>sync วิกิ (file→DB) + อ่านบทความ (ADR 0016 Phase 3). ไฟล์ = source of truth.</summary>
    public interface IKnowledgeStore
    {
        /// <summary>สแกน wiki/*.md → upsert ตาม Slug (เทียบ ContentHash). คืนจำนวนที่ sync.</summary>
        int Sync();
        List<KnowledgeArticleRow> List();
        KnowledgeArticleRow Get(string slug);
    }
}
```

- [ ] **Step 4: create `app/api/Services/Knowledge/KnowledgeSyncService.cs`**
```csharp
using System.Security.Cryptography;
using System.Text;
using InsureChatBot.Api.Data;
using InsureChatBot.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsureChatBot.Api.Services.Knowledge
{
    /// <summary>IKnowledgeStore บน EF Core (Singleton + IDbContextFactory). อ่าน wiki dir, parse frontmatter, upsert.</summary>
    public class KnowledgeSyncService : IKnowledgeStore
    {
        private readonly IDbContextFactory<InsureChatDbContext> _factory;
        private readonly string _wikiDir;
        private readonly ILogger<KnowledgeSyncService> _logger;

        // 6 ไฟล์ที่ inject เข้า prompt (ตรงกับ SystemPromptProvider.KnowledgeFiles) — mark IsInjected
        private static readonly HashSet<string> InjectedSlugs = new(StringComparer.OrdinalIgnoreCase)
        {
            "playbooks/spin-selling-การขายแบบที่ปรึกษา",
            "playbooks/รับมือข้อโต้แย้ง-laarc-feel-felt-found",
            "playbooks/เทคนิคปิดการขาย",
            "playbooks/เปรียบเทียบแผน-จับคู่-need",
            "concepts/ประกันรถยนต์",
            "concepts/คปภ-การกำกับและจรรยาบรรณตัวแทน",
        };

        public KnowledgeSyncService(IDbContextFactory<InsureChatDbContext> factory, IWebHostEnvironment env, ILogger<KnowledgeSyncService> logger)
        {
            _factory = factory;
            _logger = logger;
            _wikiDir = FindWikiDir(env.ContentRootPath);
        }

        public int Sync()
        {
            if (_wikiDir == null) { _logger.LogWarning("Knowledge sync: หาโฟลเดอร์ wiki ไม่เจอ"); return 0; }
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

        private static string FindWikiDir(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            for (int i = 0; i <= 4 && dir != null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "wiki");
                if (Directory.Exists(candidate)) return candidate;
            }
            return null;
        }
    }
}
```

- [ ] **Step 5: run → pass** (`dotnet test ...`, 14 + 3 = 17)
- [ ] **Step 6: commit** `git add app/api/Services/Knowledge app/api.Tests/KnowledgeSyncServiceTests.cs` → `git commit -m "feat: KnowledgeSyncService imports wiki markdown to DB"`

---

## Task A3: EfAdminQueryService (conversations + orders read) + DTOs — TDD

**Files:** Create `app/api/Models/Admin/AdminDataDtos.cs`, `app/api/Services/Admin/IAdminQueryService.cs`, `app/api/Services/Admin/EfAdminQueryService.cs`, `app/api.Tests/EfAdminQueryServiceTests.cs`

- [ ] **Step 1: create `app/api/Models/Admin/AdminDataDtos.cs`**
```csharp
namespace InsureChatBot.Api.Models.Admin
{
    public class ConversationSummaryDto
    {
        public long Id { get; set; }
        public string SessionKey { get; set; }
        public string CarBrand { get; set; }
        public string CarModel { get; set; }
        public int? CarYear { get; set; }
        public string ProspectName { get; set; }
        public string ProspectPhone { get; set; }
        public string OutcomeStatus { get; set; }
        public int TurnCount { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }

    public class ConversationTurnDto { public int Seq { get; set; } public string Role { get; set; } public string Text { get; set; } public DateTime Timestamp { get; set; } }

    public class ConversationDetailDto : ConversationSummaryDto
    {
        public string ProductType { get; set; }
        public string InsuranceClass { get; set; }
        public decimal? SumInsure { get; set; }
        public string ProspectEmail { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Needs { get; set; } = new();
        public List<ConversationTurnDto> Turns { get; set; } = new();
    }

    public class OrderListDto
    {
        public long Id { get; set; }
        public string OrderNo { get; set; }
        public string CustName { get; set; }
        public string CustPhone { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string InsurerName { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; }
        public string EmailStatus { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
```

- [ ] **Step 2: write tests `app/api.Tests/EfAdminQueryServiceTests.cs`**
```csharp
using InsureChatBot.Api.Models.Chatbot;
using InsureChatBot.Api.Services.Admin;
using InsureChatBot.Api.Services.Conversation;
using InsureChatBot.Api.Services.Orders;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class EfAdminQueryServiceTests
    {
        [Fact]
        public void ListConversations_returns_summary_with_turncount()
        {
            using var f = new TestDbContextFactory();
            new EfConversationStore(f).Upsert(new ChatbotConversationContext
            {
                SessionId = "s1", CarBrand = "HONDA", ProspectName = "ก",
                Turns = new() { new ChatbotTurn { Role = "user", Text = "hi" }, new ChatbotTurn { Role = "assistant", Text = "ค่ะ" } }
            });
            var q = new EfAdminQueryService(f);

            var list = q.ListConversations(50, 0, null);
            Assert.Single(list);
            Assert.Equal(2, list[0].TurnCount);
            Assert.Equal("HONDA", list[0].CarBrand);
        }

        [Fact]
        public void GetConversation_returns_turns_ordered()
        {
            using var f = new TestDbContextFactory();
            new EfConversationStore(f).Upsert(new ChatbotConversationContext
            {
                SessionId = "s1",
                Turns = new() { new ChatbotTurn { Role = "user", Text = "a" }, new ChatbotTurn { Role = "assistant", Text = "b" } }
            });
            var q = new EfAdminQueryService(f);
            var id = q.ListConversations(50, 0, null)[0].Id;

            var detail = q.GetConversation(id);
            Assert.NotNull(detail);
            Assert.Equal(2, detail.Turns.Count);
            Assert.Equal(0, detail.Turns[0].Seq);
        }

        [Fact]
        public void ListOrders_returns_orders_newest_first()
        {
            using var f = new TestDbContextFactory();
            var os = new EfOrderStore(f);
            os.CreateOrder(new ChatbotConversationContext { ProspectName = "ก", ProspectPhone = "0800000000" },
                new ChatbotProposalSummary { PackageId = 1, Brand = "HONDA", Model = "CIVIC", Year = 2020, InsurerName = "x", Premium = 1, Total = 1 }, "sent");
            var q = new EfAdminQueryService(f);

            var orders = q.ListOrders(50, 0);
            Assert.Single(orders);
            Assert.StartsWith("ORD-", orders[0].OrderNo);
        }
    }
}
```

- [ ] **Step 3: run → fail.**

- [ ] **Step 4: create `app/api/Services/Admin/IAdminQueryService.cs`**
```csharp
using InsureChatBot.Api.Models.Admin;

namespace InsureChatBot.Api.Services.Admin
{
    /// <summary>read-only query สำหรับหน้า admin (ADR 0016 Phase 3) — conversations/orders.</summary>
    public interface IAdminQueryService
    {
        List<ConversationSummaryDto> ListConversations(int take, int skip, string search);
        ConversationDetailDto GetConversation(long id);
        List<OrderListDto> ListOrders(int take, int skip);
    }
}
```

- [ ] **Step 5: create `app/api/Services/Admin/EfAdminQueryService.cs`**
```csharp
using InsureChatBot.Api.Data;
using InsureChatBot.Api.Models.Admin;
using Microsoft.EntityFrameworkCore;

namespace InsureChatBot.Api.Services.Admin
{
    /// <summary>IAdminQueryService บน EF Core (Singleton + IDbContextFactory).</summary>
    public class EfAdminQueryService : IAdminQueryService
    {
        private readonly IDbContextFactory<InsureChatDbContext> _factory;
        public EfAdminQueryService(IDbContextFactory<InsureChatDbContext> factory) => _factory = factory;

        public List<ConversationSummaryDto> ListConversations(int take, int skip, string search)
        {
            using var db = _factory.CreateDbContext();
            var query = db.Conversations.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(c => (c.ProspectName != null && c.ProspectName.Contains(s))
                                      || (c.ProspectPhone != null && c.ProspectPhone.Contains(s))
                                      || (c.CarBrand != null && c.CarBrand.Contains(s)));
            }
            return query.OrderByDescending(c => c.LastUpdatedAt)
                .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200))
                .Select(c => new ConversationSummaryDto
                {
                    Id = c.Id, SessionKey = c.SessionKey, CarBrand = c.CarBrand, CarModel = c.CarModel, CarYear = c.CarYear,
                    ProspectName = c.ProspectName, ProspectPhone = c.ProspectPhone, OutcomeStatus = c.OutcomeStatus,
                    TurnCount = c.Turns.Count, LastUpdatedAt = c.LastUpdatedAt
                }).ToList();
        }

        public ConversationDetailDto GetConversation(long id)
        {
            using var db = _factory.CreateDbContext();
            var c = db.Conversations.AsNoTracking().Include(x => x.Turns).Include(x => x.Needs)
                      .FirstOrDefault(x => x.Id == id);
            if (c == null) return null;
            return new ConversationDetailDto
            {
                Id = c.Id, SessionKey = c.SessionKey, CarBrand = c.CarBrand, CarModel = c.CarModel, CarYear = c.CarYear,
                ProspectName = c.ProspectName, ProspectPhone = c.ProspectPhone, ProspectEmail = c.ProspectEmail,
                OutcomeStatus = c.OutcomeStatus, ProductType = c.ProductType, InsuranceClass = c.InsuranceClass,
                SumInsure = c.SumInsure, CreatedAt = c.CreatedAt, LastUpdatedAt = c.LastUpdatedAt,
                TurnCount = c.Turns.Count,
                Needs = c.Needs.Select(n => n.Need).ToList(),
                Turns = c.Turns.OrderBy(t => t.Seq).Select(t => new ConversationTurnDto
                { Seq = t.Seq, Role = t.Role, Text = t.Text, Timestamp = t.Timestamp }).ToList()
            };
        }

        public List<OrderListDto> ListOrders(int take, int skip)
        {
            using var db = _factory.CreateDbContext();
            return db.Orders.AsNoTracking().OrderByDescending(o => o.Id)
                .Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200))
                .Select(o => new OrderListDto
                {
                    Id = o.Id, OrderNo = o.OrderNo, CustName = o.CustName, CustPhone = o.CustPhone,
                    Brand = o.Brand, Model = o.Model, InsurerName = o.InsurerName, Total = o.Total,
                    Status = o.Status, EmailStatus = o.EmailStatus, CreatedAt = o.CreatedAt
                }).ToList();
        }
    }
}
```

- [ ] **Step 6: run → pass** (17 + 3 = 20). **commit** `git add app/api/Models/Admin/AdminDataDtos.cs app/api/Services/Admin app/api.Tests/EfAdminQueryServiceTests.cs` → `git commit -m "feat: EfAdminQueryService (conversations/orders read DTOs)"`

---

## Task A4: AdminDataController + DI

**Files:** Create `app/api/Controllers/AdminDataController.cs`; Modify `app/api/Program.cs`

- [ ] **Step 1: DI in `Program.cs`** — after the Phase 2 store registrations add:
```csharp
builder.Services.AddSingleton<InsureChatBot.Api.Services.Knowledge.IKnowledgeStore, InsureChatBot.Api.Services.Knowledge.KnowledgeSyncService>();
builder.Services.AddSingleton<InsureChatBot.Api.Services.Admin.IAdminQueryService, InsureChatBot.Api.Services.Admin.EfAdminQueryService>();
```

- [ ] **Step 2: create `app/api/Controllers/AdminDataController.cs`**
```csharp
using InsureChatBot.Api.Models.Auth;
using InsureChatBot.Api.Services.Admin;
using InsureChatBot.Api.Services.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InsureChatBot.Api.Controllers
{
    /// <summary>หลังบ้าน /admin — ดูบทสนทนา/คำสั่งซื้อ/คลังความรู้ (ADR 0016 Phase 3). admin เท่านั้น.</summary>
    [Authorize(Roles = UserRoles.Admin)]
    [Route("api/Admin")]
    [ApiController]
    public class AdminDataController : ControllerBase
    {
        private readonly IAdminQueryService _query;
        private readonly IKnowledgeStore _knowledge;

        public AdminDataController(IAdminQueryService query, IKnowledgeStore knowledge)
        {
            _query = query;
            _knowledge = knowledge;
        }

        [HttpGet("Conversations")]
        public IActionResult Conversations([FromQuery] int take = 50, [FromQuery] int skip = 0, [FromQuery] string q = null)
            => Ok(_query.ListConversations(take, skip, q));

        [HttpGet("Conversations/{id:long}")]
        public IActionResult Conversation(long id)
        {
            var d = _query.GetConversation(id);
            return d == null ? NotFound(new { message = "ไม่พบบทสนทนา" }) : Ok(d);
        }

        [HttpGet("Orders")]
        public IActionResult Orders([FromQuery] int take = 50, [FromQuery] int skip = 0)
            => Ok(_query.ListOrders(take, skip));

        [HttpGet("Knowledge")]
        public IActionResult Knowledge()
            => Ok(_knowledge.List().Select(a => new { a.Slug, a.Title, a.Type, a.IsInjected, a.UpdatedAt }));

        [HttpGet("Knowledge/Article")]
        public IActionResult KnowledgeArticle([FromQuery] string slug)
        {
            var a = _knowledge.Get(slug);
            return a == null ? NotFound(new { message = "ไม่พบบทความ" })
                : Ok(new { a.Slug, a.Title, a.Type, a.Content, a.Tags, a.Aliases, a.IsInjected, a.UpdatedAt });
        }

        [HttpPost("Knowledge/Sync")]
        public IActionResult SyncKnowledge() => Ok(new { synced = _knowledge.Sync() });
    }
}
```
> หมายเหตุ: route `api/Admin` เดียวกับ `AdminController` เดิม แต่คนละ action paths — ASP.NET รวม controllers ได้ (ไม่ชนกันเพราะ path ต่าง). ถ้า build ฟ้อง ambiguous ให้เปลี่ยน route เป็น `api/Admin/Data`

- [ ] **Step 3: build + test** — `dotnet build app/api/InsureChatBot.Api.csproj` (succeeded) + `dotnet test app/api.Tests/InsureChatBot.Api.Tests.csproj` (20/20). 
- [ ] **Step 4: smoke เท่าที่ทำได้** (admin endpoints ต้อง token — ทดสอบว่า **กันสิทธิ์**ทำงาน): start app (background), `curl -s -o /dev/null -w "%{http_code}" http://localhost:PORT/api/Admin/Conversations` → คาดหวัง **401** (ไม่มี token). stop app. (การทำงานจริงของ query คุมด้วย unit test แล้ว)
- [ ] **Step 5: commit** `git add app/api/Controllers/AdminDataController.cs app/api/Program.cs` → `git commit -m "feat: AdminDataController (conversations/orders/knowledge admin APIs)"`

---

## Task A5: docs — Phase 3 backend done
- [ ] ใน `docs/adr/0016-insurechat-owns-sql-db-ef-core.md` แก้บรรทัด Phase 3 เป็น `- **Phase 3 (backend ทำแล้ว):** \`KnowledgeArticles\` + importer (file→DB sync, 6 ไฟล์ inject = IsInjected) + admin read APIs (conversations/orders/knowledge ใต้ \`[Authorize admin]\`). frontend admin CRM = Part B.` commit `git commit -m "docs: mark ADR 0016 Phase 3 backend done"`

### DoD Part A
- [ ] build + 20 tests ผ่าน; ตาราง `KnowledgeArticles` มีจริง; admin endpoints คืน 401 เมื่อไม่มี token; importer parse frontmatter + hash ถูก

---

# PART B — Frontend admin (design-taste-frontend-v1)

> **ไม่ใช่ bite-sized TDD — ใช้สกิล `design-taste-frontend-v1`.** Build หน้า admin ใหม่ให้ดูข้อมูลที่ Part A เปิดไว้

**ขอบเขต UI (เพิ่มใน `app/web/src/app/features/admin/`):**
- เพิ่มแท็บใน admin component: **บทสนทนา (Conversations)**, **คำสั่งซื้อ/Leads (Orders)**, **คลังความรู้ (Knowledge)** ต่อจากแท็บเดิม (prompt/llm/theme/email) — `type Tab` ขยาย
- **Conversations:** ตารางรายการ (รถ/ชื่อ/เบอร์/สถานะ/จำนวนเทิร์น/เวลา) + ช่องค้นหา → คลิกเปิด **transcript viewer** (modal/แผงข้าง) แสดง turns user/assistant แบบ chat bubble + needs/ข้อมูลรถ. เรียก `GET /Admin/Conversations`, `/Admin/Conversations/{id}`
- **Orders/Leads:** ตาราง CRM (OrderNo/ชื่อ/เบอร์/รถ/บริษัท/ยอด/สถานะ/อีเมล/วันที่). เรียก `GET /Admin/Orders`. ("Leads" = conversations มีเบอร์แต่ยังไม่ ordered — เฟสนี้โชว์จาก Conversations ที่ outcomeStatus=open + มีเบอร์ ก็พอ)
- **Knowledge:** ตาราง (title/type/inject badge) + ปุ่ม **"ซิงก์จากไฟล์"** (`POST /Admin/Knowledge/Sync`) + คลิกดูเนื้อหา (`GET /Admin/Knowledge/Article?slug=`) render markdown read-only (มี `marked` อยู่แล้วใน chatbot)
- ใช้ design system/โทนเดียวกับ admin เดิม (signals + HttpClient + `environment.apiUrl/Admin`, auth interceptor แนบ token ให้แล้ว); รองรับ light/dark + brand theme เดิม
- **verify:** login admin จริง (ผู้ใช้มีรหัส) → เปิด /admin → เห็น 3 แท็บใหม่ ดึงข้อมูลจาก DB จริงได้; ปุ่ม sync ทำให้ KnowledgeArticles เต็ม

**DoD Part B:** 3 แท็บใหม่ทำงานกับ API จริง; transcript อ่านได้; ปุ่ม sync เติม knowledge; ดีไซน์เนียนกับของเดิม; `ng build` ผ่าน

## นอก scope (อนาคต)
- chatbot keyword-search จาก KnowledgeArticles (ตอนนี้ knowledge ยัง inject จากไฟล์); status pipeline เต็ม (contacted/won/lost) + แก้สถานะจาก UI; export CSV; retention/anonymize PII job
