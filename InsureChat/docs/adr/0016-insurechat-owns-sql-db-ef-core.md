# 0016 — InsureChat เป็นเจ้าของ SQL DB (EF Core)

standalone app ย้ายจาก "JSON now" ([[docs/adr/0002]]) มาเป็นเจ้าของ DB `InsureChat` ของตัวเอง
ผ่าน **EF Core code-first + migrations** (connstring `EFDbConnection`). DB นี้แยกจาก `KPNNew`
(KPN, read-only Dapper, [[docs/adr/0004]]) — motor ยังอ่านผ่าน Dapper เหมือนเดิม (dual-stack).

## ขอบเขต
- **Phase 1 (ทำแล้ว):** `Users` (ย้ายจาก users.json), `AppSettings` (1 แถว, ย้ายจาก app-settings.json)
- **Phase 2 (ทำแล้ว):** `Conversations` + `ConversationTurns` + `ConversationNeeds`, `Orders` — hook ใน `ChatbotService` (ProcessMessage/Stream upsert conversation ทุกเทิร์น; SubmitProposal insert order + mark `ordered`). `SessionId` ฝังใน context (round-trip ผ่าน Angular `any` เดิม ไม่ต้องแก้ frontend). candidate set ไม่ persist. OrderNo = `ORD-{ค.ศ.}-{id:D6}` (ใช้ `now.Year` กัน พ.ศ. จาก culture ไทย). persist ห่อ try/catch ไม่ให้ DB ล่มทำแชตพัง
- **Phase 3 (backend ทำแล้ว):** `KnowledgeArticles` + importer (`KnowledgeSyncService`: file→DB sync, parse frontmatter + ContentHash, 6 ไฟล์ inject = `IsInjected`) + admin read APIs (`AdminDataController`: conversations/orders/knowledge ใต้ `[Authorize admin]`, `EfAdminQueryService`). ไฟล์ markdown ยังเป็น source of truth; chatbot ยัง inject จากไฟล์ (ไม่ re-wire). **frontend admin CRM/viewer = Part B** (design-taste-frontend-v1)

## ผลต่อ ADR เดิม
- **ปิดฉาก [[docs/adr/0002]]** — JSON→SQL เกิดขึ้นแล้ว ผ่าน seam `IUserStore`/`IAppSettingsStore` ที่ออกแบบไว้
  (สลับ impl ที่ DI; controller/prompt logic ไม่แตะ). `JsonUserStore`/`AppSettingsStore` คงไว้เผื่อ rollback
- **amend [[docs/adr/0011]] / [[docs/adr/0013]]** — secret (LLM API key, รหัสอีเมล) เก็บ **env เท่านั้น** ไม่ลง DB.
  admin **แก้ key/รหัสอีเมลผ่าน UI ไม่ได้อีก** (`SqlAppSettingsStore` strip secret ทิ้งเสมอ); `GetKeySource`/
  `GetEmailKeySource` คืน `env`/`none`. UI ช่องกรอก key เป็น no-op → cosmetic cleanup เลื่อนไป Phase 3 frontend
- **กลับ session-based MVP** (Phase 2) — บทสนทนาเก็บลง DB ทุก session; candidate set ไม่เก็บ (derive ใหม่)
- **เสริม [[docs/adr/0012]]** (Phase 2) — คำสั่งซื้อบันทึกลง `Orders` เพิ่มจากการยิงอีเมล (ไม่แทนที่)

## DI / Consequences
- ใช้ `AddDbContextFactory<InsureChatDbContext>` — store ที่เป็น **Singleton** (`SqlUserStore`,
  `SqlAppSettingsStore`) สร้าง `DbContext` สั้น ๆ ต่อ operation → กัน captive dependency กับ
  `SystemPromptProvider` (Singleton) ที่พึ่งพา `IAppSettingsStore`
- `DataInitializer` เติม admin + settings จาก JSON แบบ idempotent ตอน startup (ไม่ import secret)
- **Privacy:** `Conversations`/`Orders` (Phase 2) เก็บ PII (ชื่อ/เบอร์/บัตร) — อยู่ใน DB ภายในเท่านั้น
  (CLAUDE.md §12); แนะนำ retention job ภายหลัง
- **migration บน shared hosting:** ใช้ `dotnet ef migrations script --idempotent` รันบน DB โดยตรง (ไม่พึ่ง `dotnet ef` บนเซิร์ฟเวอร์)
- **หมายเหตุ:** remote SQL Server เจรจา TLS 1.0 (insecure) — backlog hardening ฝั่ง server
