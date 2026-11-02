---
type: log
title: บันทึกตามลำดับเวลา
aliases: [log, ไทม์ไลน์]
tags: [log]
created: 2026-06-01
---

# 🕒 Log (append-only)

> รูปแบบ: `## [YYYY-MM-DD] <action> | <หัวข้อ>` — ดึง 5 รายการล่าสุด: `grep "^## \[" wiki/log.md | tail -5`

## [2026-06-01] setup | ตั้งค่าโครงสร้างวิกิ + สคีมา
- สร้าง `CLAUDE.md` (สคีมา), `SKILL.md` (insure-wiki), โครงสร้างโฟลเดอร์ raw/wiki/templates
- เทมเพลต: source, client, product

## [2026-06-01] ingest | ความรู้การขายตั้งต้นจากงานวิจัยเว็บ (S-001..S-005)
- สร้าง source: S-001 (objection/closing TH), S-002 (SPIN), S-003 (objection 2025 EN), S-004 (ประเภทประกัน), S-005 (คปภ.)
- สร้างหน้าความรู้: [[ประเภทประกันชีวิต]], [[คปภ-การกำกับและจรรยาบรรณตัวแทน]]
- สร้าง playbooks: [[spin-selling-การขายแบบที่ปรึกษา]], [[รับมือข้อโต้แย้ง-laarc-feel-felt-found]], [[เทคนิคปิดการขาย]]
- อัปเดต [[index]], [[overview]]

## [2026-06-01] ingest | โบรชัวร์ตลอดชีพ มั่นคง 90/20 (S-006) — สาธิต ingest ครั้งแรก
- แหล่ง: raw/โบรชัวร์-ตลอดชีพมั่นคง90-20-ตัวอย่าง.md → สร้าง source [[S-006-brochure-ตลอดชีพมั่นคง|S-006]]
- สร้างหน้า product ใหม่: [[ตลอดชีพมั่นคง-90-20]]
- เชื่อม cross-link เข้า [[ประเภทประกันชีวิต]] (เพิ่มหัวข้อ "ผลิตภัณฑ์จริงในวิกิ")
- ตรวจความขัดแย้ง: ไม่พบ
- อัปเดต [[index]] (เพิ่มแถว product + S-006)
- แตะทั้งหมด 4 หน้า: S-006, product, ประเภทประกันชีวิต, index

## [2026-06-01] ingest | ปรับวิกิเข้าสายวินาศภัย/ประกันกลุ่ม (S-007, S-008)
- ผู้ใช้ระบุ: ขาย **วินาศภัย/ประกันกลุ่ม** (รถ บ้าน PA กลุ่ม) ลูกค้าทุกกลุ่ม เน้น **ความรู้ผลิตภัณฑ์+เปรียบเทียบ**
- สร้าง source: [[S-007-ประกันรถยนต์-ชั้น|S-007]], [[S-008-ประกันกลุ่ม-PA|S-008]]
- สร้าง concept: [[ประเภทประกันวินาศภัย]], [[ประกันรถยนต์]], [[ประกันอัคคีภัย-และที่อยู่อาศัย]], [[ประกันอุบัติเหตุส่วนบุคคล-PA]], [[ประกันกลุ่ม-สวัสดิการพนักงาน]]
- สร้าง playbook: [[เปรียบเทียบแผน-จับคู่-need]] (⭐ จุดเน้น) + เทมเพลต templates/comparison.md
- reframe [[overview]] + [[index]] ให้วินาศภัยเป็นสายหลัก ประกันชีวิตเป็นความรู้ประกอบ
- หมายเหตุ: ตัวเลขวงเงิน พ.ร.บ. และเบี้ยจริง ต้องเติมจากโบรชัวร์บริษัทที่ผู้ใช้ขาย (ยังเป็นความรู้ทั่วไป)

## [2026-06-01] ingest | T Broker portal + ชุดข้อมูลแพคเกจรถ 330,408 รายการ (S-009, S-010)
- raw: `หน้าแรก - T Broker.md` + `ข้อมูลแพคเกจรถยนต์_Page1-10.json` (~330 MB)
- สแกนจริงทั้ง 330,408 รายการ: 4 บริษัท (ธนชาต 294k, โตเกียวมารีน 16k, เมืองไทย 16k, วิริยะ 5k), ชั้น 1/2+/3+/3, 39 ยี่ห้อ, ปี 1994–2026
- สร้างเครื่องมือ `tools/motor/`: motorlib.py, query.py (ค้นกรอง), build_catalog.py (gen หน้าคลัง), explore.py
- สร้าง source [[S-009-T-Broker-portal|S-009]], [[S-010-dataset-แพคเกจรถยนต์|S-010]]
- สร้าง entity [[T-Broker-โบรกเกอร์]] + คลัง (auto-gen) [[แพคเกจรถยนต์-T-Broker]]
- เชื่อมเข้า [[ประกันรถยนต์]], อัปเดต [[index]], [[overview]]
- หลักการ: JSON ดิบ = source of truth (ไม่ duplicate), วิกิเก็บแผนที่+สรุป+เครื่องมือค้น → ข้อมูลครบ ไม่ตก
- ⚠️ เบี้ยเป็น package rate ณ วันที่ ต้องยืนยันในระบบ T Broker ก่อนเสนอจริง

## [2026-06-02] setup | Chatbot web-embedded architecture + implementation (grill-with-docs Q1–Q10)
- **Design finalized via grill-with-docs (10 questions):**
  - Conversation flow: Product-first (brand→model→year→class→sum) + SPIN deep questioning
  - Knowledge retrieval: Wiki keyword search (simple, no embeddings)
  - Session model: MVP (session-only, no persistent DB yet), ready for Google OAuth future
  - Proposal API: POST `motor/check-insure-premiums2-form/add` with JSON schema
  - User info: From JWT auth (no need to ask name/email/phone)
- **Created CONTEXT.md:** Glossary for chatbot domain (Session, Product, Flow, Knowledge, Proposal, Compliance)
- **Backend (C:\CTD\KPN\KPN_Backend):**
  - ChatbotController.cs (3 endpoints: /Message, /Propose, /History)
  - ChatbotService.cs (IChatbotService) + state machine (ask_product→ask_brand→...→spin_deep→propose)
  - ChatbotModels.cs (ChatbotMessageRequest/Response, ConversationContext, ProposalRequest/Response)
  - Startup.cs: registered ChatbotService + HttpClientFactory in DI
- **Frontend (C:\CTD\KPN\KPN_Frontend, Angular, not React):**
  - ChatbotComponent.ts/html/css ใน `/views/modules/chatbot/`
  - ChatbotModule.ts (for import)
  - UI: message bubbles (user/assistant), typing indicator, send button, proposal action
- **Skeleton complete.** TODO: integrate motor/query.py via HTTP, integrate T Broker proposal submission

## [2026-06-02] integration | Motor query service + subprocess execution
- **MotorQueryService.cs** (IMotorQueryService interface):
  - Calls `tools/motor/query.py` via subprocess (Python → JSON output)
  - Parses JSON response → List<MotorPackageDto>
  - Filters by brand, model, year, class, sum-insure
  - Returns top N packages with insurer+premium
- **ChatbotService integration:**
  - Injected IMotorQueryService
  - HandleSpinDeepAsync now async, calls motor query when ready_to_propose
  - Builds proposal list (top 3 insurers) → shows in chat
  - Updates context.Proposals for proposal submission
- **Startup.cs:** Registered IMotorQueryService in DI
- **Backend:** ✅ Compiled successfully (no errors)
- **Status:** Motor query flow ready; chatbot now searches 330k packages at SPIN deep step

## [2026-06-02] integration | T Broker proposal service + submission flow
- **ProposalService.cs** (IProposalService interface):
  - Builds proposal JSON matching T Broker endpoint schema (carFeetList + quotationPremCoverList)
  - POSTs JSON to `motor/check-insure-premiums2-form/add` via HttpClient
  - Parses response → transaction ID, policy number
  - Error handling + response mapping
- **ChatbotService integration:**
  - Injected IProposalService
  - SubmitProposalAsync now submits selected proposal to T Broker
  - Maps selected proposal (from context) → MotorProposalData → T Broker JSON
  - Returns transaction ID on success
- **Startup.cs:** Registered IProposalService in DI
- **Backend:** ✅ Compiled successfully
- **Flow complete:** ask_product → SPIN deep → search packages → propose → T Broker submit

## [2026-06-02] integration | Frontend integration + servers ready
- **app.module.ts:** Imported ChatbotModule
- **app-routing.module.ts:** Added `/chatbot` route → ChatbotComponent
- **ChatbotComponent:** Set standalone=false for NgModule compatibility
- **Frontend build:** ✅ Successful (82.980 seconds)
- **Servers:** ✅ Backend + Frontend dev servers starting
- **Next:** Access http://localhost:4200/chatbot → Login → Test chat flow

## [2026-06-02] integration | Live debugging — 4 runtime bugs fixed, end-to-end เกือบครบ
- **Bug 1 (404):** frontend เรียก URL relative (localhost:4200) → แก้ `apiUrl` ใน chatbot.component.ts เป็น absolute `http://localhost:49505/api/FrontEnd/Chatbot`
- **Bug 2 (401):** [Authorize] + SetAuthInfo() บังคับ JWT → ปิด [Authorize] ชั่วคราว, ใช้ TEST_USER mock ใน ChatbotController (ทั้ง 3 endpoint) — ⚠️ ต้องเปิดคืนก่อน production
- **Bug 3 (500 DI):** `IChatbotService/IMotorQueryService/IProposalService` resolve ไม่ได้ → **ค้นพบ: ระบบใช้ `Program.cs` ไม่ใช่ `Startup.cs`!** เพิ่ม AddScoped 3 ตัวใน Program.cs (Startup.cs เป็น dead code)
- **Bug 4 (query.py path):** MotorQueryService คำนวณ relative path ผิด (ขึ้น 3 ระดับ ได้ `KPN_Backend\ChatBot\...`) → แก้เป็น absolute `C:\CTD\ChatBot\InsureChat\tools\motor` + set `WorkingDirectory` ใน ProcessStartInfo
- **Example data:** เปลี่ยนรุ่นรถปลอม (Nissan Civic) → mapping จริงตามยี่ห้อ (Honda→Civic/Accord/City/CR-V, Toyota→Altis/Yaris/Fortuner, Nissan→March/Almera/X-Trail...) + ปี 2023–2026
- **ยืนยัน query.py ใช้งานได้จริง:** Honda Civic = 9,539 แพคเกจ; Civic 2022 ชั้น 2+ ทุน≤300k = 42 แพคเกจ (ธนชาต 2+ Fit, CIVIC 2.0 HYBRID เบี้ย 5,120 บาท)
- **Runtime facts:** Backend `49505` (http)/`49504` (https), Frontend `4201`, Python script ต้องรันใน WorkingDirectory ของตัวเอง (อ้าง raw JSON แบบ relative)
- **สถานะ:** Backend rebuild+listening; flow product→spin_deep→query 330k ทำงาน; **ยังไม่ทดสอบ end-to-end ครบถึง T Broker submit**
- **Next:** ทดสอบ flow เต็มในเบราว์เซอร์ → ยืนยัน proposal submit เข้า T Broker (endpoint ยัง hardcode localhost:3000)

## [2026-06-02] integration | ⭐ แก้ "ไม่พบแพคเกจ" — chatbot เสนอราคาได้จริงแล้ว
- **Root cause #1 (ParseQueryOutput schema ผิด — กระทบทุก query):** โค้ดคาดว่า output เป็น JSON array + ใช้ field `insureName/carBrand/motPremNet` แต่ query.py คืน `{matches, results:[...]}` + field จริงคือ `insureID/carBrandName/premNet/sumInsureMax/year` → loop ไม่เคยทำงาน → ว่างเสมอ (แม้รถมีข้อมูล). แก้: อ่าน `results` array + map field ถูก + class ใช้ค่าที่ผู้ใช้เลือก (output ไม่มี field class)
- **Root cause #2 (exact year/sum เข้มไป):** ชุดข้อมูลเก็บปีจดทะเบียนจริงของรถแต่ละรุ่น เช่น Nissan SYLPHY ชั้น 1 มีแค่ **ปี 2012–2020** (ไม่มี 2022) → exact year = 0. แก้: เพิ่ม **progressive relaxation** ใน MotorQueryService — ค้นเต็มก่อน (brand+model+year+class+sum) ไม่เจอค่อยตัด sum→year→model→เหลือ brand; เรียง prem ถูกสุดก่อน
- **Root cause #3 (spin_deep loop):** ไม่เจอแล้ว return ไม่เปลี่ยน step → พิมพ์อะไรก็ค้นซ้ำ + เก็บ junk เข้า discoveredNeeds. แก้: บันทึก need ครั้งเดียว (guard ด้วย SpinReason ว่าง) + เพิ่ม case `ready_to_propose` (กดปุ่มเสนอ/พิมพ์ "เริ่มใหม่")
- **ปรับ reply:** แสดงรถ/ปี/ทุน/ชั้นจริงของแต่ละแพคเกจ + คำเตือน compliance "เบี้ยเป็นเรตแพคเกจ ต้องยืนยันใน T Broker ก่อนออกจริง"
- **ทดสอบ API ผ่าน:** SYLPHY 2022 ชั้น 1 ทุน 300k → relax → 5 แพคเกจ (ธนชาต SYLPHY 1.6/1.8 ปี 2016-17 เบี้ย 9,308), step=ready_to_propose ✅
- **ข้อสังเกต:** ชุดข้อมูล T Broker นี้ครอบคลุมปีรถถึง ~2020 (บางรุ่น) — ผู้ใช้กรอกปีใหม่กว่าจะถูก relax เป็นปีใกล้เคียง (แสดงปีจริงให้เห็น)
- **Backend run แบบ detached** (Start-Process) เพื่ออยู่รอดข้าม session (Start-Job เดิมตายเมื่อ session จบ)
- **Next:** ทดสอบในเบราว์เซอร์ → กด "เสนอแพคเกจ" → ยืนยัน T Broker submit (ยัง hardcode localhost:3000)

## [2026-06-02] setup | ⭐ Refactor — แยก chatbot ออกจาก KPN เป็น standalone app (InsureChat/app/)
- **บริบท:** ทำ /grill-with-docs ตกผลึก 6 มติ → สร้าง [[docs/adr/0001]] (แยกจาก KPN) + [[docs/adr/0002]] (user store JSON→SQL) + อัปเดต glossary [[CONTEXT.md]] (User แตกเป็น role agent/customer, แก้ React→Angular)
- **โครงใหม่:** `app/api/` (ASP.NET Core net10, namespace `InsureChatBot.*`) + `app/web/` (Angular 21 standalone) — ตัด `AuthBaseController`/`EFDbContext`/namespace KPN ทิ้งหมด
- **Backend:** real auth ของตัวเอง — `IUserStore`+`JsonUserStore` (users.json, PBKDF2) + JWT bearer (role claim) + AuthController/Login; ChatbotService เพิ่ม role + step `ask_customer` (เฉพาะ agent กรอกชื่อ/email ลูกค้า); MotorQueryService path จาก appsettings (เลิก hardcode); ProposalService เพิ่ม `SubmitMode mock|live` (default mock)
- **บั๊กที่เจอ+แก้:** System.Text.Json serialize anonymous type ที่มีทั้ง `rowID`+`rowId` (case clash) ไม่ได้ → เปลี่ยน quotationPremCoverList item เป็น `Dictionary<string,object>` คงคีย์เป๊ะ (บั๊กติดมาจากโค้ด KPN เดิม)
- **ทดสอบผ่าน (smoke test API):** login agent/customer ✅ · flow รถ→Honda Civic 2020 ชั้น1 300k → motor query เจอจริง (ธนชาต เบี้ย 11,636) ✅ · agent มี ask_customer / customer ข้าม ✅ · Propose mock คืน MOCK-id ✅ · no-token→401, bad login→401 ✅ · frontend build+serve (4200) + api (5228) คู่กันได้ ✅
- **Frontend:** ng new plain → core (auth signal-based / jwt interceptor / auth guard) + login + chatbot (copy+adapt: apiUrl จาก environment, ต่อ submitProposal→/Propose จริง, logout)
- **ยังไม่ทำ (flag):** proposal มี field ว่างเยอะ (Tax/ที่อยู่/idcard) — live จริงอาจถูก reject · property/PA ยัง stub (มีแต่ motor) · โค้ด chatbot ใน repo KPN เดิมยังไม่ถอด (กัน 2 ก๊อปปี้)
- **Next:** ผู้ใช้อนุมัติ diff CLAUDE.md (เพิ่ม app/ + docs/ ใน folder layout) · ทดสอบ UI ในเบราว์เซอร์ · ถอดโค้ด chatbot ออกจาก KPN_Backend/KPN_Frontend

## [2026-06-02] setup | ✅ ถอด chatbot ออกจาก KPN + อัปเดต schema CLAUDE.md (ผู้ใช้สั่ง "ทำเลย")
- **CLAUDE.md §4:** เพิ่ม `CONTEXT.md`, `tools/motor/`, `docs/adr/`, `app/` (api/web) เข้า folder layout ให้ตรงจริง
- **KPN_Backend:** ลบ 8 ไฟล์ chatbot (untracked — ไม่เคย commit) + ถอด DI ใน Program.cs (กลับ HEAD) และ Startup.cs (3 บรรทัด) → **build ผ่าน 0 error** (เหลือแต่ warning test เดิม). appsettings.Development.json ไม่แตะ
- **KPN_Frontend:** ลบโฟลเดอร์ `views/modules/chatbot/` + ถอด import/ChatbotModule ใน app.module.ts และ route ใน app-routing.module.ts → ทั้งสองไฟล์ **กลับตรงกับเวอร์ชัน commit** (การต่อ chatbot เดิมเป็น working-tree ที่ยังไม่ commit) · grep ยืนยันไม่เหลือ ref `chatbot` ใน src
- **ผล:** chatbot อยู่ที่เดียวคือ `InsureChat/app/` แล้ว — ไม่มีก๊อปปี้ค้างใน KPN. branch KPN ทั้งคู่ = `benz` (ยังไม่ commit, ผู้ใช้ review เองได้)
- **Next:** ทดสอบ UI เบราว์เซอร์ (login→chat→propose) · เติม field proposal สำหรับ T Broker live · property/PA flow

## [2026-06-02] setup | ✅ E2E browser test ผ่าน (Playwright)
- **ทดสอบจริงผ่าน Chromium:** เปิด localhost:4200 → redirect /login → login agent → /chat (badge AGENT "ตัวแทนทดสอบ") → พิมพ์ flow 8 ข้อความ (รถ→Honda→Civic→2020→ชั้น1→300k→SPIN→ชื่อ/email ลูกค้า) → ปุ่ม "เสนอแพคเกจ" โผล่ → คลิก → ได้ `✅ ข้อเสนอถูกบันทึก (โหมดทดสอบ) MOCK-3894bd780562`
- **ยืนยันสิ่งที่ curl ทดสอบไม่ได้:** CORS (4200→5228) ผ่าน · JWT interceptor แนบ token จริง · ปุ่ม/การคลิกทำงาน · ภาษาไทยเรนเดอร์ครบ · **ไม่มี console/page error**
- **เก็บกวาด:** ลบ harness ทดสอบ (app/.e2e), ปิด dev server ทั้งคู่. T Broker คงไว้ mock (ยังไม่ยิง live ตามที่ตกลง)

## [2026-06-02] setup | ⭐ ต่อยอด 3 ฟีเจอร์: self-register + proposal detail + property/PA flow (ผู้ใช้สั่ง "ทำต่อทั้งหมดเลย")
- **1) Customer self-register:** `IUserStore.TryAddUser` + `JsonUserStore` เขียนไฟล์ได้ (thread-safe, lock) + `POST /Auth/Register` (role บังคับ customer, hash PBKDF2, auto-login) + หน้า register (Angular) + ลิงก์จาก login. ต่อยอด seam ADR 0002 (อนาคต SqlUserStore ก็แค่เปลี่ยน DI). ทดสอบ: สมัครได้/ซ้ำ→409/pw สั้น→400 ✅
- **2) Proposal fields for live:** เพิ่ม step `ask_customer_detail` (เก็บ เบอร์โทร/บัตรปชช.13หลัก/ทะเบียนรถ ด้วย regex parse, พิมพ์ "ข้าม" ได้) → map ลง proposal JSON (`mobile/phone/idcard/licno`). agent ผ่าน ask_customer→ask_customer_detail; customer ใช้ profile + detail. **ยังคง mock** — ยังไม่ยิง live (field อื่นเช่นที่อยู่/cc ยังว่าง อาจถูก T Broker reject)
- **3) Property/PA flow:** เพราะมีแต่ความรู้ (concept) ไม่มี catalog ราคา + endpoint เป็น motor-only → ทำ flow เก็บความต้องการ + handoff ส่งต่อตัวแทน (ไม่ออกราคาอัตโนมัติ ตามความจริง). property: prop_type→prop_sum→prop_spin→handoff_contact→done (ใช้ความรู้ [[ประกันอัคคีภัย-และที่อยู่อาศัย]]: under-insurance, sub-limit น้ำท่วม). PA: pa_occupation→pa_sum→pa_spin→handoff (ใช้ [[ประกันอุบัติเหตุส่วนบุคคล-PA]]: ชั้นอาชีพ, คุ้มเฉพาะอุบัติเหตุ). **ไม่มีปุ่มเสนอแพคเกจ** (isReadyToPropose=false)
- **ทดสอบครบ:** API (register/motor+detail/property/PA) ✅ · **Browser E2E** (Playwright): สมัครผ่าน UI → auto-login role customer → property flow → handoff/done ไม่มีปุ่มเสนอ, ไม่มี console error ✅ · backend build 0 error · frontend build (register lazy chunk) ✅
- **เก็บกวาด:** คืน seed users.json (agent+customer), ลบ harness, ปิดเซิร์ฟเวอร์
- **ยังไม่ทำ:** property/PA ออกราคาจริง (ต้องมี catalog/แหล่งข้อมูล — วางโบรชัวร์ลง raw/ แล้ว ingest) · proposal field ครบสำหรับ live (ที่อยู่/cc/chassis) · prospect/conversation persistence (ยัง session-based)

## [2026-06-02] setup | ⭐ LLM conversation engine (OpenRouter gpt-oss-120b) + tool-use + fallback (ดู [[docs/adr/0003]])
- **บริบท:** /grill-with-docs ตกผลึก → ใช้ LLM คุยเป็นธรรมชาติ + ปิดการขาย, ข้อมูลผ่าน tool เท่านั้น (grounding). provider = **OpenRouter `openai/gpt-oss-120b:free`** (OpenAI-compatible, config-driven สลับ Claude ได้), key จาก `.env` (`OPENROUTER_API_KEY`)
- **สถาปัตยกรรม:** `IConversationEngine` 2 ตัว — `LlmConversationEngine` (หลัก) + `StateMachineConversationEngine` (เดิม = fallback). `ChatbotService` = router (มี key→LLM; error/ปิด→state machine). `OpenAiCompatibleClient` (typed HttpClient, SnakeCaseLower). `SystemPromptProvider` โหลด playbook+concept จาก wiki เป็น system prompt. `DotEnv` loader (ไต่หา .env ที่ root). controller/contract เดิม + frontend **ไม่แตะ**
- **Tools:** `search_motor_packages` (→ 330k จริง, เก็บใน context.Proposals) · `prepare_proposal` (เลือกแพคเกจ + ข้อมูลลูกค้า → set ready). **ส่งจริง = คนกดปุ่ม → /Propose** (deterministic, human-in-the-loop)
- **ทดสอบผ่าน:** LLM คุยไทยธรรมชาติ (เปอร์โซนา "พิมพ์") → เรียก search จริง → prepare → /Propose=MOCK ✅ · fallback (ปิด LLM → state machine ask_brand) ✅ · build 0 error
- **ปัญหาโมเดลฟรี (flag):** (1) latency ~45-60s/ข้อความ (2) เคยมั่วเลข (แต่งเบี้ย/ติดป้าย "ต่อเดือน") → **แก้ด้วย system prompt เข้ม + temp 0.4 → เลขเป๊ะตาม tool แล้ว** (3) ยัง **มั่วชื่อบริษัทยาวๆ ภาษาไทย** (เช่น "ธนชาตประกันภัย จำกัด(มหาชน)..." → เพี้ยน) แม้ใส่กฎ copy ตรงตัว — *แต่ proposal ที่ส่งจริงใช้ค่าจาก context (ถูกต้องเสมอ)*
- **Next:** structured proposal display (UI โชว์ชื่อบริษัท/เบี้ยจริงจาก context แทน prose LLM) = follow-up #1 · render markdown ในแชต · หรือสลับเป็น Claude/รุ่นใหญ่กว่าเพื่อความเป๊ะ
- **Security:** เพิ่ม `.gitignore` กัน `.env` (มี key) หลุด

## [2026-06-02] setup | follow-up #1+#2: structured proposal card + render markdown ในแชต
- **#2 Markdown:** ติดตั้ง `marked` → assistant bubble เรนเดอร์ผ่าน `[innerHTML]="renderMarkdown(text)"` (Angular sanitize อัตโนมัติ) → ตาราง/ตัวหนา/ลิสต์ของ LLM แสดงสวย. CSS ตาราง markdown อยู่ใน `styles.css` (global เพราะ innerHTML ไม่ติด component scope), `.md-content`
- **#1 Proposal card:** เพิ่มการ์ด (chatbot.html) ดึงจาก `conversationContext.proposals[0]` (ข้อมูลจริงจาก backend) แสดง ชื่อบริษัท/รถ/ชั้น/ทุน/เบี้ยสุทธิ/เบี้ยรวม + คำเตือน T Broker — **แก้ปัญหา LLM มั่วชื่อบริษัท**: prose อาจเพี้ยน แต่การ์ด (และ proposal ที่ส่งจริง) ใช้ค่าจริงเสมอ. ใช้ได้ทั้ง engine LLM และ state machine (ทั้งคู่ populate proposals[0] ตอน ready)
- **ทดสอบ browser (LLM):** ตาราง markdown เรนเดอร์ (1 `<table>`) ✅ · การ์ดโชว์ "ธนชาตประกันภัย จำกัด(มหาชน)สำนักงานใหญ่ / HONDA CIVIC 1.8 ปี 2020 / ชั้น 1 / 450,000 / เบี้ย 11,636" (ชื่อเต็มถูกต้อง) ✅ · ปุ่มเสนอแพคเกจ ✅ · ไม่มี console error ✅ · build 0 error
- **Note:** frontend bundle chatbot โต ~7→52kB (รวม marked)

## [2026-06-02] setup | สลับ LLM → DeepSeek V4 Flash (`deepseek/deepseek-v4-flash`)
- เปลี่ยน `appsettings.json` → `Llm:Model = "deepseek/deepseek-v4-flash"` (หา id จริงจาก OpenRouter /models). **เป็นรุ่นเสียเงิน** (กิน credit) ต่างจาก gpt-oss-120b:free เดิม
- **ทดสอบจริง:** latency ~29s (เร็วกว่าเดิม ~2 เท่า) · เรียก search → 5 แพคเกจจริง (ธนชาต 11,636) · เบี้ย+label ถูกต้อง · ภาษาไทยลื่น · บอกเองว่า "ทุน 300k ไม่มีในระบบ แนะนำใกล้เคียง" (ซื่อสัตย์/แม่นกว่า gpt-oss ที่เคยมั่วเลข+ชื่อบริษัท)
- สลับกลับ/เปลี่ยนรุ่นได้ที่ `Llm:Model` อย่างเดียว (config-driven ตาม ADR 0003)

## [2026-06-04] setup | ⭐ Quote form + ตารางเปรียบเทียบเบี้ย (ดึง KPN SQL DB สด) → handoff เข้า chat (ADR 0004)
- **บริบท:** /grill-with-docs → อยากกรอกฟอร์มรถก่อน (เร็ว/แม่นกว่าพิมพ์) + แสดงตารางเปรียบเทียบเบี้ย เหมือนหน้า KPN check-insure-premiums2. มติ (re-confirm 2 รอบ): ดึงข้อมูล**สด**จาก **KPN SQL DB ตรง** (DB `KPNNew` @ 210.1.60.104) — qualify [[docs/adr/0001]]
- **Backend:** Dapper + Microsoft.Data.SqlClient · `MotorFacetSqlService` อ่าน `PackagePolType` (+join PackageMotor/PolicyType/Insure) → `MotorController [Authorize]`: `GET /Motor/{Brands,Models,Cc,Years,PolTypes,SumInsures}` + `POST /Motor/Search`. connstring จาก env `KPN_DB_CONNECTION` (.env, +TrustServerCertificate). บั๊กที่แก้: SQL DISTINCT+ORDER BY cast (wrap subquery)
- **Frontend:** หน้า `/quote` (default หลัง login) — dropdown cascade (brand→model→cc→year→ผลิตภัณฑ์→ทุน) + ปุ่มค้นหา → ตารางเปรียบเทียบ (บริษัท/ผลิตภัณฑ์/ซ่อม/ทุน/เบี้ย premStm+premNet) + sort + ปุ่มเลือก. `QuoteHandoff` service → seed conversationContext (proposals[0] + greeting + การ์ด + ปุ่มเสนอ) แล้ว LLM ปิดการขายต่อ → /Propose เดิม
- **บั๊กสำคัญที่เจอ:** แอป **zoneless** (Angular 21) → set plain property ใน HTTP callback ไม่ re-render → แปลง list ใน quote เป็น **signals** (chatbot รอดเพราะมี signal.set ใน callback อยู่แล้ว)
- **ทดสอบ E2E (browser, DB สด):** login→/quote → AUDI A1 1.5/1500/2023 → ค้นเจอ 3 แพคเกจวิริยะจริง → เลือก → chat seeded (การ์ดโชว์ "วิริยะประกันภัย จำกัด (มหาชน)" เต็มถูกต้อง) → เสนอแพคเกจ → MOCK ✅ · ไม่มี console error · api/web build ผ่าน
- **Next:** จังหวัด/ทุนไฟไหม้-น้ำท่วม, เทียบ 4 บริษัท, โลโก้ · fallback ไป local 330k ถ้า DB ล่ม · SetPremStm (ส่วนลด) ตามสูตร KPN เป๊ะ

## [2026-06-05] setup | ⭐ รื้อ flow ใหม่: intake 5 ฟิลด์ → AI scoping → shortlist (ADR 0005)
- **/grill-with-docs:** เปลี่ยน flow — ฟอร์มเหลือ **5 ฟิลด์ = ระบุตัวรถ** (ยี่ห้อ/รุ่น/CC/ปี/จังหวัด) → ค้นได้ candidate set → **เข้าแชตเลย** ให้ AI สโคป (ทุน/งบเบี้ย/ไฟไหม้/น้ำท่วม) แล้วกรองเหลือ shortlist 3 ให้เลือก. ตัด: dropdown ผลิตภัณฑ์/ทุน/ไฟ/น้ำ + ตารางเปรียบเทียบ + เทียบ-4 (qualify [[docs/adr/0004]])
- **จังหวัด — ตรวจ DB จริงแล้วกรองไม่ได้:** ทุกแพคเกจ `ProvinId=0` nationwide, ไม่อยู่ใน ProvinSale (CIVIC 53 แผน inProvinSale=0). หลักฐาน KPN production ที่ผู้ใช้ส่งมายืนยัน (`provinnamePackage="ไม่มีข้อมูล"`, ส่ง provinPlate แต่คืนผลปกติ) → **จังหวัดเป็นฟิลด์เก็บลง proposal ไม่ใช่ตัวกรอง** (ไม่ทำ filter ปลอม — grounding)
- **Backend:** `context.Candidates` (candidate set round-trip) + rewrite `LlmConversationEngine` เป็น **scoping engine**: ฉีด candidate summary (จำนวน/บริษัท/ช่วงทุน-เบี้ย/ชั้น) เข้า system prompt + tool **`filter_packages`** (กรอง set จริงด้วย sum/prem/fire/flood/class → shortlist 3 → Proposals) + `prepare_proposal` เดิม. ตัด search_motor_packages (330k) ออกจาก LLM engine. candidate cap 50→500
- **Frontend:** quote เหลือ 5 ฟิลด์ + ปุ่ม "ค้นหา→คุยกับ AI"; `QuoteHandoff` เก็บ candidate set; chatbot seed candidates + greeting "พบ N แผน จาก M บริษัท" เปิดสโคป. login→/quote
- **ทดสอบ E2E (LLM จริง DeepSeek):** (1) ตั้งทุน 4 แสนที่ไม่มีจริง → AI ตอบตามจริง "ไม่มีแผนตรงเกณฑ์ ทุนสูงสุด 100k เบี้ย 2,900–8,500" (grounded ไม่มั่ว) ✅ (2) งบ 30k → filter → shortlist → เลือก → การ์ด "วิริยะ ประเภท 3 เบี้ย 2,900" → propose MOCK ✅ · ไม่มี console error · api/web build 0 error
- **Next:** LLM-down fallback (แสดง candidate เป็นลิสต์กรองมือ) · ลบโค้ด quote เก่าที่ไม่ใช้ (compare/sort CSS)

## [2026-06-04] setup | quote form parity เต็ม — เหมือน KPN API หลัก (จังหวัด/ไฟไหม้/น้ำท่วม + โลโก้ + เทียบ 4 + ส่วนลด)
- **เป้า:** ผู้ใช้สั่ง "เอาให้เหมือน api หลักมากสุด เพื่อกันผิดพลาด" → อ่าน logic จริงของ KPN แล้ว mirror
- **SetPremStm (เบี้ยหลังส่วนลด):** copy สูตรเป๊ะจาก `CheckPrmCheckPrmController.SetPremStm` — `premStm − (P:premNet*disc/100 | B:disc)`. PackageRow คืน `premStm`=หลังลด (ตรง KPN), `premTotal`=ดิบ, `discount`+`discType` (%/บาท/อื่นๆ)
- **โลโก้:** `Insure.LogoPath` + base `https://kpnnewapi.clicktodesign.net/image/logo/` (ตรง `DataService.GetUrlLogo`). ว่างได้ถ้า LogoPath null
- **fire/flood:** facet จาก `PackageMotor.FireTheft/Flood` (string→เลข) + filter ใน Search · **จังหวัด:** master `Province` (ProvId/ProvName) — *provinid ไม่กรองแพคเกจ (KPN ก็ส่งไปเฉยๆ ไม่ใช้ใน WHERE)* แค่เก็บลง proposal
- **endpoint ใหม่:** `/Motor/{FireSumInsures,FloodSumInsures,Provinces}` · Search คืน logo/premTotal/discount/discType/fire/flood
- **Frontend:** quote เพิ่ม 3 dropdown (จังหวัด/ไฟไหม้/น้ำท่วม → 9 ช่อง ตรงตัวอย่าง KPN) + คอลัมน์โลโก้ + checkbox **เทียบสูงสุด 4** + panel เปรียบเทียบ side-by-side + แสดงส่วนลด
- **fallback local 330k: ตัดสินใจไม่ทำ** — snapshot ขาด field (discount/logo/fire/flood/polTypeId int) จะให้ผล "ต่างจาก KPN" = ขัดเป้า "เหมือน api หลัก". ใช้ error handling สะอาดแทน (DB ล่ม → 500 + UI ขึ้น "เชื่อมฐานข้อมูลไม่ได้")
- **ทดสอบ E2E (DB สด):** login→/quote → จังหวัด 83 + cascade + fire/flood โหลด → ค้น 3 แพคเกจ + checkbox เทียบ → ติ๊ก 2 → panel เปรียบเทียบ 2 การ์ด → เลือก → chat seeded (วิริยะฯ) ✅ · api/web build 0 error · ไม่มี console error

## [2026-06-09] setup | chatbot เป็น public + admin control panel (ADR 0011)
- **grill-with-docs:** ซัก 5 คำถาม → ตัดสิน: chatbot/quote เป็น **public ไม่ต้อง login**; ตัด role agent/customer; เหลือ `admin` login เข้า `/admin`; admin จัดการ persona + LLM connection + theme; แก้มีผลทันทีแชตรอบถัดไป; API key write-only+masked+fallback env
- **เอกสาร:** ADR `0011-public-chatbot-and-admin-control-panel.md` (amend 0001/0002, ต่อยอด 0010) + `CONTEXT.md` (redefine User→admin-only, เพิ่ม anonymous/Admin panel/Editable persona/Locked prompt/LLM connection/API key/app-settings.json, deprecate agent/customer/self-register)
- **Backend:** `UserRoles.Admin` + seed `admin/admin1234`; `AppSettingsStore`+`app-settings.json` (theme/persona/llm); `SystemPromptProvider` แยก persona(editable)+locked; LLM อ่าน effective settings runtime; Chatbot/Motor → `[AllowAnonymous]`; ลบ `/Auth/Register`; `AdminController` (Persona/Llm masked/Theme) + `SettingsController` (public Theme). build 0 error
- **Frontend:** route public chat/quote + `adminGuard` `/admin`; ลบ register; login → เข้า admin เท่านั้น; `BrandTheme` ดึง `--brand` ตอน bootstrap; หน้า admin panel 3 แท็บ (persona/LLM/theme + พรีวิวสด). build 0 error
- **gitignore:** เพิ่ม `app/api/Data/app-settings.json` (มี API key)

## [2026-06-10] setup | ลบชื่อบริษัทจริงออกจากข้อความที่ลูกค้าเห็น (anti-impersonation)
- **เหตุผล:** กันแอบอ้างบริษัทอื่น — แชตบอต/UI ห้ามเอ่ย "T Broker"/"KPN" กับลูกค้า เปลี่ยนเป็น "ตัวแทน"
- **เปลี่ยน (customer-facing):** system prompt กฎ#3 + style note (`SystemPromptProvider`), คำเตือนเบี้ย (`StateMachineConversationEngine`, `LlmConversationEngine`), ข้อความผล proposal mock/live/error (`ProposalService`), การ์ดเสนอแพคเกจ + detail note (`chatbot.html`) — "ยืนยันใน T Broker" → "ยืนยันกับตัวแทน", "ยังไม่ส่ง T Broker" → "ยังไม่ส่งให้ตัวแทน"
- **คงไว้ (internal ไม่ถึงลูกค้า):** ชื่อ class `TBrokerSettings`/`KpnDbSettings`, config section + env `KPN_DB_CONNECTION`, JSON field สคีมา partner (`brokerCode`/`moBrokerage*`), LogoBaseUrl, comment/ADR/log — functional + ไม่ใช่การแอบอ้าง
- ตรวจ: 6 ไฟล์ knowledge ที่ฉีดเข้า prompt ไม่มีชื่อบริษัท → ไม่หลุดทางบอท · api/web build 0 error

## [2026-06-10] setup | บังคับ ชื่อ+เบอร์โทร ก่อนเสนอแพคเกจ (grill-with-docs)
- **มติ (grill 3 ข้อ):** conversational (บอทถาม) + **backend บังคับ**; เบอร์ = strip อักขระไม่ใช่ตัวเลข → ต้อง 9–10 หลัก, ชื่อ = ไม่ว่าง; ปุ่มโผล่เมื่อ pin แผน, backend reject ตอนกดถ้าขาด (ไม่ผูก IsReadyToPropose)
- **Backend:** `ChatbotService.SubmitProposalAsync` validate ชื่อ+เบอร์ → reject พร้อมข้อความชวนพิมพ์ในแชต; ส่ง `Phone` เป็น digits ล้วนไปกับ proposal · `SystemPromptProvider` กฎ#6 + bullet: บอทต้องเก็บชื่อ+เบอร์ทันทีหลังเลือกแผน ก่อนชวนกด · `StateMachineConversationEngine` (fallback): public path ถามชื่อ (เลิก assume userName ว่าง) + บังคับเบอร์ก่อน ready_to_propose
- **เอกสาร:** CONTEXT.md เพิ่ม [[#Required identity (ชื่อ+เบอร์)]] + แก้ [[#Confirm]] (มี gate) — ไม่ทำ ADR (validation rule กลับง่าย)
- **ทดสอบ:** propose smoke 4/4 (no name→reject ชื่อ · name only→reject เบอร์ · เบอร์สั้น→reject · ชื่อ+เบอร์มีขีด 10 หลัก→success+strip) · driver.mjs end-to-end PASS (SM เก็บชื่อ→เบอร์→propose) · อัปเดต driver flow ('ข้าม'→'0812345678') · api/web build 0 error

## [2026-06-10] setup | ปุ่ม "ส่งคำสั่งซื้อ" + ส่งคำสั่งซื้อทางอีเมลถึงโบรคเกอร์ (grill-with-docs, ADR 0012)
- **มติ (grill 3 ข้อ):** อีเมล**แทนที่** partner POST · **Gmail SMTP + App Password** (MailKit, env `SMTP_PASSWORD`) ส่งไป clickto.helper@gmail.com · **mock เมื่อยังไม่ตั้ง password** (log+success) / **error เมื่อส่งจริงล้ม**
- **Backend:** เพิ่ม `MailKit`; `OrderEmailSettings` (host/port/user/pass/from/to, env `SMTP_PASSWORD`/`ORDER_EMAIL_TO`); `OrderEmailService` ประกอบ **ฟอร์ม HTML** (ลูกค้า ชื่อ/เบอร์/อีเมลถ้ามี/บัตร/ทะเบียน + แพคเกจ บริษัท/ชั้น/รถ/ทุน/เบี้ย) + reply-to อีเมลลูกค้า; `ChatbotService.SubmitProposalAsync` เรียก OrderEmailService แทน ProposalService + ข้อความสำเร็จใหม่
- **ปุ่ม/ข้อความ:** "เสนอแพคเกจ" → **"ส่งคำสั่งซื้อ"** ทุกที่ (frontend button, error, prompt, state machine, tool desc); frontend ตัด "(เลขอ้างอิง: ...)" ออก
- **ข้อความสำเร็จใหม่:** "ระบบจะส่งคำสั่งซื้อไปยังโบรคเกอร์ เพื่อให้เจ้าหน้าที่ติดต่อกลับ โปรดดำเนินการตามเงื่อนไขที่เจ้าหน้าที่แจ้ง เพื่อดำเนินการออกกรมธรรม์ต่อไป"
- **เอกสาร:** ADR `0012-order-via-email-to-broker.md` + CONTEXT.md ([[#Order email]] + แก้ [[#Confirm]]/[[#Proposal]]) + run-skill SKILL.md (env SMTP_PASSWORD)
- **ทดสอบ:** propose smoke 3/3 (success+ข้อความใหม่ · ไม่มี MOCK ref · gate ชื่อยังอยู่) · mock email log แสดงฟอร์ม HTML ครบ + เบอร์ strip เป็น digits · api/web build 0 error. **ยังไม่ทดสอบส่งจริง** (ต้องใส่ SMTP_PASSWORD)
