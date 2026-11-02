# 0017 — แยก standalone app เป็น 2 git repo (api + web) เพื่อ handoff ให้ dev คนอื่น

เดิมโค้ด chatbot ทั้งหมด (`app/api`, `app/api.Tests`, `app/web`) อยู่ใน git repo เดียวกับ
**Obsidian vault / สมองที่สอง** (`wiki/`, `raw/`, `CONTEXT.md`, `docs/adr/`) ที่ root `InsureChat/`.
เมื่อต้องการ **ส่งงานให้ dev คนอื่นทำ** (แบ่ง backend/frontend) การให้ทั้ง repo ไปจะ
**ทำให้ PII ลูกค้าใน `wiki/clients/` หลุด** (ขัด CLAUDE.md §12) และ git history เดิม
**เคยมี secret หลุด** (`gpt-test` / `sk-secret-9999wxyz`, ดู [[CONTEXT.md]]). จึงแยกโค้ดออกเป็น
**git repo อิสระ 2 ตัว** (`insurechat-api`, `insurechat-web`) ส่วน vault เก็บเป็น repo ส่วนตัวต่อไป.
รายละเอียดเต็มดู [[2026-06-25-split-api-web-repos-design]].

## Considered Options

- **2 repo อิสระ api + web, vault private, sibling, fresh `git init` (เลือก)** — clone แยกสะอาด,
  ไม่มี nested repo, contractor ได้เฉพาะโค้ดที่ตัวเองทำ. fresh init = ไม่พา history ที่อาจมี
  secret/PII ไปด้วย. แลกกับการที่ api ต้องทำตัว **self-contained** (bundle ไฟล์ความรู้ที่เคยอ่านจาก `wiki/`).
- **monorepo (api+web) + vault repo** — ให้ทีมเดียวดูแลทั้ง product. *ไม่เลือก* เพราะต้องการ
  แบ่งงาน backend/frontend คนละคน และ repo เดียวรวม 2 stack ทำให้ขอบเขตงานไม่ชัด.
- **แยก `llm` เป็น repo/service ที่ 3** — *ไม่เลือก*: ขัดข้อจำกัด "build แค่ 2 ที่ (api, web)",
  LLM ผูกกับ domain service ของ api และคุยผ่าน interface ในกระบวนการเดียวอยู่แล้ว.
- **คงไว้ repo เดียว** — *ไม่เลือก*: ส่งงานให้คนอื่นไม่ได้โดยไม่เปิด vault/PII.

## Consequences

- **api ต้อง self-contained:** ลบ `FindWikiDir` ที่ไต่ parent หา `wiki/` (เขียนซ้ำ 2 ที่ใน
  `SystemPromptProvider` + `KnowledgeSyncService`) → อ่านจาก `Knowledge/` ที่ bundle ใน repo
  (config-driven path). `wiki/` ยังเป็น **source of truth**; `Knowledge/` = snapshot ที่ sync ทางเดียว vault→api.
- **Privacy gate:** ห้าม bundle `wiki/clients/` (PII), `raw/`, secret. fresh `git init` สำหรับ 2 repo
  ที่แชร์ → ไม่มี history ที่อาจรั่ว. vault repo เก็บ history เต็มแบบส่วนตัว.
- **.env decouple:** เลิก include `..\..\.env` ข้าม repo; api repo ถือ `.env` (ignored) + `.env.example` (commit) ของตัวเอง.
- **build/publish ไม่เปลี่ยนจำนวน:** ยัง = api (`dotnet publish`) + web (`ng build`) = 2 ที่ (ADR 0015 ยังใช้ได้).
- **web แยกง่าย:** คุย api ผ่าน HTTP/proxy (`:5228`) เท่านั้น ไม่มี shared compiled code.
- **ภาระใหม่:** ต้องดูแล knowledge sync (vault→api) ด้วยวินัย/สคริปต์; แต่ละ repo อาจต้องมี CI ของตัวเอง (อนาคต).
- **Phase 2 (หลัง split):** จัดระเบียบ `Services/Implementations` + `Services/Interfaces` → feature folder.
