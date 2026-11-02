---
title: แยก standalone app เป็น 2 git repo (api + web) เพื่อส่งงานให้ dev คนอื่น
type: design-spec
created: 2026-06-25
updated: 2026-06-25
status: approved
related: [docs/adr/0001, docs/adr/0015, docs/adr/0016, ADR 0017]
---

# Design Spec — แยก api / web เป็น git repo คนละตัว (handoff-ready)

## 1. บริบท & เป้าหมาย (Context & Goal)

ปัจจุบันโค้ด chatbot ทั้งหมดอยู่ในโฟลเดอร์เดียว `InsureChat/app/` (`api` + `api.Tests` + `web`)
ภายใต้ git repo เดียวที่ root `InsureChat/` ซึ่ง root นี้ทำหน้าที่ **2 อย่างพร้อมกัน**:

1. **Obsidian vault / สมองที่สอง** — `wiki/`, `raw/`, `templates/`, `tools/`, `CONTEXT.md`,
   `docs/adr/`, `CLAUDE.md` (ดู CLAUDE.md §3 สถาปัตยกรรม 3 ชั้น)
2. **โค้ดสินค้า (product)** — `app/api` (ASP.NET Core net10), `app/web` (Angular 21)

**เป้าหมายจริง** (จาก grilling): ผู้ใช้ต้องการ **ส่งงานให้ dev คนอื่นทำ** โดยแบ่งเป็น
backend dev / frontend dev → ต้องการ **2 git repo ที่ clone แยกกันได้สะอาด** และ
**ต้อง gate ไม่ให้ข้อมูลลูกค้า (PII ใน `wiki/clients/`) หลุดไปกับ repo ที่แชร์**

> ข้อจำกัดที่คงไว้: **build/publish ได้แค่ 2 ที่เหมือนเดิม = api, web** (ไม่เพิ่ม target ที่ 3)

## 2. Decisions (สรุปจาก grilling — ล็อกแล้ว)

| # | Decision | เหตุผล |
|---|----------|--------|
| D1 | **ไม่แยก `llm` เป็น peer/service** — LLM (`Services/Llm` + `Services/Conversation`) อยู่ในโฟลเดอร์ใน api ต่อไป | seam คุยผ่าน interface อยู่แล้ว แต่ผูกกับ domain service ของ api; แยกเป็น library/service จะเพิ่ม ceremony + ขัด "build 2 ที่" |
| D2 | **2 git repo ที่แชร์ = `api` + `web`** (vault ไม่เข้า repo ไหน) | แบ่งงาน backend/frontend; web คุย api ผ่าน HTTP/proxy เท่านั้น ไม่มี shared compiled code → แยกได้สะอาด |
| D3 | **Layout = Sibling** — 3 โฟลเดอร์ระดับเดียวกันใต้ `c:\CTD\ChatBot\` | git topology สะอาด ไม่มี nested repo; contractor clone repo เดียวจบ |
| D4 | **api self-contained** — bundle ไฟล์ความรู้ (ไม่มี PII) เข้า `api/.../Knowledge/` | api เดิมอ่าน 6 ไฟล์จาก `wiki/` ตอน runtime → ถ้าแยก repo จะหา wiki ไม่เจอ; ต้องพึ่งตัวเองได้ |
| D5 | **จัดระเบียบ `Services/` ภายใน = Phase 2** (หลัง split + verify) | ลด churn/ความเสี่ยงต่อ commit; แต่ละ phase verify ได้แยก |
| D6 | **`git init` ใหม่ (ไม่เอา history) สำหรับ api/web repo ที่แชร์** | history เดิมเคยมี secret หลุด (`sk-secret-9999wxyz`, CONTEXT.md) + อาจมีร่องรอย PII → fresh init = ปลอดภัยสุด; vault repo เก็บ history เต็มไว้ส่วนตัว |

## 3. โครงสร้างผลลัพธ์ (Target layout)

```
c:\CTD\ChatBot\
├── InsureChat/            ← vault (private) — .git เดิม + history เต็ม
│   ├── .obsidian/  wiki/  raw/  templates/  tools/
│   ├── CONTEXT.md  CLAUDE.md  docs/adr/
│   ├── docs/superpowers/specs/   (สเปกนี้)
│   └── (README/DEPLOYMENT/INTEGRATION/TEST guides — product docs, อยู่ที่นี่ได้)
│
├── insurechat-api/        ← git repo ใหม่ (fresh init) → backend dev | publish #1
│   ├── InsureChatBot.Api/         (จาก app/api/)
│   │   └── Knowledge/             ✨ ใหม่: ไฟล์ความรู้ที่ bundle (ไม่มี PII)
│   ├── InsureChatBot.Api.Tests/   (จาก app/api.Tests/)
│   ├── InsureChatBot.sln          ✨ ใหม่
│   ├── .env.example  .env(ignored)  .gitignore  README.md
│
└── insurechat-web/        ← git repo ใหม่ (fresh init) → frontend dev | build #2
    ├── (เนื้อหาจาก app/web/ ทั้งหมด)
    ├── .gitignore  README.md
```

หลัง refactor: vault (`InsureChat/`) ไม่มีโฟลเดอร์ `app/` แล้ว — Obsidian graph
(`wiki ↔ CONTEXT ↔ docs/adr`) **ไม่กระทบ** เพราะไฟล์เหล่านั้นอยู่ที่เดิมหมด

## 4. api repo (`insurechat-api`)

### 4.1 ทำให้ self-contained — bundle ความรู้ (D4) — **การเปลี่ยนโค้ดหลัก**

ปัญหาเดิม: `SystemPromptProvider` และ `KnowledgeSyncService` ต่างมีเมธอด `FindWikiDir(ContentRootPath)`
ที่ **ไต่ parent ขึ้นไปหาโฟลเดอร์ชื่อ `wiki`** (ลึก 4 ชั้น) — โลจิกนี้ **เขียนซ้ำ 2 ที่ (code smell)**
และจะพังเมื่อ api เป็น repo แยกที่ไม่มี vault อยู่ใกล้

**วิธีแก้:**
1. สร้างโฟลเดอร์ `InsureChatBot.Api/Knowledge/` คงโครง subfolder เดิม (`playbooks/`, `concepts/`)
   และ copy ไฟล์ความรู้ **ที่ไม่มี PII** เข้าไป:
   - **ตัดสินแล้ว = bundle non-PII corpus** (ครอบ 6 ไฟล์ prompt-injection ที่ locked ตาม ADR 0014 อยู่ในนี้):
     `wiki/concepts/`, `wiki/playbooks/`, `wiki/products/`, `wiki/sources/`, `wiki/overview.md`, `wiki/index.md`
     → keyword-search (`KnowledgeSyncService`) ครบเหมือนเดิม
     - 6 ไฟล์ prompt-injection ที่ `SystemPromptProvider` อ้างตรง (ต้องมีครบ):
       `playbooks/spin-selling-การขายแบบที่ปรึกษา.md`,
       `playbooks/รับมือข้อโต้แย้ง-laarc-feel-felt-found.md`,
       `playbooks/เทคนิคปิดการขาย.md`,
       `playbooks/เปรียบเทียบแผน-จับคู่-need.md`,
       `concepts/ประกันรถยนต์.md`,
       `concepts/คปภ-การกำกับและจรรยาบรรณตัวแทน.md`
   - **ห้าม bundle:** `wiki/clients/` (PII), `log.md` (บันทึกภายใน), `raw/` (heavy + user-owned)
2. เพิ่ม config `appsettings.json` → `"Knowledge": { "Dir": "Knowledge" }` (relative ต่อ ContentRoot)
3. รวมการ resolve path เป็น **จุดเดียว** (เช่น helper `KnowledgePaths.Resolve(env, config)`):
   - default = `Path.Combine(env.ContentRootPath, "Knowledge")`
   - override ได้ด้วย config/env (เผื่อ production ชี้ path อื่น)
   - **ลบ `FindWikiDir` ทั้ง 2 ก๊อปทิ้ง**
4. ตั้ง csproj ให้ก๊อป `Knowledge/**` เข้า output/publish (`CopyToOutputDirectory=PreserveNewest`)

> **Source of truth ยังเป็น `wiki/`** (ตาม CLAUDE.md) — `Knowledge/` ใน api repo คือ **snapshot ที่ publish แล้ว**
> ออกแบบ sync ทางเดียว vault→api: เพิ่มสคริปต์ใน vault (`tools/sync-knowledge.ps1` หรือสั่ง Claude)
> ที่ copy หน้า non-PII จาก `wiki/` → `insurechat-api/InsureChatBot.Api/Knowledge/` เวลา wiki เปลี่ยน

### 4.2 ความลับ / .env (decouple จาก vault)

- เดิม csproj include `..\..\.env` (จาก vault root) → **ลบทิ้ง** (ผูกข้าม repo)
- api repo ถือ `.env` ของตัวเอง:
  - `.env.example` (commit) — คีย์ placeholder: `OPENROUTER_API_KEY=`, `DB_CONNECTION=`, `SMTP_PASSWORD=` ฯลฯ
  - `.env` (gitignored) — secret จริงของ owner; production deploy ก็ใช้ไฟล์นี้
- contractor: `cp .env.example .env` แล้วใส่คีย์ dev ของตัวเอง
- `Config/DotEnv.cs` ไต่ parent หา `.env` (≤4 ชั้น) → ใช้ได้หลังย้าย ถ้า `.env` อยู่ที่ repo root

> ⚠️ **secret ที่ commit อยู่ตอนนี้ (ต้องดึงออกก่อน git init repo ใหม่):**
> - `appsettings.json` → `ConnectionStrings:EFDbConnection` มี **รหัส DB จริง** (`...Password=c1!ckt0`) +
>   `Jwt:Key` (dev key) → ย้ายเป็น env (`EF_DB_CONNECTION`, `JWT_KEY`), ตั้งค่าใน appsettings ที่ commit เป็น **""**
> - `appsettings.json` → `MotorTool:ScriptPath`/`WorkingDirectory` hardcode path ไป **vault** (`tools/motor/query.py`)
>   → ย้ายเป็น env (`MOTOR_SCRIPT_PATH`/`MOTOR_WORKING_DIR`), commit เป็น `""`. Python fallback search เป็น
>   **owner-only** (ต้องมี vault `tools/` + `raw/`); contractor ไม่มี → `MotorQueryService` ต้อง degrade เงียบ (คืน list ว่าง)
> - การ **scrub secret ต้องเสร็จก่อน `git init` + commit แรกของ api repo** (fresh init จึงจะสะอาดจริง)

### 4.3 โครงโปรเจกต์ + ไฟล์ใหม่

- ย้าย `app/api/*` → `insurechat-api/InsureChatBot.Api/*`
- ย้าย `app/api.Tests/*` → `insurechat-api/InsureChatBot.Api.Tests/*`
- แก้ ProjectReference ใน test csproj: `..\api\...` → `..\InsureChatBot.Api\InsureChatBot.Api.csproj`
- เพิ่ม `InsureChatBot.sln` (อ้าง 2 โปรเจกต์) — ให้ contractor เปิด VS/Rider ง่าย
- `.gitignore` ของ api repo: `bin/ obj/ .vs/ .env *.env.local Data/app-settings.json`
- `README.md`: prereq (.NET 10 SDK, SQL Server), `cp .env.example .env`, `dotnet run`,
  รันที่ `:5228`, วิธี migrate (EF Core), หมายเหตุว่า Knowledge sync มาจาก vault

### 4.4 จุดที่ build/publish ไม่เปลี่ยน

`dotnet publish InsureChatBot.Api` ยังเป็น backend artifact เดียว (out-of-process, ADR 0015) →
**publish เป้าหมายยังคง = 1 (api) + 1 (web) = 2** ตามข้อจำกัด

## 5. web repo (`insurechat-web`)

- ย้าย `app/web/*` → `insurechat-web/*` (เป็น repo root)
- `proxy.conf.json` ชี้ `localhost:5228` คงเดิม; `environment.ts` ใช้ relative path ผ่าน proxy
- `.gitignore`: `node_modules/ dist/ .angular/` (มี `app/web/.gitignore` เดิมใช้ต่อได้)
- `README.md`: `npm ci`, `npm start`, อธิบายว่า backend ต้องรันที่ `:5228` (หรือแก้ proxy)
- build = `ng build` เหมือนเดิม → artifact static + CORS (memory: deployment)

## 6. vault (`InsureChat/` — private) เปลี่ยนน้อย

- ลบโฟลเดอร์ `app/` (โค้ดย้ายออกไป repo ใหม่แล้ว)
- แก้ root `.gitignore`: ลบ path `app/api/...`, `app/web/...`, `app/.e2e/...`,
  `app/api/Data/app-settings.json` (ย้ายตามไป repo api แล้ว) — คง `raw/`, `.env`, secrets
- อัปเดต path ในเอกสาร vault ที่อ้าง `app/api`, `app/web`:
  - `CONTEXT.md` §ลิงก์เอกสาร (บรรทัด ~128-130)
  - `README.md`, `DEPLOYMENT_CHECKLIST.md`, `INTEGRATION_GUIDE.md`, `TEST_GUIDE.md` (ถ้ามี)
- Obsidian graph: ไม่กระทบ (wiki/CONTEXT/docs/adr อยู่ที่เดิม)

## 7. แผนทำงานแบบ phase

### Phase 1 — Repo split (สโคปหลักของ spec นี้)
1. สร้าง `insurechat-api/`; ย้าย api + api.Tests; เพิ่ม `Knowledge/` + refactor loader (§4.1); แก้ .env (§4.2); sln/gitignore/README
2. `dotnet build` + `dotnet test` เขียวบน clone สะอาด (ไม่มี vault) → `git init` + commit แรก
3. สร้าง `insurechat-web/`; ย้าย web; README/gitignore; `npm ci && ng build` เขียว → `git init` + commit แรก
4. ใน vault: ลบ `app/`, แก้ `.gitignore` + path ในเอกสาร, commit
5. เขียน ADR 0017 (decision record)

### Phase 2 — จัดระเบียบ `Services/` ใน api repo (แยก commit, หลัง Phase 1 verify)
ยุบ `Services/Implementations/` + `Services/Interfaces/` → feature folder ให้สอดคล้องของเดิม
(`Conversation/`, `Llm/`, `Email/`, …):
| ย้ายเข้า folder | ไฟล์ |
|---|---|
| `Services/Chatbot/` | `IChatbotService` + `ChatbotService` |
| `Services/Users/` | `IUserStore` + `JsonUserStore` + `SqlUserStore` |
| `Services/Auth/` | `IJwtTokenService` + `JwtTokenService` (+ ย้าย `Auth/PasswordHasher.cs` มารวม) |
| `Services/Motor/` (มีอยู่) | เพิ่ม `IMotorQueryService` + `MotorQueryService` |
| `Services/Proposal/` | `IProposalService` + `ProposalService` |
อัปเดต namespace + `using` + DI registration ใน `Program.cs` ตาม; `dotnet test` เขียว

## 8. เกณฑ์ verify (Definition of Done)

- [ ] **api:** clone สะอาด (ไม่มี vault) → `dotnet build` + `dotnet test` ผ่าน
- [ ] **api:** รันแล้ว chatbot ตอบได้ + knowledge injection ทำงานจาก `Knowledge/` (log ไม่ขึ้น "หาโฟลเดอร์ wiki ไม่เจอ")
- [ ] **web:** clone สะอาด → `npm ci && ng build` ผ่าน
- [ ] **ความลับ:** ไม่มี `.env`/`app-settings.json`/PII ใน 2 repo ที่แชร์ (fresh init → history สะอาด); สแกนยืนยัน
- [ ] **vault:** ไม่มี `app/`; Obsidian graph ปกติ; เอกสาร path อัปเดต
- [ ] **publish ยัง 2 ที่:** api (dotnet publish) + web (ng build)
- [ ] ADR 0017 สร้างแล้ว

## 9. ความเสี่ยง & ข้อควรระวัง

- **knowledge drift:** `Knowledge/` ใน api เป็น snapshot — ต้องมีวินัย sync จาก wiki (มีสคริปต์ช่วย)
- **ชุดไฟล์ bundle (ตัดสินแล้ว):** bundle non-PII corpus (§4.1) — keyword-search ครบ, ตัด `clients/` (PII) ออก
- **DotEnv path:** ต้องเช็คว่า `DotEnv.cs` โหลด `.env` ถูกตำแหน่งหลังย้าย
- **EF migrations/connection:** ตรวจว่า migration + connection string อ่านจาก env ใน repo ใหม่ได้
- **CI ในอนาคต:** แต่ละ repo อาจต้องมี workflow ของตัวเอง (นอกสโคป Phase 1)

## 10. นอกสโคป (Out of scope)

- แยก `llm` เป็น library/service (ตัดทิ้งตาม D1)
- เปลี่ยน deployment model (ยัง shared hosting out-of-process, ADR 0015)
- เปลี่ยน DB / schema (ADR 0016 ไม่แตะ)
- ตั้ง CI/CD pipeline ต่อ repo
- Phase 2 (จัดระเบียบ Services) — อยู่ใน spec แต่ทำหลัง Phase 1
