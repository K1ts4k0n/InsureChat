---
title: Glossary — InsureChat Chatbot Domain
type: glossary
created: 2026-06-02
---

# 🧠 CONTEXT — InsureChat Chatbot Domain

ศัพท์เฉพาะและแนวคิดหลักของระบบ chatbot ขายประกันวินาศภัยแบบ web-embedded

---

## 👤 ผู้ใช้ & Session

| ศัพท์ | ความหมาย | ตัวอย่าง |
|------|---------|---------|
| **User (admin)** | **บัญชีที่ login เข้าระบบ — เหลือ role เดียวคือ `admin`** (2026-06-09 ยุบ login ฝั่งลูกค้าทั้งหมด). ใช้เข้า [[#Admin panel]] จัดการระบบเท่านั้น ไม่มี chatbot flow ของตัวเอง. **seed เท่านั้น สมัครเองไม่ได้**. ดู [[docs/adr/0011]] | `{username, role:"admin", name, email}` |
| **ผู้ใช้ทั่วไป (anonymous)** | คนที่เข้า [[#Session\|chatbot]]/quote **โดยไม่ login** — เป็น **public** เปิดให้ทุกคนใช้. identity (ชื่อ/เบอร์/อีเมล) มาจาก [[#PII intake turn]] ในแชต ไม่พึ่ง JWT. แทนที่แนวคิด customer/agent เดิม | ใครก็ได้เปิด /chat ขอเบี้ยรถ |
| ~~**Agent / Customer (role เดิม)**~~ | **ยกเลิกแล้ว (2026-06-09)** — เดิมแยก agent (ตัวแทนกรอกแทนลูกค้า) / customer (ลูกค้า login ขอเอง). มติใหม่: chatbot เป็น public ไม่มี login ฝั่งผู้ใช้ → 2 role นี้หายไป เหลือ [[#ผู้ใช้ทั่วไป (anonymous)\|ผู้ใช้ทั่วไป]] + admin. amend [[docs/adr/0001]] ผ่าน [[docs/adr/0011]] | — |
| **Admin panel** (`/admin`) | หน้าหลังบ้านที่ login (role admin) แล้วเข้าจัดการ 4 อย่าง: [[#Editable persona]], [[#App theme]], [[#LLM connection settings]], [[#Email delivery settings]]. guard ด้วย role admin **ทั้ง 2 ชั้น** — frontend route + backend `[Authorize(Roles="admin")]`. ดู [[docs/adr/0011]] | admin login → แก้ persona/สี/โมเดล |
| **App theme** (brand color) | **สีแบรนด์ระดับแอป (global)** ที่ [[#Admin]] ตั้งได้ — สี primary 1 สี + เฉด hover + สีตัวอักษรบนปุ่ม (derive อัตโนมัติ). สี semantic (success/error/warn) คงที่ ไม่อยู่ใน theme. เก็บฝั่ง server, client ดึงตอนเปิดแอป (ก่อน login ก็ธีมแล้ว). **แกนแยกจาก [[#Surface theme]]**. ดู [[docs/adr/0010]] | admin เลือก preset เขียว → ทุกหน้าเป็นเขียว |
| **Surface theme** (light/dark) | **โหมดสว่าง/มืด** — แกนคนละตัวกับ brand color. สลับ token พื้น/ตัวอักษร/ขอบผ่าน `[data-theme]` บน `<html>`. **per-user + ตามอุปกรณ์** (ค่าเริ่มจาก `prefers-color-scheme`, override ด้วย toggle, จำใน `localStorage` — ไม่ขึ้น server). เพิ่มตอน redesign 2026-06-09 (amend [[docs/adr/0010]] ที่เดิมตัด dark ออก) | ผู้ใช้กดสลับเป็นมืด → จำไว้เครื่องนั้น, สีแบรนด์เท่าเดิม |
| **Editable prompt sections** (คำสั่ง AI) | ส่วนของ system prompt ที่ [[#Admin panel\|admin]] **แก้ได้** ผ่านแท็บ **"คำสั่ง AI (System Prompt)"** — แตกเป็น **4 ช่องแยกกัน แก้/รีเซ็ตได้รายช่อง** (2026-06-11 amend [[docs/adr/0011]] ผ่าน [[docs/adr/0014]]): ① **บุคลิก/โทน** (persona เดิม) ② **วิธีการขาย & การสโคป** (consultative/SPIN, 4 ประเด็นสโคป — *เจตนาการขาย ไม่ใช่กลไก tool*) ③ **กฎเหล็ก compliance** (ห้ามแต่งเบี้ย/verify/คปภ. — ย้ายจาก locked มาเป็นแก้ได้) ④ **สไตล์การตอบ/ตาราง**. prompt สุดท้ายที่ป้อน LLM = **4 ช่องนี้ + [[#Locked prompt mechanics]]** เสมอ. null/ว่างรายช่อง = ใช้ default ในโค้ด. มีผล **ทันทีแชตรอบถัดไป** | admin แก้กฎเหล็กให้เข้มขึ้น → แชตถัดไปเปลี่ยนเลย; กด "รีเซ็ต" เฉพาะช่องสไตล์ได้ |
| **Locked prompt mechanics** | ส่วน system prompt ที่ **ยังล็อกในโค้ด — admin แก้ไม่ได้** (เหลือแคบลงหลัง [[docs/adr/0014]]): (1) **กลไก sentinel/tool ที่ผูกกับ parser** ([[#`[BUBBLE]` sentinel\|BUBBLE]] / [[#`[SUGGEST]` sentinel\|SUGGEST]] / [[#`coverageInSet`\|coverageInSet]] / mapping `filter_packages`+`prepare_proposal`) — แก้แล้ว **UI/parser พังจริง** ไม่ใช่แค่ compliance; (2) **knowledge injection จากวิกิ** (auto จาก 6 ไฟล์ — wiki = source of truth, โชว์ read-only). compliance ไม่อยู่ในนี้แล้ว → ย้ายไปช่องแก้ได้ ③ (ดู [[docs/adr/0014]] เรื่องความเสี่ยง CLAUDE.md ข้อ 11) | admin แก้ persona/กฎได้ แต่ `[BUBBLE]` และ knowledge วิกิแก้ไม่ได้ |
| **LLM connection settings** | การตั้งค่าเชื่อม LLM ที่ admin แก้ได้: base URL/provider, model, **vision model** (โมเดลอ่านเอกสารแยกสำหรับ [[#Document intake (แนบเอกสารรถ)\|Document intake]] — โมเดลสนทนา text-only อ่านรูปไม่ได้; default `google/gemini-2.5-flash`, ดู [[docs/adr/0018]]), [[#API key (write-only)\|API key]], temperature, max tokens, timeout (= `LlmSettings` ที่ย้ายจาก env/config มาเป็น **runtime-editable**). มีผลทันทีรอบถัดไป. **theme คงเป็น "รอบโหลดถัดไป" ตาม [[docs/adr/0010]]** | admin สลับเป็น gpt-4o-mini → แชตถัดไปใช้โมเดลใหม่ |
| **API key (write-only)** | key ของ LLM provider: admin **ตั้ง/เปลี่ยน**ได้ แต่ `GET` คืนเป็น **masked** (`••••xyz4`) — ไม่เคยส่ง key เต็มกลับ client. เก็บใน [[#app-settings.json]] (gitignored). ถ้า admin ไม่เคยตั้ง → **fallback** ไป env `OPENROUTER_API_KEY` เดิม. ดู [[docs/adr/0011]] | admin วาง key ใหม่ → จอโชว์ `••••a1b2` |
| **App Password (write-only)** | รหัสแอป (Gmail app password) ของบัญชีผู้ส่ง [[#Order email]]: admin **ตั้ง/เปลี่ยน**ได้ แต่ `GET` คืน **masked** — ไม่เคยส่งกลับ client. เก็บใน [[#app-settings.json]]. ไม่เคยตั้ง → **fallback** env `SMTP_PASSWORD`. ว่างทั้งคู่ → mock (ไม่ส่งจริง). ขนานกับ [[#API key (write-only)]]. ดู [[docs/adr/0013]] | admin วาง app password → จอโชว์ `••••wxyz` |
| **Email delivery settings** (การส่งคำสั่งซื้อ) | ช่องทางส่ง [[#Order email]] ที่ [[#Admin panel\|admin]] แก้ได้ runtime: **บัญชีผู้ส่ง** (อีเมล Gmail + [[#App Password (write-only)]]) + **อีเมลปลายทาง** (โบรคเกอร์). host/port ตรึงที่ Gmail; **From ผูก = อีเมลผู้ส่งเสมอ** (Gmail บังคับ From = บัญชีที่ auth → แก้ From แยกไม่ได้). มีปุ่มส่งอีเมลทดสอบ. มีผลทันทีคำสั่งซื้อรอบถัดไป. ขนานกับ [[#LLM connection settings]]. ดู [[docs/adr/0013]] | admin เปลี่ยนผู้ส่งเป็น orders@… → คำสั่งซื้อถัดไปส่งจากบัญชีนั้น |
| **app-settings.json** | store ฝั่ง server (ปรัชญา "JSON now, SQL later" [[docs/adr/0002]]) เก็บ **runtime settings** ที่ admin แก้: [[#App theme\|theme]] + [[#Editable persona]] + [[#LLM connection settings]] + [[#Email delivery settings]]. คู่กับ `users.json`. ไม่ commit (gitignored) | root ของ app/api/ |
| **Prospect / ผู้มุ่งหวัง** | บุคคลที่ถูกเสนอราคา = เจ้าของข้อมูลใน proposal (`custName/email` + รายละเอียดออกกรมธรรม์: `phone/idcard/ทะเบียนรถ`). โหมด agent → ตัวแทนกรอกให้; โหมด customer → คือตัว User เอง | `{custName, email, phone, idcard, plate}` |
| ~~**Self-register**~~ | **ยกเลิก (2026-06-09)** — ไม่มีบัญชีลูกค้าให้สมัครอีกแล้ว (chatbot เป็น public); admin seed อย่างเดียว. `POST /Auth/Register` + หน้า register เลิกใช้. ดู [[docs/adr/0011]] | — |
| **Session** | การสนทนา 1 รอบ — **เก็บลง DB** ทุก session (`Conversations`+`ConversationTurns`, ADR 0016 Phase 2); candidate set ไม่เก็บ (derive จาก KPN ใหม่) | Conversation stream ระหว่าง 10:00–10:15 |
| **Turn** | **1 รอบตอบ** = หน่วยตรรกะ/ความจำ. 1 user message → 1 assistant turn (เก็บใน `context.Turns` ป้อนกลับ LLM ทุกรอบ). _เลี่ยง:_ ใช้ "turn" เรียกกล่องที่แสดง | assistant 1 turn อาจกินพื้นที่หลายกล่อง |
| **Bubble** (กล่องข้อความ) | **1 กล่องที่แสดงผล** = หน่วยนำเสนอ. _เดิม_ (path `/Message` ไม่สตรีม) assistant 1 turn แตกเป็น **หลาย bubble** ได้ (ดู [[#Bubble split]]). _ใหม่_ ([[#Streaming reply]] ผ่าน `/MessageStream`) = **กล่องเดียวโตขึ้นแบบ AI chat**, `[BUBBLE]` กลายเป็นเว้นบรรทัดในกล่องนั้น. _เลี่ยง:_ "แชต"/"ข้อความ" เมื่อหมายถึงกล่องเดียว | สตรีม: 1 กล่องไหลจนจบ + chips ใต้กล่อง |
| **Streaming reply** | คำตอบ assistant ที่ **ไหลทีละ token** ผ่าน `POST /Chatbot/MessageStream` (NDJSON บน `fetch` ReadableStream) แทนรอเต็มก้อนแบบ `/Message`. backend parse sentinel กลางสตรีม → ส่ง [[#Status event]] / `delta` / `suggestions` / `done`. เป็น presentation ล้วน — `context.Turns` ยังเก็บ finalText 1 turn เท่าเดิม. ดู [[docs/adr/0009]] | "กำลังค้นแผน…" → token ไหล → chips โผล่ |
| **Status event** | event ระหว่าง **tool loop** ที่ยังไม่มี prose — บอกผู้ใช้ว่ากำลังทำอะไร (แมพจากชื่อ tool: `filter_packages`→"กำลังค้นแผนประกัน…", `prepare_proposal`→"กำลังเตรียมใบเสนอ…") **ถูกแทนที่ทันทีที่ `delta` แรกไหล** — กัน dead air ตอนโมเดลเรียก tool หลายรอบ. ดู [[docs/adr/0009]] | filter→prepare→พิมพ์: เห็น 2 สถานะก่อนคำตอบ |
| **Conversation history** | ข้อมูลการถาม-ตอบแต่ละ turn ในระหว่าง session | `[{role: "assistant", text: "รถยี่ห้าอะไรครับ?"}, {role: "user", text: "ฮอนดา"}]` |

> ⚠️ **Resolved ambiguity (2026-06-02):** เดิมนิยาม "User = ลูกค้าที่ login" (customer-facing เพียงอย่างเดียว) ซึ่งขัดกับกรอบ CLAUDE.md ที่ว่านี่คือ "เครื่องมือของตัวแทน". มติ: **User มี 2 role** — `agent` (ตัวแทนกรอกข้อมูลลูกค้า) และ `customer` (ลูกค้าขอราคาเอง). proposal identity ขึ้นกับ role. ดู [[docs/adr/0001]]
>
> 🔄 **Superseded (2026-06-09):** มติ 2-role ข้างบน **ถูกกลับ** — chatbot/quote เปลี่ยนเป็น **public ไม่ต้อง login**, role `agent`/`customer` ถูกตัดทิ้ง, login เหลือไว้เฉพาะ `admin` เพื่อเข้า [[#Admin panel]]. proposal identity มาจาก [[#PII intake turn]] ในแชตแทน. ดู [[docs/adr/0011]]
>
> 🔄 **Superseded บางส่วน (2026-06-18, ADR 0016):** ① secret (LLM API key + รหัสแอปอีเมล) ย้ายไป **env เท่านั้น** — admin แก้ key/รหัสผ่านผ่าน UI ไม่ได้แล้ว, key source คืน `env`/`none` (ช่องกรอกใน [[#Admin panel]] เป็น no-op รอเก็บกวาด frontend). ② [[#Session]] เก็บลง DB แล้ว (เลิก session-based MVP). ดู [[docs/adr/0016]]

---

## 🚗 ผลิตภัณฑ์ & ข้อมูล

| ศัพท์ | ความหมาย | แหล่ง |
|------|---------|-------|
| **Package** | แพคเกจประกันรถ 1 รายการ (brand+model+year+class+insurer+premium) | `raw/ข้อมูลแพคเกจรถยนต์_Page*.json` (330,408 รายการ) |
| **Class** | ชั้นประกัน (1, 2+, 3+, 2, 3) — แบ่งตามวงเงินความเสี่ยง | ชั้น 1 = ความครอบคลุมสูงสุด |
| **Sum Insure** | จำนวนวงเงินประกัน (ทีเดดฟูล) | 200,000–500,000 บาท |
| **Premium/เบี้ย** | ราคารายปีต่อแพคเกจ | 14,614–25,000 บาท (ต้อง verify ใน T Broker ก่อนเสนอจริง) ⚠️ |
| **Quote** | ผลการค้นหาแพคเกจ (1 หรือหลาย package match) | HONDA CIVIC 2020 ชั้น 1 = 440 packages |
| **Product knowledge** | ความรู้ผลิตภัณฑ์จากวิกิ (ประกันรถ, ชั้นการเทียบ, ตัวเลือก) | [[ประกันรถยนต์]], [[เปรียบเทียบแผน-จับคู่-need]] |

---

## 💬 Conversation Flow & Methodology

| ศัพท์ | คำนิยาม | การใช้ในแชท |
|------|--------|-----------|
| **Product-first flow** | หลักการ: เก็บรายละเอียดรถ (brand/model/year/class/sum) **ก่อน** ค้นแพคเกจ. โหมด LLM เก็บแบบยืดหยุ่นตามบทสนทนา (ไม่ตายตัว); โหมด fallback (state machine) บังคับลำดับ brand→model→year→class→sum→ask_customer_detail | "รถอะไรครับ?" → ... → เสนอแพคเกจ |
| **Structured intake** (quote form) | ทางเข้าแบบกรอกฟอร์ม **5 ฟิลด์ = ระบุตัวรถ** (ยี่ห้อ/รุ่น/CC/ปี/จังหวัด) → ค้นหา → ได้ [[#Candidate set\|candidate set]] → **เข้าแชตเลย**. เรื่องทุน/ความคุ้มครอง/งบ **ไม่อยู่ในฟอร์ม** แต่ให้ AI [[#Scoping\|สโคป]]ในแชต. ข้อมูลสดจาก KPN DB (ดู [[docs/adr/0004]]) | Honda→Civic→1800→2020→เชียงใหม่ → ค้นหา → แชต |
| **Candidate set** | ชุดแพคเกจ**ทั้งหมด**ที่ตรงตัวรถ+จังหวัด (ทุกชั้น/ทุกบริษัท/ทุกทุน) จากการค้น 5 ฟิลด์ → เก็บไว้ฝั่ง server แล้วป้อนให้ AI กรอง | "พบ 42 แผน จาก 6 บริษัท" |
| **Scoping** | ขั้นที่ AI ถามหา **ทุนประกัน / เบี้ย(งบ) / ทุนสูญหายไฟไหม้ / ภัยน้ำท่วม** แล้วเรียก tool กรอง candidate set ให้เหลือ "ผลิตภัณฑ์ที่ตรงที่สุด" (ชั้น/ผลิตภัณฑ์เป็น**ผลลัพธ์** ไม่ได้ถามตรง) | "งบเบี้ยราวเท่าไหร่คะ?" → filter → เหลือ 3 แผน |
| **Shortlist** | ผลของ `filter_packages` ที่แสดงให้ลูกค้า — เรียงได้ (`sort_by`: เบี้ย/ทุน/ความคุ้มครอง) และปรับจำนวนได้ (`limit` ดีฟอลต์ 3, "ขอทั้งหมด" → clamp ≤ **HardCap 10**). เก็บลง `Proposals` ให้ prepare_proposal/การ์ดใช้. ดู [[docs/adr/0007]] | "ขอทั้งหมด" → แสดง 4/4; ถ้า 42 → "แสดง 10 จาก 42 บอกเกณฑ์เพื่อแคบลง" |
| **Document intake** (แนบเอกสารรถ) | **ทางเข้าที่ 3** (คู่ [[#Structured intake (quote form)\|Structured intake]]) — ลูกค้าแนบ **รูป/PDF เอกสารรถ** (ตารางกรมธรรม์/เล่มทะเบียน) ที่หน้า quote หรือในแชต → [[#Vision extraction\|vision สกัด]] ยี่ห้อ/รุ่น/ปี → [[#Facet resolve\|resolve]] เข้าค่า DB → **ปลายทางคือแชตเสมอ**: ครบ → ค้นแผน+seed / ไม่ครบ → [[#Intake gate\|intake gate]] ถามต่อ. **ephemeral** — ไม่เก็บไฟล์/PII. ดู [[docs/adr/0018]], [[docs/adr/0019]] | แนบตารางกรมธรรม์วิริยะฯ → เด้งเข้าแชตพร้อม KIA · K2700 · 2005 |
| **Vision extraction** | คอล **vision LLM คอลเดียว** ที่ทำทั้ง **classify** (เป็นเอกสารรถไหม — ไม่ใช่ → 422) + **extract** (คืน JSON fields + ตาราง markdown). ใช้ `VisionModel` แยก. รูป **ไม่เข้า** [[#Turn\|context.Turns]] → ไม่แตะ pipeline สตรีม ([[docs/adr/0009]]). ดู [[docs/adr/0018]] | รูปวิริยะฯ → `{isVehicleDocument:true, docType:"policy_schedule", …}` |
| **Facet resolve** | ขั้นแมตช์ค่าที่ vision อ่านได้ (free text) → **ค่า canonical ใน DB** ก่อน prefill (normalize + fuzzy) เพราะฟอร์มค้นด้วยค่าเป๊ะ. แมตช์ไม่ได้ = ส่งต่อให้ [[#Intake gate\|intake gate]] ถาม (safety net กัน vision อ่านผิด). ดู [[docs/adr/0018]] | `"K 2700 M"` → `"K2700"`; ปีไม่ตรง → เว้นว่าง |
| **Intake gate** | ช่วง **ก่อน LLM เข้ามาคุย** ที่ frontend ถามฟิลด์ที่ยังขาด (ยี่ห้อ→รุ่น→ปี→จังหวัด) ด้วย **chip จากค่า facet จริง + พิมพ์เองได้** (พิมพ์เอง → `GET /Motor/ResolveVehicle`). ครบ → ค้นแผน → seed `Candidates` → LLM รับช่วง. จำเป็นเพราะ LLM **ไม่มี tool ค้นหา** (ADR 0005) และถ้า `Candidates` ว่างบอทจะไล่กลับไปหน้าฟอร์ม. ขณะ gate เปิด ข้อความผู้ใช้ **ไม่ยิงเข้า LLM**. ดู [[docs/adr/0019]] | เอกสารได้แค่ KIA → บอทถามรุ่น (chip 18 รุ่น) → ปี → จังหวัด → โชว์แผน |
| **จังหวัด (ทะเบียน)** | จังหวัดที่รถจดทะเบียน — เป็นฟิลด์ที่ 5 ของฟอร์ม **แต่ไม่กรองแพคเกจ/เบี้ย** (ข้อมูลจริง: ทุกแพคเกจ `ProvinId=0` nationwide; KPN live ก็ไม่กรอง — `provinnamePackage="ไม่มีข้อมูล"`). เก็บไว้ลง proposal เท่านั้น | เก็บ "เชียงใหม่" ลง proposal — ผลค้นเท่าเดิม |
| **Grounding** | กฎว่าแชทบอตพูดเบี้ย/ชื่อแผนได้เฉพาะค่าที่ได้จากการค้นจริง (tool) เท่านั้น — ห้ามแต่งเอง (กัน hallucination + คุม คปภ.). **รวมถึงห้ามอ้างว่า "ไม่มี/ทุกแผนไม่คุ้มครอง X" ถ้าใน candidate set จริง ๆ มี** — ต้องดู `coverageInSet` จากผล tool, ถ้ามีแต่ไม่อยู่ในแถวที่แสดง → ให้เสนอกรองหา ([[docs/adr/0007]]) | AI กรอง candidate set ด้วย tool เท่านั้น ไม่เลือก/อ้างเลขเอง |
| **`coverageInSet`** | ฟิลด์ในผล `filter_packages` ที่นับความคุ้มครองจาก candidate **ทั้งชุด** (ไม่ใช่แค่ที่แสดง) เช่น `{fire:3, flood:1}` — เป็น grounding "สดและใกล้" กัน LLM ดริฟต์พูดเท็จเรื่องน้ำท่วม/ไฟไหม้ | flood:1 → "มี 1 แผนคุ้มครองน้ำท่วมนะคะ" |
| **Bubble split** | การแตก assistant **1 [[#Turn\|turn]] → หลาย [[#Bubble\|bubble]]** เพื่อ UX แบบแชต. LLM แทรก sentinel `[BUBBLE]` ตามความหมาย (เนื้อหา/ตาราง แยกจากคำถามปิดท้าย); เป็นเรื่อง **presentation ล้วน** — ไม่รั่วเข้า conversation state. ดู [[docs/adr/0006]] | "ตาราง 8 แผน" 1 กล่อง + "สนใจทุนเท่าไหร่คะ?" อีกกล่อง |
| **`[BUBBLE]` sentinel** | ตัวคั่นบรรทัดเดี่ยวที่ LLM ใส่เพื่อบอกจุดขึ้นกล่องใหม่; backend split + strip ทิ้งก่อนส่ง `Replies[]`. _เลี่ยง:_ ใช้ `---` (ชน markdown hr/ตาราง) | `...ข้อสังเกต\n[BUBBLE]\nสนใจทุนเท่าไหร่คะ` |
| **Suggestion chips** (ปุ่มถามด่วน) | ปุ่มวลีสั้นใต้แชตที่ลูกค้า**กดแทนการพิมพ์** — **chip = สตริงเดียว (โชว์ = ส่ง)** กดแล้วกลายเป็น user message วิ่งเข้า pipeline ที่ grounded ตามปกติ. มาจาก LLM, อิง candidate set, รีเฟรชทุกเทิร์น. **ปิด chips เมื่อ turn นั้นปิดท้ายด้วยการขอ [[#PII intake turn\|PII identity]]** — ให้ลูกค้าพิมพ์เอง (chip เป็น intent ที่มีแต่ลูกค้าตอบได้ → ทำเป็นปุ่มไม่ได้). _เลี่ยง:_ เรียก "quick reply" สลับกับ [[#Bubble]] | ['ขอแผนถูกสุด', 'เพิ่มทุนประกัน', 'ดูทั้งหมด'] |
| **PII intake turn** | turn ที่ **ประโยคปิดท้ายเป็นการขอข้อมูลระบุตัวตนของ [[#Prospect\|Prospect]]** ที่มีแต่ลูกค้าคนนั้นตอบได้ — **ชื่อ / เบอร์ / บัตรประชาชน / อีเมล** (ไม่รวม brand/model/year/plate ที่มาจากฟอร์ม). กฎ: turn แบบนี้ **เว้น `[SUGGEST]` (ไม่มี chips)** + ใช้ถ้อยคำชวน "พิมพ์" ไม่ใช่ "จิ้ม/กดเลย/👇". _ครอบ:_ turn ที่ยืนยันแผน ✅ แล้วปิดท้ายด้วย "บอกชื่อด้วยนะคะ" ก็นับ. _ไม่รวม:_ turn `ready_to_propose` (กดปุ่ม "เสนอแพคเกจ") ที่ยังคง chips ได้ปกติ. ดู [[docs/adr/0008]] | "ก่อนส่งใบเสนอ พิมพ์ชื่อได้เลยค่ะ" → ไม่มีปุ่มใต้แชต |
| **Required identity (ชื่อ+เบอร์)** | **ชื่อ + เบอร์โทร = ข้อมูลบังคับ** ก่อน [[#Confirm]] สำเร็จ (2026-06-10). บอทต้องเก็บผ่าน [[#PII intake turn]] ทันทีหลังลูกค้ายืนยันแผน; ถ้ายังไม่ครบ backend **reject** การกด "เสนอแพคเกจ" พร้อมชวนพิมพ์ในแชต. เบอร์ = strip อักขระไม่ใช่ตัวเลขแล้วต้อง **9–10 หลัก** (กันค่าขยะ/"ไม่บอก"); ชื่อ = ไม่ว่าง. อีเมล/บัตร ปชช. ยัง optional | กดเสนอโดยไม่มีเบอร์ → "ขอชื่อและเบอร์ในแชตก่อนนะคะ" |
| **`[SUGGEST]` sentinel** | บรรทัดท้ายสุดที่ LLM ใส่เพื่อแนบ chips: `[SUGGEST] a \| b \| c`; backend parse → `Suggestions[]` แล้ว strip ตอนแสดง (history คงไว้เป็น few-shot). chips ติดมากับ /Message **ไม่เพิ่ม LLM call**. ตอนเปิดแชต greeting เป็น template instant จึงดึงชุดแรกจาก endpoint แยก `POST /Suggest`. ดู [[docs/adr/0008]] | `...อยากดูไหมคะ\n[SUGGEST] ขอแผนถูกสุด \| ดูทั้งหมด` |
| **Non-motor flow** | (property/PA) เก็บความต้องการ (แบบ/ทุน/ความกังวล) แล้ว **handoff** — ยังไม่มี catalog ราคา จึงไม่ออกราคาอัตโนมัติ | "บ้าน" → แบบครบ → ทุน → กังวลน้ำท่วม → ส่งต่อตัวแทน |
| **Handoff** | จบ flow non-motor ด้วยการเก็บชื่อ/เบอร์ → "ตัวแทนจะติดต่อกลับพร้อมใบเสนอราคา" (step `done`) | ไม่มีปุ่ม "เสนอแพคเกจ" (ต่างจาก motor) |
| **SPIN selling** | Situation → Problem → Implication → Need-Payoff (ฟังมากกว่าพูด) | "ทำไมต้องประกันชั้น 1 ให้ดีกว่า?" = Implication ของ Problem (เสี่ยงสูง) |
| **Deep SPIN** | ใช้คำถาม SPIN ตรงจุดที่ลูกค้ากำลังตัดสิน (เช่น ชั้นประกัน/ทุนประกัน) | "ถ้ารถเสียหาย ต้องใจหนักกว่านี้ไหม?" |
| **Need-Payoff** | ให้ลูกค้าสรุปเอง "เราต้องการ..." (ไม่บอก) | "จากที่คุณบอก ถ้าได้ความมั่นใจทั้งหมด คิดว่าคุ้มค่าไหม?" |
| **Objection handling** | LAARC (Listen-Acknowledge-Ask-Respond-Confirm) + Feel-Felt-Found | ลูกค้า: "เบี้ยแพง" → ตอบ: "ทำความเข้าใจ หลายคนบอกแบบนี้ แต่พอได้เห็นข้อดี..." |
| **Closing** | Price anchoring (เปรียบเทียบค่าใช้จ่ายประจำวัน) | "เบี้ย 14,614 บาท/ปี = 40 บาท/วัน ตรวจสอบความปลอดภัยของครอบครัว" |

---

## 📋 Proposal & Transaction

| ศัพท์ | ความหมาย | API |
|------|---------|-----|
| **Proposal** | ข้อเสนอขายที่ chatbot สร้างขึ้นเพื่อให้ลูกค้ากด [[#Confirm\|"ส่งคำสั่งซื้อ"]] | ส่งเป็น [[#Order email]] ไปโบรคเกอร์ (เดิม POST `motor/check-insure-premiums2-form/add` — เก็บโค้ดไว้แต่ไม่ผูกปุ่ม) |
| **Proposal JSON** | สคีมา: carFeetList (รถ), quotationPremCoverList (เบี้ย), custCode/custName/email | ดูตัวอย่าง (ผู้ใช้ให้มาแล้ว) |
| **Confirm** | ลูกค้ากดปุ่ม **"ส่งคำสั่งซื้อ"** (เดิม "เสนอแพคเกจ") → backend ส่ง [[#Order email]] ไปโบรคเกอร์. **มี gate:** ปฏิเสธถ้า [[#Required identity (ชื่อ+เบอร์)\|ชื่อ/เบอร์]] ไม่ครบ. ดู [[docs/adr/0012]] | เจ้าหน้าที่ติดต่อกลับเพื่อออกกรมธรรม์ |
| **Order email (คำสั่งซื้อ)** | ช่องทางส่งคำสั่งซื้อ (2026-06-10) — backend ประกอบ **ฟอร์ม HTML** (ลูกค้า: ชื่อ/เบอร์/อีเมลถ้ามี + รายละเอียดแพคเกจที่เลือก) แล้วส่งอีเมลไปโบรคเกอร์ (`clickto.helper@gmail.com`) ผ่าน **Gmail SMTP** (MailKit). **แทนที่** partner JSON POST เดิม (mock/live). `SMTP_PASSWORD` ว่าง → mock (log+success); ตั้งแล้ว → ส่งจริง, ส่งล้ม → error. **ต้นทาง (บัญชีผู้ส่ง) + ปลายทาง admin แก้ได้ runtime** ผ่าน [[#Email delivery settings]] (เดิม env-only — amend ผ่าน [[docs/adr/0013]]). ดู [[docs/adr/0012]] | subject "คำสั่งซื้อประกันรถ — คุณสมชาย…" |
| **Pending** | Proposal ที่ยังรออนุมัติจากลูกค้า (ไม่จำเป็นต้องเก็บ session-based MVP) | |
| **Order record** | คำสั่งซื้อที่บันทึกลงตาราง `Orders` (snapshot ลูกค้า/รถ/แผน/ราคา + status pipeline) เพิ่มจากการยิง [[#Order email]] (ADR 0016 Phase 2) | ตาราง Orders |
| **Lead** | conversation ที่มีเบอร์ลูกค้าแล้วแต่ยังไม่มี Order — query เอาจาก `Conversations` (ไม่มีตารางแยก, ADR 0016) | — |

---

## 🔍 Knowledge & Search

| ศัพท์ | ความหมาย | ตัวอย่าง |
|------|---------|---------|
| **Wiki knowledge** | ความรู้จากวิกิ (concepts, playbooks, sources) | [[ประกันรถยนต์]], [[SPIN]], [[รับมือข้อโต้แย้ง]] |
| **Keyword search** | ค้นหาหน้าวิกิโดยใช้ keyword แบบ simple (ไม่ embeddings) | "ชั้น 1" → หาหน้า [[ประกันรถยนต์]] |
| **Motor query** | ค้นแพคเกจรถด้วย `tools/motor/query.py` | `--brand HONDA --model CIVIC --class 1 --year 2020` |
| **Answer synthesis** | สังเคราะห์คำตอบจากวิกิ + query result + SPIN context | "ชั้น 1 ให้ความคุ้มครองเต็ม [wiki] คุณอยากได้หรือ [SPIN]?" |
| **KnowledgeArticle** | ตาราง sync วิกิ (file→DB ทางเดียว) ให้ chatbot keyword-search + admin ดู; ไฟล์ markdown ยังเป็น source of truth (ADR 0016 Phase 3) | ตาราง KnowledgeArticles |

---

## ⚠️ Compliance & Constraint

| ศัพท์ | กฎ | หมายเหตุ |
|------|-----|---------|
| **Verify premium** | เบี้ยทั้งหมดต้อง verify ใน T Broker ก่อนเสนอจริง | Package rate อาจเปลี่ยน |
| **Mis-selling** | ห้ามเสนอแผนที่ไม่ตรงความต้องการลูกค้า (พ.ร.บ.) | ใช้ SPIN ขุด Need ก่อนเสนอ |
| **Data privacy** | ไม่ส่ง PII ลูกค้าออกจากระบบ (เช่น ค้นเว็บ) | ใช้ข้อมูลสมมติเมื่อต้อง demo |
| **Honesty** | ไม่ให้ข้อมูลเท็จ/เกินจริง เรื่อง Return/คุ้มครอง | Unit Linked "ไม่การันตี" ต้องแจ้งความเสี่ยง |

---

## 🔗 ลิงก์เอกสาร

- **Wiki:** `C:\CTD\ChatBot\InsureChat\wiki\`
- **Standalone app — Backend:** repo `insurechat-api/` (ASP.NET Core) — แยกออกตาม ADR 0017
- **Standalone app — Frontend:** repo `insurechat-web/` (Angular) — แยกออกตาม ADR 0017
- **Tools:** `C:\CTD\ChatBot\InsureChat\tools\motor\` (Python CLI) + ข้อมูล 330k ใน `raw\`
- **ADRs:** `C:\CTD\ChatBot\InsureChat\docs\adr\`

> หมายเหตุ: เดิมโค้ด chatbot ฝังอยู่ใน `C:\CTD\KPN\KPN_Backend` (ASP.NET Core) และ `C:\CTD\KPN\KPN_Frontend` (**Angular** — ไม่ใช่ React ตามที่เคยระบุผิด). มติแยกออกมาเป็น standalone ใต้ `InsureChat\app\` ตัดขาดจาก KPN — เหตุผลดู [[docs/adr/0001]]
