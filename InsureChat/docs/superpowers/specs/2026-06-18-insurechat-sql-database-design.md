---
title: InsureChat SQL Database (EF Core) — Design Spec
type: spec
created: 2026-06-18
status: approved-for-planning
related_adr: [0002, 0004, 0011, 0012, 0013, 0014, 0016]
---

# InsureChat SQL Database (EF Core) — Design Spec

## 1. บริบทและเป้าหมาย

ผู้ใช้สร้าง SQL Server database ชื่อ **`InsureChat`** ไว้แล้ว (ว่างเปล่า) — connection string `EFDbConnection`
ใน `app/api/appsettings.json`. ปัจจุบันแอป InsureChat ยัง **ไม่มีโค้ดไหนอ่าน connstring นี้เลย** และเก็บ
ข้อมูลแบบกระจาย (JSON ไฟล์ + ไม่เก็บบางอย่าง). spec นี้กำหนดการสร้าง schema และเดินสาย EF Core
เพื่อให้ DB `InsureChat` เป็นที่เก็บข้อมูลของแอป (ของเราเอง เขียนได้) แยกจาก DB ของ KPN (อ่านอย่างเดียว).

### สภาพปัจจุบัน (ที่สำรวจจากโค้ด)

| ข้อมูล | เก็บที่ไหนตอนนี้ | seam |
|--------|-----------------|------|
| บัญชี admin | `Data/users.json` ผ่าน `JsonUserStore : IUserStore` (Singleton) | มี `IUserStore` รอ `SqlUserStore` (ADR 0002) |
| Settings ที่ admin แก้ | `Data/app-settings.json` ผ่าน `AppSettingsStore : IAppSettingsStore` (Singleton, cache + lock) | มี `IAppSettingsStore` |
| บทสนทนา / session | **ไม่เก็บ** (session-based MVP) — context วิ่ง client-side ทุกเทิร์น | — |
| คำสั่งซื้อ / proposal | **ไม่เก็บ** — ยิงอีเมลหาโบรคเกอร์แล้วจบ (ADR 0012) | — |
| แพคเกจรถ 330k | DB **`KPNNew`** ของ KPN — read-only Dapper (`MotorFacetSqlService`, ADR 0004) | คงเดิม ไม่ย้าย |
| วิกิ | ไฟล์ markdown ใน `wiki/` (Claude เป็นเจ้าของ, CLAUDE.md §3) | คงเป็นไฟล์ |

### Stack จริงของโปรเจค (สำคัญ)
- `app/api` = ASP.NET Core net10, ใช้ **Dapper + Microsoft.Data.SqlClient** เท่านั้น — **ไม่มี EF Core**
  ในตอนนี้ (csproj มี Dapper 2.1.79, Microsoft.Data.SqlClient 7.0.1)
- DI: `JsonUserStore`, `AppSettingsStore` = **Singleton**; `SystemPromptProvider` (Singleton) inject
  `IAppSettingsStore`; `MotorFacetSqlService` = Scoped (Dapper)
- deploy = shared hosting, out-of-process (ADR 0015)

## 2. การตัดสินใจที่ยืนยันแล้ว (decisions)

| # | หัวข้อ | มติ | เหตุผล / ผลกระทบ |
|---|--------|-----|------------------|
| D1 | ขอบเขตตาราง | Auth/Users + Admin Settings + Conversations/Sessions + Orders/Leads + KnowledgeArticle (wiki sync) | ครอบหลังบ้าน + เก็บแชต + CRM + ค้นวิกิ |
| D2 | วิกิ | คงไฟล์ markdown เป็น **source of truth**; เพิ่มตาราง `KnowledgeArticle` sync ทางเดียว (file→DB) | ไม่ขัด CLAUDE.md §3 |
| D3 | Data access | **EF Core code-first + migrations** สำหรับ DB `InsureChat`; motor คง Dapper อ่าน KPN | ตรง ADR 0002 + ชื่อ connstring `EFDbConnection`; dual-stack |
| D4 | Secrets | **env ล้วน** — DB ไม่เก็บ secret (LLM API key, รหัสอีเมล) | ปลอดภัยสุด; **กลับบางส่วนของ ADR 0011/0013** (admin แก้ key ผ่าน UI ไม่ได้แล้ว) |
| D5 | เก็บแชต | เก็บ **ทุก session เต็มรูป** (transcript + identity) | candidate set ≤500 แถว **ไม่ persist** (derive ใหม่ได้, เลี่ยง bloat) |
| D6 | Leads | **ไม่มีตารางแยก** — lead = Conversation ที่มีเบอร์แต่ยังไม่มี Order (query เอา) | เลี่ยง duplicate; ทำ SQL view ภายหลังได้ |
| D7 | รูปตาราง Settings | **single-row typed table** (1 แถว Id=1) | typed, เข้ากับ `AppSettingsStore` ที่ strongly-typed |

## 3. สถาปัตยกรรม & การเดินสาย

```
InsureChatDbContext (EF Core, UseSqlServer)  ── connstring "EFDbConnection" ──> DB "InsureChat" (เขียนได้)
   ลงทะเบียนผ่าน AddDbContextFactory<InsureChatDbContext>
MotorFacetSqlService (Dapper, read-only)     ── env KPN_DB_CONNECTION       ──> DB "KPNNew" (KPN, อ่านอย่างเดียว)
```

### DI lifetime (ข้อควรระวังหลัก)
EF `DbContext` เป็น **Scoped** แต่ `JsonUserStore`/`AppSettingsStore` เป็น **Singleton** และมี Singleton
อื่น (`SystemPromptProvider`) พึ่งพา `IAppSettingsStore`. ถ้าทำ store ใหม่เป็น Scoped → DI จะพัง
(captive dependency / cannot resolve scoped from singleton).

**วิธีแก้:** ลงทะเบียน `builder.Services.AddDbContextFactory<InsureChatDbContext>(o => o.UseSqlServer(conn))`
แล้วให้ store ที่ยังเป็น **Singleton** inject `IDbContextFactory<InsureChatDbContext>` สร้าง `DbContext`
สั้น ๆ ต่อ operation (`using var db = _factory.CreateDbContext();`). store ที่ผูกกับ request (Conversation/Order)
จะเป็น Scoped หรือใช้ factory ก็ได้ — ใช้ factory ทั้งหมดเพื่อความสม่ำเสมอ.

### Seam เดิมไม่แตะ
`IUserStore`, `IAppSettingsStore` คงเดิม → สลับ impl ใน `Program.cs` เท่านั้น
(`JsonUserStore`→`SqlUserStore`, `AppSettingsStore`→`SqlAppSettingsStore`). controller/prompt logic ไม่แก้.

### แพ็กเกจที่เพิ่ม
`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`,
`Microsoft.EntityFrameworkCore.Tools` (Dapper + Microsoft.Data.SqlClient คงไว้สำหรับ motor).

## 4. Schema (ตารางทั้งหมด)

### ERD (ภาพรวมความสัมพันธ์)
```
Users                AppSettings (1 row)        KnowledgeArticles
 (standalone)         (standalone)               (standalone, sync จากไฟล์)

Conversations 1───* ConversationTurns
      │ 1
      │
      *
   Orders            (Order.ConversationId → Conversations.Id, nullable)
```

### 4.1 `Users` — ย้ายจาก users.json (Phase 1)
| คอลัมน์ | ชนิด SQL | ข้อกำหนด |
|---------|----------|----------|
| Id | int IDENTITY | PK |
| Username | nvarchar(64) | NOT NULL, UNIQUE index |
| PasswordHash | nvarchar(256) | NOT NULL — รูปเดิม `iterations.saltB64.hashB64` (PBKDF2-SHA256) |
| Role | nvarchar(16) | NOT NULL — `admin` (`agent`/`customer` legacy, เก็บได้แต่ไม่ออก token) |
| CustCode | nvarchar(64) | NULL |
| Name | nvarchar(128) | NULL |
| Email | nvarchar(256) | NULL |
| CreatedAt | datetime2 | NOT NULL default sysutcdatetime() |
| UpdatedAt | datetime2 | NOT NULL |

- `SqlUserStore : IUserStore` — `FindByUsername`, `TryAddUser` (คืน false ถ้า Username ซ้ำ — ใช้ unique index/catch)
- **seed admin** ตอน startup แบบ idempotent: ถ้าตาราง Users ว่าง → import จาก `users.json` เดิม (หรือ seed จาก config). ห้าม seed ซ้ำ.

### 4.2 `AppSettings` — ย้ายจาก app-settings.json (Phase 1, single-row)
| คอลัมน์ | ชนิด | หมายเหตุ |
|---------|------|----------|
| Id | int | PK, บังคับ = 1 (single row) |
| ThemeBrand | nvarchar(16) | default `#2563eb` |
| Persona / SalesApproach / Compliance / Style | nvarchar(max) NULL | 4 ช่อง prompt (ADR 0014); null = ใช้ default ในโค้ด |
| LlmEnabled | bit NULL | override (null = base) |
| LlmBaseUrl / LlmModel | nvarchar(256) NULL | override |
| LlmTemperature | float NULL | override |
| LlmMaxTokens / LlmTimeoutSeconds | int NULL | override |
| EmailUsername / EmailTo | nvarchar(256) NULL | override (อีเมลผู้ส่ง/ปลายทาง) |
| UpdatedAt | datetime2 | |

- **ไม่มีคอลัมน์ secret** (`LlmApiKey`, `EmailPassword` ตัดทิ้ง) — อ่านจาก env เท่านั้น (D4)
- `SqlAppSettingsStore : IAppSettingsStore` — คง interface เดิม (`Get`, `Update`, `GetEffectiveLlm`,
  `GetEffectiveEmail`, `GetKeySource`, `GetEmailKeySource`). `GetEffective*` merge env base (secret จาก env);
  `GetKeySource`/`GetEmailKeySource` คืน `env`/`none` เท่านั้น (ไม่มี `admin` อีกต่อไป)
- cache 1 แถวในหน่วยความจำ + reload หลัง Update (เหมือน pattern เดิม)

### 4.3 `Conversations` — เก็บทุก session (Phase 2)
| คอลัมน์ | ชนิด | หมายเหตุ |
|---------|------|----------|
| Id | bigint IDENTITY | PK |
| SessionKey | uniqueidentifier | UNIQUE — ให้ client อ้างอิงข้ามเทิร์น |
| ProductType | nvarchar(16) NULL | motor/property/pa/group |
| CarBrand / CarModel | nvarchar(64) NULL | |
| CarYear | int NULL | |
| InsuranceClass | nvarchar(8) NULL | 1/2+/3+/2/3 |
| SumInsure | decimal(18,2) NULL | |
| Province | nvarchar(64) NULL | เก็บค่า ไม่กรองแพคเกจ |
| SpinReason | nvarchar(max) NULL | |
| ProspectName / Phone / Email / IdCard / LicensePlate | nvarchar NULL | **PII** |
| OutcomeStatus | nvarchar(16) | open / proposed / ordered / abandoned |
| IsReadyToPropose | bit | |
| CreatedAt / LastUpdatedAt | datetime2 | |

### 4.4 `ConversationTurns` — transcript (Phase 2)
| คอลัมน์ | ชนิด | หมายเหตุ |
|---------|------|----------|
| Id | bigint IDENTITY | PK |
| ConversationId | bigint | FK → Conversations.Id, ON DELETE CASCADE, index |
| Seq | int | ลำดับในบทสนทนา |
| Role | nvarchar(16) | user / assistant |
| Text | nvarchar(max) | |
| Timestamp | datetime2 | |

### 4.5 `ConversationNeeds` — SPIN needs (Phase 2, child)
| คอลัมน์ | ชนิด | หมายเหตุ |
|---------|------|----------|
| Id | bigint IDENTITY | PK |
| ConversationId | bigint | FK → Conversations.Id, ON DELETE CASCADE |
| Need | nvarchar(256) | 1 ความต้องการที่ค้นพบ |

> **candidate set / shortlist ไม่ persist** — derive จาก KPN ใหม่ได้; เลี่ยง bloat (≤500 แถว/เทิร์น).

### 4.6 `Orders` — บันทึกคำสั่งซื้อ (Phase 2)
| คอลัมน์ | ชนิด | หมายเหตุ |
|---------|------|----------|
| Id | bigint IDENTITY | PK |
| OrderNo | nvarchar(32) | UNIQUE — เช่น `ORD-2026-000123` |
| ConversationId | bigint NULL | FK → Conversations.Id (อ้างอิงกลับ session) |
| CustName / Phone / Email / IdCard / LicensePlate | nvarchar | snapshot ลูกค้า (PII) |
| Brand / Model | nvarchar(64) | snapshot รถ |
| Year | int | |
| PackageId | int | id แพคเกจ KPN ที่เลือก |
| InsurerName / PolTypeName | nvarchar(128) | snapshot ผู้รับประกัน/ชั้น |
| SumInsure / Premium / Total | decimal(18,2) | **snapshot ราคา ณ เวลาเสนอ** (KPN เป็น live) |
| Status | nvarchar(16) | new → emailed → contacted → won/lost/follow-up (align CLAUDE.md §5.2) |
| EmailStatus | nvarchar(16) | mock / sent / failed |
| EmailSentAt | datetime2 NULL | |
| Notes | nvarchar(max) NULL | ตัวแทนจดเพิ่ม |
| CreatedAt / UpdatedAt | datetime2 | |

> สมมติฐาน: 1 order = 1 แพคเกจที่เลือก (Proposals[0]). ถ้าต้องรองรับหลายแพคเกจ/order → เพิ่ม child `OrderItems` ภายหลัง.

### 4.7 `KnowledgeArticles` — วิกิ sync (Phase 3)
| คอลัมน์ | ชนิด | หมายเหตุ |
|---------|------|----------|
| Id | bigint IDENTITY | PK |
| Slug | nvarchar(256) | UNIQUE — เช่น `concepts/ประกันรถยนต์` |
| Title | nvarchar(256) | |
| Type | nvarchar(32) | concept/playbook/source/product/... |
| Content | nvarchar(max) | markdown body |
| Tags / Aliases | nvarchar(max) NULL | csv |
| SourceFile | nvarchar(512) | path สัมพัทธ์ใน wiki/ |
| ContentHash | nvarchar(64) | ตรวจว่าเปลี่ยน (sync) |
| IsInjected | bit | เป็น 1 ใน knowledge ป้อน prompt ไหม |
| UpdatedAt / SyncedAt | datetime2 | |

- **importer** (file→DB ทางเดียว): อ่าน `wiki/*.md` → parse frontmatter → upsert ตาม Slug + เทียบ ContentHash.
  ทริกเกอร์: ปุ่ม admin หรือ startup task. ไฟล์ = source of truth เสมอ.

## 5. การเปลี่ยน pipeline (Phase 2)
ปัจจุบัน `ChatbotConversationContext` round-trip กับ client ทุกเทิร์น (ไม่แตะ server storage). การเก็บแชต
ต้องเพิ่ม hook ใน `ChatbotService` (และ path `/MessageStream`):
- ตอนเริ่ม session → สร้าง/หา `Conversations` ด้วย `SessionKey`
- ทุกเทิร์น → upsert ฟิลด์ context + append `ConversationTurns` (user + assistant) + sync `ConversationNeeds`
- ตอนกด "ส่งคำสั่งซื้อ" สำเร็จ → insert `Orders` + set `Conversation.OutcomeStatus = ordered` + เก็บ `EmailStatus`

⚠️ ต้องไม่ให้การเขียน DB ล้มแล้วทำแชตพัง — ครอบ try/catch + log (graceful degradation เหมือน guardrail ADR 0010).

## 6. Migrations / Seeding / Data migration
- `dotnet ef migrations add InitialCreate` → `dotnet ef database update` (หรือ generate SQL script สำหรับ shared hosting)
- **Seed admin** idempotent: ถ้า Users ว่าง → import `users.json`
- **one-time data migration**: `app-settings.json` → AppSettings row (1 ครั้ง). secret ใน json เดิม **ทิ้ง** (ไปอยู่ env)
- shared hosting: ใช้ `migrations script` รันบน DB โดยตรง หรือ apply ตอน startup (พิจารณา idempotency)

## 7. Phasing (แต่ละ phase = 1 implementation plan แยก)
1. **Phase 1 — EF foundation + JSON→SQL** *(เสี่ยงต่ำ, ของเดิมยังวิ่ง)*
   EF packages + `InsureChatDbContext` + `Users` + `AppSettings` + `SqlUserStore`/`SqlAppSettingsStore` +
   สลับ DI + migration + seed/data-migrate + ADR 0016 + CONTEXT.md
2. **Phase 2 — เก็บแชต + คำสั่งซื้อ**
   `Conversations`/`Turns`/`Needs` + `Orders` + hook เข้า `ChatbotService`/stream + ปุ่มสั่งซื้อ insert order
3. **Phase 3 — วิกิ sync + admin frontend**
   `KnowledgeArticles` + importer · **หน้า admin**: ดู conversation/transcript + CRM orders/leads
   → ใช้ `/design-taste-frontend-v1` สำหรับ UI ส่วนนี้

## 8. เอกสารที่ต้องอัปเดต (grill-with-docs)

### ADR 0016 (ใหม่) — ร่างเนื้อหา
> **0016 — InsureChat เป็นเจ้าของ SQL DB (EF Core)**
> standalone app ย้ายจาก "JSON now" (ADR 0002) มาเป็นเจ้าของ DB `InsureChat` ของตัวเอง ผ่าน EF Core
> code-first + migrations. DB นี้แยกจาก `KPNNew` (KPN, read-only Dapper, ADR 0004).
> **ขอบเขต:** Users, AppSettings, Conversations+Turns+Needs, Orders, KnowledgeArticles.
> **ผลต่อ ADR เดิม:**
> - ปิดฉาก **ADR 0002** (JSON→SQL เกิดขึ้นแล้ว ผ่าน seam `IUserStore`/`IAppSettingsStore` ที่ออกแบบไว้)
> - **amend ADR 0011/0013** — secret (LLM API key, รหัสอีเมล) เก็บ **env เท่านั้น** ไม่ลง DB →
>   admin **แก้ key/รหัสอีเมลผ่าน UI ไม่ได้อีก** (หน้า admin เหลือแสดงสถานะ env: ตั้งแล้ว/ยัง). `GetKeySource`
>   คืน `env`/`none` เท่านั้น
> - **กลับ session-based MVP** — บทสนทนาเก็บลง DB ทุก session (candidate set ไม่เก็บ)
> - **เสริม ADR 0012** — คำสั่งซื้อบันทึกลง `Orders` เพิ่มจากการยิงอีเมล (ไม่แทนที่อีเมล)
> **DI:** ใช้ `AddDbContextFactory` ให้ singleton store สร้าง context สั้น ๆ (กัน captive dependency กับ `SystemPromptProvider`).
> **Privacy:** Conversations/Orders เก็บ PII (ชื่อ/เบอร์/บัตร) — อยู่ใน DB ภายในเท่านั้น (CLAUDE.md §12); แนะนำ retention job ภายหลัง.

### CONTEXT.md — จุดแก้
- **Session**: จากเดิม "ยังไม่บันทึกลง DB — session-based MVP" → "เก็บลง DB ทุก session (`Conversations`+`ConversationTurns`); candidate set ไม่เก็บ (derive ใหม่)"
- **Pending/Proposal**: เพิ่มว่า order บันทึกลงตาราง `Orders` ด้วย (ADR 0016)
- **API key / App Password (write-only)**: แก้ว่า admin แก้ผ่าน UI ไม่ได้แล้ว — มาจาก env เท่านั้น (amend ADR 0011/0013 ผ่าน ADR 0016)
- เพิ่มศัพท์ใหม่: **Order record**, **Lead** (= conversation มีเบอร์ แต่ยังไม่มี order), **KnowledgeArticle** (วิกิ sync table)

## 9. ความเสี่ยง / ข้อควรระวัง
- **DI captive dependency** — ต้องใช้ `IDbContextFactory` (ดู §3)
- **PII + table growth** — anonymous traffic ทำให้ `Conversations`/`Turns` โตเร็ว; เลือกเก็บทุก session แล้ว
  → แนะนำ retention/anonymize job เป็น follow-up (ไม่อยู่ใน 3 phase นี้)
- **DB write ล้ม** ต้องไม่ทำแชตพัง (try/catch + log)
- **migration บน shared hosting** — เตรียม SQL script สำรอง (ไม่พึ่ง `dotnet ef` บนเซิร์ฟเวอร์)
- **snapshot ราคา** ใน Orders จำเป็น เพราะ KPN เป็น live (อย่าผูก FK ข้าม DB)

## 10. เกณฑ์สำเร็จ (Phase 1)
- [ ] แอป build ผ่าน + ต่อ DB `InsureChat` ได้ผ่าน `EFDbConnection`
- [ ] migration สร้างตาราง `Users`, `AppSettings` จริง
- [ ] login admin เดิมยังทำงาน (อ่านจาก SQL ไม่ใช่ json)
- [ ] admin แก้ theme/prompt/LLM(non-secret)/email(non-secret) แล้ว persist ลง SQL
- [ ] secret ยังมาจาก env (key source = env/none)
- [ ] ADR 0016 + CONTEXT.md อัปเดต
