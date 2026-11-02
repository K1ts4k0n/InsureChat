# InsureChat SQL — Phase 2 Implementation Plan (Conversations + Orders)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** เก็บบทสนทนา (ทุก session) และคำสั่งซื้อ (Orders) ลง DB `InsureChat` โดย hook เข้า `ChatbotService` — เป็น **backend ล้วน** (ไม่แตะ Angular)

**Architecture:** เพิ่ม 4 ตาราง (`Conversations`, `ConversationTurns`, `ConversationNeeds`, `Orders`) ใน `InsureChatDbContext`. ใส่ `SessionId` ลงใน `ChatbotConversationContext` ซึ่ง Angular round-trip ทั้งก้อนเป็น `any` อยู่แล้ว → SessionId ติดกลับมาทุกเทิร์นโดยไม่ต้องแก้ frontend. สร้าง `EfConversationStore`/`EfOrderStore` (Singleton + `IDbContextFactory` ตามแพตเทิร์น Phase 1). `ChatbotService` upsert conversation ทุกเทิร์น (ProcessMessage + Stream) และ insert Order ตอน SubmitProposal สำเร็จ. candidate set (≤500) ไม่ persist.

**Tech Stack:** ASP.NET Core net10, EF Core 10 (SqlServer), xUnit + EF Core Sqlite in-memory (test). ต่อยอดจาก Phase 1 (commit ล่าสุดบน branch `master`).

---

## หมายเหตุก่อนเริ่ม
- รันคำสั่งจาก root `c:\CTD\ChatBot\InsureChat` (PowerShell). repo เป็น git แล้ว — commit ต่อ task; จบ commit body ด้วย `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`
- DB remote `210.1.60.34,14333 / InsureChat` เข้าถึงได้ (ยืนยันใน Phase 1); `sqlcmd` ใช้ได้
- spec อ้างอิง: `docs/superpowers/specs/2026-06-18-insurechat-sql-database-design.md` (§4.3–4.6, §5)
- **ห้ามแตะ Angular** (`app/web`) — SessionId ไป-กลับผ่าน context `any` ที่ client เก็บ/ส่งคืนทั้งก้อนอยู่แล้ว
- เขียน DB ต้อง **ไม่ทำให้แชตพัง**: ทุก hook ห่อ try/catch + log (graceful degradation)
- **Deviation จาก spec §4.3:** `Conversations` ไม่มีคอลัมน์ `Province` เพราะ `ChatbotConversationContext` ไม่มีฟิลด์จังหวัด (จังหวัดเก็บใน proposal/quote เท่านั้น) — ไม่ persist สิ่งที่ context ไม่พก. `Orders` ใช้ `InsuranceClass` (จาก `ChatbotProposalSummary`) แทน `PolTypeName`

## โครงไฟล์ (สร้าง/แก้)
**สร้าง:** `Data/Entities/ConversationRow.cs`, `ConversationTurnRow.cs`, `ConversationNeedRow.cs`, `OrderRow.cs` · `Services/Conversation/IConversationStore.cs` + `EfConversationStore.cs` · `Services/Orders/IOrderStore.cs` + `EfOrderStore.cs` · `app/api.Tests/EfConversationStoreTests.cs`, `EfOrderStoreTests.cs` · `Migrations/*` (generated)
**แก้:** `Models/Chatbot/ChatbotModels.cs` (เพิ่ม `SessionId`) · `Data/InsureChatDbContext.cs` (4 DbSet + config) · `Services/Implementations/ChatbotService.cs` (hook) · `Program.cs` (DI 2 store)

---

## Task 1: เพิ่ม SessionId + 4 entities + DbContext config

**Files:** Modify `app/api/Models/Chatbot/ChatbotModels.cs`, `app/api/Data/InsureChatDbContext.cs`; Create 4 entity files.

- [ ] **Step 1: เพิ่ม `SessionId` ใน `ChatbotConversationContext`.** ในไฟล์ `app/api/Models/Chatbot/ChatbotModels.cs` ภายใน class `ChatbotConversationContext` เพิ่ม property นี้ไว้บนสุด (ก่อน `Turns`):
```csharp
        // รหัส session (ตั้งฝั่ง server เทิร์นแรก, round-trip กลับผ่าน context — ใช้ correlate การเก็บลง DB, ADR 0016 Phase 2)
        public string SessionId { get; set; }
```

- [ ] **Step 2: สร้าง `app/api/Data/Entities/ConversationRow.cs`**
```csharp
namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>ตาราง Conversations — 1 session = 1 แถว (ADR 0016 Phase 2). candidate set ไม่เก็บ.</summary>
    public class ConversationRow
    {
        public long Id { get; set; }
        public string SessionKey { get; set; }      // = ChatbotConversationContext.SessionId (unique)
        public string ProductType { get; set; }
        public string CarBrand { get; set; }
        public string CarModel { get; set; }
        public int? CarYear { get; set; }
        public string InsuranceClass { get; set; }
        public decimal? SumInsure { get; set; }
        public string CurrentStep { get; set; }
        public string SpinReason { get; set; }
        public string ProspectName { get; set; }
        public string ProspectPhone { get; set; }
        public string ProspectEmail { get; set; }
        public string ProspectIdCard { get; set; }
        public string ProspectLicensePlate { get; set; }
        public string OutcomeStatus { get; set; } = "open"; // open | ordered
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }

        public ICollection<ConversationTurnRow> Turns { get; set; } = new List<ConversationTurnRow>();
        public ICollection<ConversationNeedRow> Needs { get; set; } = new List<ConversationNeedRow>();
    }
}
```

- [ ] **Step 3: สร้าง `app/api/Data/Entities/ConversationTurnRow.cs`**
```csharp
namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>1 turn ของบทสนทนา (transcript) — ADR 0016 Phase 2</summary>
    public class ConversationTurnRow
    {
        public long Id { get; set; }
        public long ConversationId { get; set; }
        public int Seq { get; set; }
        public string Role { get; set; }   // user | assistant
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
        public ConversationRow Conversation { get; set; }
    }
}
```

- [ ] **Step 4: สร้าง `app/api/Data/Entities/ConversationNeedRow.cs`**
```csharp
namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>1 ความต้องการที่ค้นพบ (SPIN) — ADR 0016 Phase 2</summary>
    public class ConversationNeedRow
    {
        public long Id { get; set; }
        public long ConversationId { get; set; }
        public string Need { get; set; }
        public ConversationRow Conversation { get; set; }
    }
}
```

- [ ] **Step 5: สร้าง `app/api/Data/Entities/OrderRow.cs`**
```csharp
namespace InsureChatBot.Api.Data.Entities
{
    /// <summary>คำสั่งซื้อที่บันทึก (snapshot ลูกค้า/รถ/แผน/ราคา) เพิ่มจากการยิงอีเมล — ADR 0016 Phase 2</summary>
    public class OrderRow
    {
        public long Id { get; set; }
        public string OrderNo { get; set; }          // ORD-yyyy-000123 (unique)
        public long? ConversationId { get; set; }    // อ้างอิง session (ถ้ามี)

        public string CustName { get; set; }
        public string CustPhone { get; set; }
        public string CustEmail { get; set; }
        public string CustIdCard { get; set; }
        public string LicensePlate { get; set; }

        public string Brand { get; set; }
        public string Model { get; set; }
        public int Year { get; set; }
        public string InsuranceClass { get; set; }
        public int PackageId { get; set; }
        public string InsurerName { get; set; }
        public decimal SumInsure { get; set; }
        public decimal Premium { get; set; }
        public decimal Total { get; set; }

        public string Status { get; set; } = "ordered";   // ordered (pipeline ขยายภายหลัง)
        public string EmailStatus { get; set; }            // sent | failed
        public DateTime? EmailSentAt { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
```

- [ ] **Step 6: เพิ่ม DbSet + config ใน `app/api/Data/InsureChatDbContext.cs`.** เพิ่ม DbSet 4 ตัว (หลัง `AppSettings`):
```csharp
        public DbSet<ConversationRow> Conversations => Set<ConversationRow>();
        public DbSet<ConversationTurnRow> ConversationTurns => Set<ConversationTurnRow>();
        public DbSet<ConversationNeedRow> ConversationNeeds => Set<ConversationNeedRow>();
        public DbSet<OrderRow> Orders => Set<OrderRow>();
```
แล้วเพิ่ม config ใน `OnModelCreating` (ต่อท้าย หลัง block ของ `AppSettingsRow`) — **provider-agnostic** (ห้าม column type เฉพาะ SqlServer; `HasPrecision` ใช้ได้):
```csharp
            b.Entity<ConversationRow>(e =>
            {
                e.ToTable("Conversations");
                e.HasKey(x => x.Id);
                e.Property(x => x.SessionKey).HasMaxLength(40).IsRequired();
                e.HasIndex(x => x.SessionKey).IsUnique();
                e.Property(x => x.ProductType).HasMaxLength(16);
                e.Property(x => x.CarBrand).HasMaxLength(64);
                e.Property(x => x.CarModel).HasMaxLength(64);
                e.Property(x => x.InsuranceClass).HasMaxLength(8);
                e.Property(x => x.SumInsure).HasPrecision(18, 2);
                e.Property(x => x.CurrentStep).HasMaxLength(32);
                e.Property(x => x.ProspectName).HasMaxLength(128);
                e.Property(x => x.ProspectPhone).HasMaxLength(32);
                e.Property(x => x.ProspectEmail).HasMaxLength(256);
                e.Property(x => x.ProspectIdCard).HasMaxLength(32);
                e.Property(x => x.ProspectLicensePlate).HasMaxLength(32);
                e.Property(x => x.OutcomeStatus).HasMaxLength(16);
                e.HasMany(x => x.Turns).WithOne(t => t.Conversation)
                    .HasForeignKey(t => t.ConversationId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Needs).WithOne(n => n.Conversation)
                    .HasForeignKey(n => n.ConversationId).OnDelete(DeleteBehavior.Cascade);
            });
            b.Entity<ConversationTurnRow>(e =>
            {
                e.ToTable("ConversationTurns");
                e.HasKey(x => x.Id);
                e.Property(x => x.Role).HasMaxLength(16);
                e.HasIndex(x => new { x.ConversationId, x.Seq });
            });
            b.Entity<ConversationNeedRow>(e =>
            {
                e.ToTable("ConversationNeeds");
                e.HasKey(x => x.Id);
                e.Property(x => x.Need).HasMaxLength(256);
            });
            b.Entity<OrderRow>(e =>
            {
                e.ToTable("Orders");
                e.HasKey(x => x.Id);
                e.Property(x => x.OrderNo).HasMaxLength(32).IsRequired();
                e.HasIndex(x => x.OrderNo).IsUnique();
                e.Property(x => x.CustName).HasMaxLength(128);
                e.Property(x => x.CustPhone).HasMaxLength(32);
                e.Property(x => x.CustEmail).HasMaxLength(256);
                e.Property(x => x.CustIdCard).HasMaxLength(32);
                e.Property(x => x.LicensePlate).HasMaxLength(32);
                e.Property(x => x.Brand).HasMaxLength(64);
                e.Property(x => x.Model).HasMaxLength(64);
                e.Property(x => x.InsuranceClass).HasMaxLength(8);
                e.Property(x => x.InsurerName).HasMaxLength(128);
                e.Property(x => x.Status).HasMaxLength(16);
                e.Property(x => x.EmailStatus).HasMaxLength(16);
                e.Property(x => x.SumInsure).HasPrecision(18, 2);
                e.Property(x => x.Premium).HasPrecision(18, 2);
                e.Property(x => x.Total).HasPrecision(18, 2);
            });
```

- [ ] **Step 7: build** — `dotnet build app/api/InsureChatBot.Api.csproj` → "Build succeeded".

- [ ] **Step 8: commit**
```
git add app/api/Models/Chatbot/ChatbotModels.cs app/api/Data
git commit -m "feat: add Conversation/Order entities + SessionId on context"
```

---

## Task 2: migration Phase2 + apply

**Files:** Create `app/api/Migrations/*` (generated)

- [ ] **Step 1: generate** — `dotnet ef migrations add Phase2_ConversationsAndOrders --project app/api/InsureChatBot.Api.csproj --output-dir Migrations`
- [ ] **Step 2: ตรวจ `Up()`** ของไฟล์ `*_Phase2_ConversationsAndOrders.cs` — ต้อง `CreateTable` ครบ `Conversations` (unique index `SessionKey`), `ConversationTurns` (FK cascade + index ConversationId,Seq), `ConversationNeeds` (FK cascade), `Orders` (unique index `OrderNo`). ถ้าผิด/ขาด → STOP รายงาน (อย่าแก้ไฟล์ entity เอง)
- [ ] **Step 3: commit** `git add app/api/Migrations` → `git commit -m "feat: Phase2 migration (Conversations, Orders)"`
- [ ] **Step 4: apply** — `dotnet ef database update --project app/api/InsureChatBot.Api.csproj` → "Done." (ถ้าต่อ DB ไม่ได้: `dotnet ef migrations script --idempotent -o app/api/Migrations/phase2.sql ...`, ไม่ commit `phase2.sql`, รายงาน BLOCKED ส่วน apply)
- [ ] **Step 5: verify** — `sqlcmd -S 210.1.60.34,14333 -U clicktodesign -P "c1!ckt0" -d InsureChat -Q "SELECT name FROM sys.tables ORDER BY name"` → เห็น `Conversations`, `ConversationTurns`, `ConversationNeeds`, `Orders` เพิ่มจากของเดิม

---

## Task 3: IConversationStore + EfConversationStore (TDD)

**Files:** Create `app/api/Services/Conversation/IConversationStore.cs`, `app/api/Services/Conversation/EfConversationStore.cs`, `app/api.Tests/EfConversationStoreTests.cs`

> หมายเหตุ: namespace `InsureChatBot.Api.Services.Conversation` มีอยู่แล้ว (engines อยู่ที่นั่น) — วางไฟล์ใหม่ในโฟลเดอร์เดียวกัน

- [ ] **Step 1: เขียน test `app/api.Tests/EfConversationStoreTests.cs`**
```csharp
using InsureChatBot.Api.Models.Chatbot;
using InsureChatBot.Api.Services.Conversation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class EfConversationStoreTests
    {
        private static ChatbotConversationContext Ctx(string sid) => new()
        {
            SessionId = sid,
            ProductType = "motor", CarBrand = "HONDA", CarModel = "CIVIC", CarYear = 2020,
            CurrentStep = "llm",
            DiscoveredNeeds = new() { "กลัวน้ำท่วม" },
            ProspectName = "สมชาย", ProspectPhone = "0812345678",
            Turns = new()
            {
                new ChatbotTurn { Role = "assistant", Text = "สวัสดีค่ะ", Timestamp = DateTime.UtcNow },
                new ChatbotTurn { Role = "user", Text = "ฮอนดา ซีวิค", Timestamp = DateTime.UtcNow }
            }
        };

        [Fact]
        public void Upsert_creates_conversation_with_turns_and_needs()
        {
            using var f = new TestDbContextFactory();
            var store = new EfConversationStore(f);

            store.Upsert(Ctx("sess-1"));

            using var db = f.CreateDbContext();
            var conv = db.Conversations.Include(c => c.Turns).Include(c => c.Needs)
                        .Single(c => c.SessionKey == "sess-1");
            Assert.Equal("HONDA", conv.CarBrand);
            Assert.Equal("open", conv.OutcomeStatus);
            Assert.Equal(2, conv.Turns.Count);
            Assert.Single(conv.Needs);
            Assert.Equal(0, conv.Turns.OrderBy(t => t.Seq).First().Seq);
        }

        [Fact]
        public void Upsert_same_session_updates_and_replaces_turns_no_duplicate()
        {
            using var f = new TestDbContextFactory();
            var store = new EfConversationStore(f);

            store.Upsert(Ctx("sess-1"));
            var ctx2 = Ctx("sess-1");
            ctx2.Turns.Add(new ChatbotTurn { Role = "assistant", Text = "ทุนเท่าไหร่คะ", Timestamp = DateTime.UtcNow });
            ctx2.CarModel = "CITY";
            store.Upsert(ctx2);

            using var db = f.CreateDbContext();
            Assert.Equal(1, db.Conversations.Count(c => c.SessionKey == "sess-1")); // ไม่ duplicate
            var conv = db.Conversations.Include(c => c.Turns).Single(c => c.SessionKey == "sess-1");
            Assert.Equal("CITY", conv.CarModel);
            Assert.Equal(3, conv.Turns.Count);                 // turns ถูกแทนที่ด้วยชุดใหม่
            Assert.Equal(3, db.ConversationTurns.Count());     // ไม่มี orphan turn ค้าง
        }

        [Fact]
        public void MarkOrdered_sets_outcome_status()
        {
            using var f = new TestDbContextFactory();
            var store = new EfConversationStore(f);
            store.Upsert(Ctx("sess-1"));

            store.MarkOrdered("sess-1");

            using var db = f.CreateDbContext();
            Assert.Equal("ordered", db.Conversations.Single(c => c.SessionKey == "sess-1").OutcomeStatus);
        }

        [Fact]
        public void FindConversationId_returns_id_or_null()
        {
            using var f = new TestDbContextFactory();
            var store = new EfConversationStore(f);
            store.Upsert(Ctx("sess-1"));

            Assert.NotNull(store.FindConversationId("sess-1"));
            Assert.Null(store.FindConversationId("nope"));
        }
    }
}
```

- [ ] **Step 2: รัน test → fail** (`EfConversationStore` ไม่มี): `dotnet test app/api.Tests/InsureChatBot.Api.Tests.csproj`

- [ ] **Step 3: สร้าง `app/api/Services/Conversation/IConversationStore.cs`**
```csharp
using InsureChatBot.Api.Models.Chatbot;

namespace InsureChatBot.Api.Services.Conversation
{
    /// <summary>เก็บบทสนทนาลง DB (ADR 0016 Phase 2). candidate set/proposals ไม่ persist.</summary>
    public interface IConversationStore
    {
        /// <summary>upsert conversation ตาม SessionId — แทนที่ turns/needs ด้วยชุดล่าสุดจาก context</summary>
        void Upsert(ChatbotConversationContext context);

        /// <summary>set OutcomeStatus = "ordered" (เรียกหลังคำสั่งซื้อสำเร็จ)</summary>
        void MarkOrdered(string sessionId);

        /// <summary>หา Id ของ conversation จาก SessionId (null ถ้าไม่พบ)</summary>
        long? FindConversationId(string sessionId);
    }
}
```

- [ ] **Step 4: สร้าง `app/api/Services/Conversation/EfConversationStore.cs`**
```csharp
using InsureChatBot.Api.Data;
using InsureChatBot.Api.Data.Entities;
using InsureChatBot.Api.Models.Chatbot;
using Microsoft.EntityFrameworkCore;

namespace InsureChatBot.Api.Services.Conversation
{
    /// <summary>IConversationStore บน EF Core (Singleton + IDbContextFactory) — ADR 0016 Phase 2.</summary>
    public class EfConversationStore : IConversationStore
    {
        private readonly IDbContextFactory<InsureChatDbContext> _factory;
        public EfConversationStore(IDbContextFactory<InsureChatDbContext> factory) => _factory = factory;

        public void Upsert(ChatbotConversationContext c)
        {
            if (c == null || string.IsNullOrWhiteSpace(c.SessionId)) return;
            using var db = _factory.CreateDbContext();
            var now = DateTime.UtcNow;

            var conv = db.Conversations
                .Include(x => x.Turns).Include(x => x.Needs)
                .FirstOrDefault(x => x.SessionKey == c.SessionId);
            if (conv == null)
            {
                conv = new ConversationRow { SessionKey = c.SessionId, OutcomeStatus = "open", CreatedAt = now };
                db.Conversations.Add(conv);
            }

            conv.ProductType = c.ProductType;
            conv.CarBrand = c.CarBrand;
            conv.CarModel = c.CarModel;
            conv.CarYear = c.CarYear;
            conv.InsuranceClass = c.InsuranceClass;
            conv.SumInsure = c.SumInsure;
            conv.CurrentStep = c.CurrentStep;
            conv.SpinReason = c.SpinReason;
            conv.ProspectName = c.ProspectName;
            conv.ProspectPhone = c.ProspectPhone;
            conv.ProspectEmail = c.ProspectEmail;
            conv.ProspectIdCard = c.ProspectIdCard;
            conv.ProspectLicensePlate = c.ProspectLicensePlate;
            conv.LastUpdatedAt = now;

            // แทนที่ turns/needs ด้วยชุดล่าสุด (context พก history เต็มทุกเทิร์น)
            if (conv.Turns.Count > 0) db.ConversationTurns.RemoveRange(conv.Turns);
            if (conv.Needs.Count > 0) db.ConversationNeeds.RemoveRange(conv.Needs);
            conv.Turns = (c.Turns ?? new()).Select((t, i) => new ConversationTurnRow
            {
                Seq = i, Role = t.Role, Text = t.Text,
                Timestamp = t.Timestamp == default ? now : t.Timestamp
            }).ToList();
            conv.Needs = (c.DiscoveredNeeds ?? new()).Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => new ConversationNeedRow { Need = n }).ToList();

            db.SaveChanges();
        }

        public void MarkOrdered(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return;
            using var db = _factory.CreateDbContext();
            var conv = db.Conversations.FirstOrDefault(x => x.SessionKey == sessionId);
            if (conv == null) return;
            conv.OutcomeStatus = "ordered";
            conv.LastUpdatedAt = DateTime.UtcNow;
            db.SaveChanges();
        }

        public long? FindConversationId(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return null;
            using var db = _factory.CreateDbContext();
            return db.Conversations.Where(x => x.SessionKey == sessionId)
                     .Select(x => (long?)x.Id).FirstOrDefault();
        }
    }
}
```

- [ ] **Step 5: รัน test → pass** (`dotnet test ...` — ทุก test ผ่าน รวม Phase 1 8 ตัว + ใหม่ 4 = 12)
- [ ] **Step 6: commit** `git add app/api/Services/Conversation/IConversationStore.cs app/api/Services/Conversation/EfConversationStore.cs app/api.Tests/EfConversationStoreTests.cs` → `git commit -m "feat: EfConversationStore persists conversation+turns+needs"`

---

## Task 4: IOrderStore + EfOrderStore (TDD)

**Files:** Create `app/api/Services/Orders/IOrderStore.cs`, `EfOrderStore.cs`, `app/api.Tests/EfOrderStoreTests.cs`

- [ ] **Step 1: เขียน test `app/api.Tests/EfOrderStoreTests.cs`**
```csharp
using InsureChatBot.Api.Models.Chatbot;
using InsureChatBot.Api.Services.Conversation;
using InsureChatBot.Api.Services.Orders;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class EfOrderStoreTests
    {
        private static ChatbotProposalSummary Pkg() => new()
        {
            PackageId = 123, Brand = "HONDA", Model = "CIVIC", Year = 2020,
            InsuranceClass = "1", SumInsure = 500000m, InsurerName = "วิริยะ",
            Premium = 14614m, Total = 15640m
        };

        [Fact]
        public void CreateOrder_inserts_with_formatted_orderno_and_links_conversation()
        {
            using var f = new TestDbContextFactory();
            new EfConversationStore(f).Upsert(new ChatbotConversationContext
            {
                SessionId = "sess-1", ProspectName = "สมชาย", ProspectPhone = "0812345678"
            });
            var store = new EfOrderStore(f);

            var ctx = new ChatbotConversationContext
            {
                SessionId = "sess-1", ProspectName = "สมชาย", ProspectPhone = "081-234-5678",
                ProspectEmail = "a@b.c"
            };
            var orderNo = store.CreateOrder(ctx, Pkg(), "sent");

            Assert.StartsWith($"ORD-{DateTime.UtcNow:yyyy}-", orderNo);
            using var db = f.CreateDbContext();
            var o = db.Orders.Single();
            Assert.Equal(orderNo, o.OrderNo);
            Assert.Equal("0812345678", o.CustPhone);   // strip non-digit
            Assert.Equal(123, o.PackageId);
            Assert.Equal(15640m, o.Total);
            Assert.Equal("sent", o.EmailStatus);
            Assert.NotNull(o.ConversationId);           // ผูกกับ session
            Assert.Equal("ordered", db.Conversations.Single().OutcomeStatus);
        }

        [Fact]
        public void CreateOrder_without_session_has_null_conversation()
        {
            using var f = new TestDbContextFactory();
            var store = new EfOrderStore(f);

            var orderNo = store.CreateOrder(new ChatbotConversationContext { ProspectName = "x", ProspectPhone = "0800000000" }, Pkg(), "sent");

            using var db = f.CreateDbContext();
            Assert.Null(db.Orders.Single().ConversationId);
            Assert.False(string.IsNullOrWhiteSpace(orderNo));
        }
    }
}
```

- [ ] **Step 2: รัน test → fail** (`EfOrderStore` ไม่มี)

- [ ] **Step 3: สร้าง `app/api/Services/Orders/IOrderStore.cs`**
```csharp
using InsureChatBot.Api.Models.Chatbot;

namespace InsureChatBot.Api.Services.Orders
{
    /// <summary>บันทึกคำสั่งซื้อลง DB (ADR 0016 Phase 2) — snapshot ราคา ณ เวลาเสนอ (KPN เป็น live).</summary>
    public interface IOrderStore
    {
        /// <summary>insert Order จากแผนที่เลือก + ผูก conversation ถ้ามี; คืน OrderNo. set conversation = ordered.</summary>
        string CreateOrder(ChatbotConversationContext context, ChatbotProposalSummary selected, string emailStatus);
    }
}
```

- [ ] **Step 4: สร้าง `app/api/Services/Orders/EfOrderStore.cs`**
```csharp
using InsureChatBot.Api.Data;
using InsureChatBot.Api.Data.Entities;
using InsureChatBot.Api.Models.Chatbot;
using Microsoft.EntityFrameworkCore;

namespace InsureChatBot.Api.Services.Orders
{
    /// <summary>IOrderStore บน EF Core (Singleton + IDbContextFactory) — ADR 0016 Phase 2.</summary>
    public class EfOrderStore : IOrderStore
    {
        private readonly IDbContextFactory<InsureChatDbContext> _factory;
        public EfOrderStore(IDbContextFactory<InsureChatDbContext> factory) => _factory = factory;

        public string CreateOrder(ChatbotConversationContext ctx, ChatbotProposalSummary p, string emailStatus)
        {
            using var db = _factory.CreateDbContext();
            var now = DateTime.UtcNow;

            long? convId = null;
            if (!string.IsNullOrWhiteSpace(ctx?.SessionId))
                convId = db.Conversations.Where(c => c.SessionKey == ctx.SessionId)
                           .Select(c => (long?)c.Id).FirstOrDefault();

            var phone = new string((ctx?.ProspectPhone ?? "").Where(char.IsDigit).ToArray());
            var order = new OrderRow
            {
                OrderNo = "",
                ConversationId = convId,
                CustName = ctx?.ProspectName,
                CustPhone = phone,
                CustEmail = ctx?.ProspectEmail,
                CustIdCard = ctx?.ProspectIdCard,
                LicensePlate = ctx?.ProspectLicensePlate,
                Brand = p.Brand, Model = p.Model, Year = p.Year,
                InsuranceClass = p.InsuranceClass, PackageId = p.PackageId, InsurerName = p.InsurerName,
                SumInsure = p.SumInsure, Premium = p.Premium, Total = p.Total > 0 ? p.Total : p.Premium,
                Status = "ordered", EmailStatus = emailStatus,
                EmailSentAt = emailStatus == "sent" ? now : (DateTime?)null,
                CreatedAt = now, UpdatedAt = now
            };
            db.Orders.Add(order);
            db.SaveChanges();                         // ได้ Id

            order.OrderNo = $"ORD-{now:yyyy}-{order.Id:D6}";
            if (convId.HasValue)
            {
                var conv = db.Conversations.FirstOrDefault(c => c.Id == convId.Value);
                if (conv != null) { conv.OutcomeStatus = "ordered"; conv.LastUpdatedAt = now; }
            }
            db.SaveChanges();                         // เซฟ OrderNo + status
            return order.OrderNo;
        }
    }
}
```

- [ ] **Step 5: รัน test → pass** (รวม 14 tests)
- [ ] **Step 6: commit** `git add app/api/Services/Orders app/api.Tests/EfOrderStoreTests.cs` → `git commit -m "feat: EfOrderStore records orders with snapshot + order no"`

---

## Task 5: hook เข้า ChatbotService + DI

**Files:** Modify `app/api/Services/Implementations/ChatbotService.cs`, `app/api/Program.cs`

- [ ] **Step 1: register store ใน `Program.cs`.** หลังบรรทัด `builder.Services.AddSingleton<IAppSettingsStore, SqlAppSettingsStore>();` เพิ่ม:
```csharp
builder.Services.AddSingleton<InsureChatBot.Api.Services.Conversation.IConversationStore, InsureChatBot.Api.Services.Conversation.EfConversationStore>();
builder.Services.AddSingleton<InsureChatBot.Api.Services.Orders.IOrderStore, InsureChatBot.Api.Services.Orders.EfOrderStore>();
```

- [ ] **Step 2: แก้ `ChatbotService` constructor + fields.** เพิ่ม using ด้านบน:
```csharp
using InsureChatBot.Api.Services.Orders;
```
(`InsureChatBot.Api.Services.Conversation` มี using อยู่แล้ว). เพิ่ม 2 field:
```csharp
        private readonly IConversationStore _conversations;
        private readonly IOrderStore _orders;
```
แก้ constructor signature ให้รับเพิ่ม `IConversationStore conversations, IOrderStore orders` และ assign:
```csharp
        public ChatbotService(
            LlmConversationEngine llm,
            StateMachineConversationEngine stateMachine,
            IOrderEmailService orderEmail,
            IAppSettingsStore settings,
            IConversationStore conversations,
            IOrderStore orders,
            ILogger<ChatbotService> logger)
        {
            _llm = llm;
            _stateMachine = stateMachine;
            _orderEmail = orderEmail;
            _llmSettings = settings.GetEffectiveLlm();
            _conversations = conversations;
            _orders = orders;
            _logger = logger;
        }
```

- [ ] **Step 3: เพิ่ม helper + persist ใน ProcessMessageAsync.** เพิ่ม helper method ใน class:
```csharp
        /// <summary>ตั้ง SessionId (ถ้ายังไม่มี) บน context ขาออก แล้ว persist — ห่อ try/catch ไม่ให้กระทบแชต</summary>
        private void PersistConversation(ChatbotConversationContext incoming, ChatbotConversationContext updated)
        {
            if (updated == null) return;
            var sid = !string.IsNullOrWhiteSpace(updated.SessionId) ? updated.SessionId
                    : !string.IsNullOrWhiteSpace(incoming?.SessionId) ? incoming.SessionId
                    : Guid.NewGuid().ToString("N");
            updated.SessionId = sid;
            try { _conversations.Upsert(updated); }
            catch (Exception ex) { _logger.LogWarning(ex, "persist conversation ล้มเหลว (ไม่กระทบแชต)"); }
        }
```
ใน `ProcessMessageAsync` แก้ทั้ง 2 จุด return ให้ persist ก่อน return. แทนที่ body ของ method เป็น:
```csharp
            ChatbotMessageResponse response;
            if (_llmSettings.IsConfigured)
            {
                try { response = await _llm.ProcessMessageAsync(userCode, userName, userEmail, role, message, context); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LLM engine ล้มเหลว — fallback ไป state machine");
                    response = await _stateMachine.ProcessMessageAsync(userCode, userName, userEmail, role, message, context);
                }
            }
            else
            {
                response = await _stateMachine.ProcessMessageAsync(userCode, userName, userEmail, role, message, context);
            }
            PersistConversation(context, response.UpdatedContext);
            return response;
```

- [ ] **Step 4: persist ใน StreamMessageAsync.** แก้ method ให้ persist ตอนเจอ event `done`:
  - LLM path: เปลี่ยน loop เป็น
```csharp
            if (_llmSettings.IsConfigured)
            {
                await foreach (var ev in _llm.ProcessMessageStreamAsync(userCode, userName, userEmail, role, message, context, ct))
                {
                    if (ev.Type == "done" && ev.Context != null)
                        PersistConversation(context, ev.Context);   // ตั้ง SessionId ก่อน yield → client เก็บกลับ
                    yield return ev;
                }
                yield break;
            }
```
  - state-machine one-shot path: ก่อน `yield return ChatStreamEvent.Done(...)` ให้ persist:
```csharp
            var resp = await _stateMachine.ProcessMessageAsync(userCode, userName, userEmail, role, message, context);
            var text = (resp.Replies != null && resp.Replies.Count > 0) ? string.Join("\n\n", resp.Replies) : resp.Reply;
            if (!string.IsNullOrWhiteSpace(text)) yield return ChatStreamEvent.Delta(text);
            if (resp.Suggestions is { Count: > 0 }) yield return ChatStreamEvent.Suggest(resp.Suggestions);
            PersistConversation(context, resp.UpdatedContext);
            yield return ChatStreamEvent.Done(resp.UpdatedContext, resp.IsReadyToPropose);
```

- [ ] **Step 5: insert Order ใน SubmitProposalAsync.** หลังบล็อก `if (!sent.Success) return ...;` (ก่อน `return new ChatbotProposalResponse { Success = true, ... }`) แทรก:
```csharp
                // บันทึกคำสั่งซื้อลง DB + mark conversation = ordered (ADR 0016 Phase 2) — ไม่ให้พังถ้า DB ล่ม
                string orderNo = null;
                try { orderNo = _orders.CreateOrder(context, selected, "sent"); }
                catch (Exception ex) { _logger.LogWarning(ex, "บันทึก order ลง DB ล้มเหลว (อีเมลส่งแล้ว)"); }
```
แล้วแก้ return success ให้แนบ `TransactionId = orderNo`:
```csharp
                return new ChatbotProposalResponse
                {
                    Success = true,
                    TransactionId = orderNo,
                    Message = "ระบบจะส่งคำสั่งซื้อไปยังโบรคเกอร์ เพื่อให้เจ้าหน้าที่ติดต่อกลับ โปรดดำเนินการตามเงื่อนไขที่เจ้าหน้าที่แจ้ง เพื่อดำเนินการออกกรมธรรม์ต่อไป"
                };
```
> หมายเหตุ: `_orders.CreateOrder` set conversation = ordered อยู่แล้ว จึงไม่ต้องเรียก `MarkOrdered` ซ้ำ (`MarkOrdered` มีไว้เผื่อ flow อื่น)

- [ ] **Step 6: build** — `dotnet build app/api/InsureChatBot.Api.csproj` → "Build succeeded" (ตรวจ DI ครบ, constructor ใหม่ resolve ได้)

- [ ] **Step 7: รัน unit test ทั้งชุด** — `dotnet test app/api.Tests/InsureChatBot.Api.Tests.csproj` → 14/14 ผ่าน (ไม่มี test ของ ChatbotService — คุมด้วย smoke Task 6)

- [ ] **Step 8: commit** `git add app/api/Services/Implementations/ChatbotService.cs app/api/Program.cs` → `git commit -m "feat: persist conversation each turn + record order on submit"`

---

## Task 6: runtime smoke (chat persist + order persist)

**Files:** ไม่มี (runtime). DB ต้อง apply migration Phase2 แล้ว (Task 2)

- [ ] **Step 1: start app (background)** — `dotnet run --project app/api/InsureChatBot.Api.csproj --launch-profile http` (รอ ~12s, อ่าน PORT จาก log)

- [ ] **Step 2: chat persists.** ส่งข้อความ (public, ไม่ต้อง auth):
```bash
curl -s http://localhost:PORT/api/Chatbot/Message -H "Content-Type: application/json" -d '{"message":"สวัสดีครับ อยากได้ประกันรถ","conversationContext":{"productType":"motor","turns":[]}}'
```
Expected: HTTP 200, JSON มี `updatedContext.sessionId` (ค่าไม่ว่าง). เก็บค่า sessionId ไว้
ตรวจ DB: `sqlcmd -S 210.1.60.34,14333 -U clicktodesign -P "c1!ckt0" -d InsureChat -Q "SELECT TOP 1 SessionKey, CarBrand, OutcomeStatus FROM Conversations ORDER BY Id DESC; SELECT COUNT(*) AS Turns FROM ConversationTurns"`
Expected: มีแถว Conversations (SessionKey = ค่าที่ได้) + ConversationTurns > 0

- [ ] **Step 3: order persists.** ยิงคำสั่งซื้อ (ใส่ proposals + identity ครบ; ไม่มี SMTP_PASSWORD → อีเมล mock success):
```bash
curl -s http://localhost:PORT/api/Chatbot/Propose -H "Content-Type: application/json" -d '{"conversationContext":{"sessionId":"SMOKE-ORDER-1","prospectName":"ทดสอบ สมชาย","prospectPhone":"0812345678","proposals":[{"packageId":123,"brand":"HONDA","model":"CIVIC","year":2020,"insuranceClass":"1","sumInsure":500000,"insurerName":"วิริยะ","premium":14614,"total":15640}]}}'
```
Expected: HTTP 200, JSON `success:true` + `transactionId` ขึ้นต้น `ORD-`
ตรวจ DB: `sqlcmd ... -Q "SELECT TOP 1 OrderNo, CustName, PackageId, Total, EmailStatus FROM Orders ORDER BY Id DESC"`
Expected: แถว Order (OrderNo ตรงกับ transactionId, PackageId=123, Total=15640.00, EmailStatus=sent)

- [ ] **Step 4: stop app** (kill process — อย่าทิ้งค้าง)

- [ ] **Step 5:** ไม่มี commit (runtime). รายงานผล smoke ละเอียด

---

## Task 7: เอกสาร — mark Phase 2 done

**Files:** Modify `docs/adr/0016-insurechat-owns-sql-db-ef-core.md`

- [ ] **Step 1:** ในไฟล์ ADR 0016 ใต้หัวข้อ `## ขอบเขต` แก้บรรทัด Phase 2 จาก
`- **Phase 2:** ...`
เป็น
`- **Phase 2 (ทำแล้ว):** \`Conversations\` + \`ConversationTurns\` + \`ConversationNeeds\`, \`Orders\` — hook ใน \`ChatbotService\` (ProcessMessage/Stream upsert conversation; SubmitProposal insert order). \`SessionId\` ฝังใน context (round-trip ผ่าน Angular เดิม ไม่ต้องแก้ frontend). candidate set ไม่ persist.`
- [ ] **Step 2: commit** `git add docs/adr/0016-insurechat-owns-sql-db-ef-core.md` → `git commit -m "docs: mark ADR 0016 Phase 2 done"`

---

## Definition of Done (Phase 2)
- [ ] `dotnet build app/api` + `dotnet test app/api.Tests` ผ่าน (14 tests)
- [ ] ตาราง `Conversations`/`ConversationTurns`/`ConversationNeeds`/`Orders` เกิดจริงบน DB
- [ ] ส่งข้อความ → Conversations + ConversationTurns ถูกบันทึก, response มี `sessionId`
- [ ] คำสั่งซื้อ → Orders ถูกบันทึก (OrderNo = transactionId), conversation = ordered
- [ ] candidate set ไม่ถูก persist; DB ล่มไม่ทำแชตพัง (try/catch)
- [ ] ไม่แตะ Angular; ADR 0016 อัปเดต Phase 2 = ทำแล้ว

## นอก scope (Phase 3)
- KnowledgeArticles + importer; หน้า admin ดู conversation/CRM (/design-taste-frontend-v1)
- status pipeline เต็ม (contacted/won/lost), retention/anonymize PII job, บันทึก order ตอนอีเมล fail
