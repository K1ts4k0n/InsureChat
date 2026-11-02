---
title: Document intake — แนบเอกสารรถ → vision สกัด → prefill ฟอร์ม/แชต (ephemeral)
type: design-spec
created: 2026-07-06
updated: 2026-07-06
status: approved
related: [docs/adr/0003, docs/adr/0005, docs/adr/0009, docs/adr/0011, ADR 0018, CONTEXT.md]
---

# Design Spec — Document intake ด้วย vision extraction + facet-resolved prefill

## 1. บริบท & เป้าหมาย (Context & Goal)

ผู้ใช้ต้องการให้ลูกค้า **แนบรูป/PDF เอกสารรถ** (เช่น [[ตารางกรมธรรม์ประกันภัยรถยนต์]] ของวิริยะฯ,
รายการจดทะเบียน) แล้วให้ **vision LLM อ่านรายละเอียดออกมาเป็นตาราง** — แทนการพิมพ์เอง —
เพื่อลดแรงเสียดทานตอนเริ่มหาแผนประกัน

**เป้าหมายจริง (จาก brainstorming):** เอกสารรถมีข้อมูล **ยี่ห้อ/รุ่น/ปี** ครบ ซึ่งตรงกับ
**Structured intake 5 ฟิลด์** ([[docs/adr/0005]]) พอดี → แนบเอกสาร = **prefill ฟอร์มคำนวณเบี้ยอัตโนมัติ**
แล้วเข้า flow ค้นแผน/สโคปเดิมต่อได้ทันที

**ข้อจำกัดที่คงไว้:**
- **ไม่แตะ pipeline สตรีม/tool-loop ของแชต** ([[docs/adr/0009]]) — `LlmMessage.Content` ยังเป็น `string`
- **แชตเป็น public ไม่มี login** ([[docs/adr/0011]]) แต่เอกสารจริงมี **PII หนาแน่น** (ชื่อ/ที่อยู่/เลขตัวถัง)
  → ต้องเป็น ephemeral ตาม CONTEXT §12 privacy + CLAUDE.md §11–12

## 2. Decisions (ล็อกแล้ว — จาก brainstorming 2026-07-06)

| # | Decision | เหตุผล |
|---|----------|--------|
| D1 | **Endpoint สกัดเอกสารแยกตัวเดียว** `POST /api/Motor/ExtractDocument` ที่ quote + chat เรียกร่วมกัน | isolation: vision extraction เป็นคนละ concern กับบทสนทนา; รูปไม่เข้า `context.Turns` |
| D2 | **ผลลัพธ์ = prefill อัตโนมัติ** (เข้าฟอร์มที่แก้ได้ / seed แชต) | ตรง product-first flow; ฟอร์มที่แก้ได้ = safety net กัน vision อ่านผิด |
| D3 | **ปุ่มแนบอยู่ทั้ง 2 ที่** — หน้า quote + ช่องแชต | ลูกค้าเข้าได้ 2 ทาง; entry-point ต่างกัน แต่เรียก endpoint เดียว |
| D4 | **Vision คัดกรองก่อน (classify gate)** ในคอลเดียวกับ extract | classify+extract คอลเดียว = ถูกกว่า 2 คอล; ไม่ใช่เอกสารรถ → 422 |
| D5 | **Ephemeral 100%** — ไม่เขียนไฟล์/PII ลง disk/DB, ไม่ log ภาพ | privacy; เก็บกลับแค่ ยี่ห้อ/รุ่น/ปี ที่ flow ต้องใช้ |
| D6 | **รับรูป + PDF หน้าแรก** (render server-side ด้วย PDFium) | ตรงคำขอ "รูปหรือ pdf"; หน้าแรกพอสำหรับตารางกรมธรรม์ |
| D7 | **โมเดล vision แยก** `LlmSettings.VisionModel` (admin แก้ได้) default = **`google/gemini-2.5-flash`** | โมเดลสนทนาปัจจุบัน (`deepseek/deepseek-v4-flash`) เป็น text-only อ่านรูปไม่ได้ → แยก setting; gemini-2.5-flash อ่านเอกสารไทย/ตารางแม่น + JSON นิ่ง + ถูก |
| D8 | **Resolve เข้าค่า facet ก่อน prefill** — normalize + fuzzy match | ฟอร์มค้นด้วยค่า canonical ใน DB เป๊ะ; `"K 2700 M"`→`"K2700"` ไม่งั้นค้นไม่เจอ |

## 3. สถาปัตยกรรม & Data flow

```
[ปุ่มแนบ: quote page  |  chat composer]
      │  multipart/form-data (image/png,jpeg,webp | application/pdf ≤ 8MB)
      ▼
POST /api/Motor/ExtractDocument          [AllowAnonymous]
  1. Guard: ชนิดไฟล์ + ขนาด            → ผิด → 400 (ก่อนเรียก LLM)
  2. ถ้า PDF → PdfRasterizer render หน้าแรก → PNG bytes
  3. DocumentExtractionService:
        vision LLM 1 call (image_url content-part, VisionModel)
        → JSON { isVehicleDocument, docType, confidence, fields{...}, table[...] }
        → ไม่ใช่เอกสารรถ → 422 + ข้อความไทยสุภาพ
  4. MotorFacet.ResolveVehicle(brand, model, year)
        → canonical { brand, model, year, cc } + matchQuality; แมตช์ไม่ได้ = null
  5. คืน DocumentExtractResponse { isVehicleDocument, docType, table,
                                   resolved, unresolved[], message }
      ✗ ไม่เขียน disk/DB · ไม่ log PII · ปล่อย byte[] ทิ้งหลัง call
      ▼
quote → set brand → trigger cascade → model → year (dropdown เด้งตาม)
        ช่องที่ resolve ไม่ได้เว้นไว้ + toast; strip "อ่านจากเอกสาร: KIA · K2700 · 2005"
chat  → user bubble "📎 แนบเอกสารรถ" (preview local) → assistant bubble ตารางสกัด
        → seed context (brand/model/year) + ค้น candidates → เข้า scoping (เหมือน seedFromQuote)
```

## 4. Backend (สิ่งที่เพิ่ม/แก้)

### 4.1 Controller
- `Controllers/MotorController.cs` → `[HttpPost("ExtractDocument")]` รับ `IFormFile file` (multipart),
  คืน `DocumentExtractResponse`. public `[AllowAnonymous]` เหมือน chatbot

### 4.2 Services ใหม่ (`Services/Document/`)
- **`IDocumentExtractionService` / `DocumentExtractionService`**
  - รับ `byte[] imagePng, string mimeType` → เรียก OpenAI-compatible endpoint โดย **สร้าง request เอง**
    ที่มี content-parts `[{type:"text",...},{type:"image_url",image_url:{url:"data:image/png;base64,..."}}]`
  - reuse effective LLM settings (BaseUrl/ApiKey) แต่ใช้ `VisionModel`
  - prompt: สั่งคืน **JSON เดียว** ที่มีทั้ง `isVehicleDocument` (gate) + `fields` + `table`
  - **ไม่ยุ่งกับ `LlmMessage`/`IChatCompletionClient`** — แยก DTO ของตัวเอง
- **`IPdfRasterizer` / `PdfRasterizer`** — PDF bytes → PNG หน้าแรก (NuGet `PDFtoImage` บน PDFium)

### 4.3 Motor facet
- `Services/Motor/IMotorFacetService` → เพิ่ม `ResolveVehicle(brand, model, year)`
  - brand: normalize (upper, strip `.`/space) match กับ `brands()`
  - model: ใน `models(brand)` หา best match ด้วย normalized-contains / token-overlap
  - year: ตรงถ้ามีใน `years(brand,model)`, ไม่งั้น null
  - คืน `{ brand, model, year, cc, brandOk, modelOk, yearOk }`

### 4.4 Settings
- `LlmSettings` → เพิ่ม `VisionModel` (string) + effective resolution
  (default = `google/gemini-2.5-flash`; admin override ได้)
- Admin LLM tab (`Models/Admin/AppSettings.cs` + หน้า admin) → ช่องกรอก Vision model
  (มีผลรอบถัดไป ตาม [[docs/adr/0011]])

### 4.5 DTO (`Models/Motor/` หรือ `Models/Document/`)
```
DocumentExtractResponse {
  bool   isVehicleDocument
  string docType            // "policy_schedule" | "registration" | "tax" | "other"
  string table              // markdown ตาราง (สำหรับโชว์ในแชต)
  Resolved resolved         // { brand, model, year, cc, brandOk, modelOk, yearOk }
  string[] unresolved       // ["model"] ช่องที่อ่าน/แมตช์ไม่ได้
  string message            // ข้อความสรุป/แจ้งเตือน (ไทย)
}
```

## 5. Frontend (สิ่งที่เพิ่ม/แก้)

### 5.1 Core
- `core/motor-facet.ts` → `extractDocument(file: File): Observable<DocumentExtractResponse>` (multipart)
- util จำกัด type/ขนาด + downscale รูปใหญ่ก่อนส่ง (canvas → blob, ประหยัด token)

### 5.2 หน้า quote (`features/quote/`)
- ปุ่ม "📄 แนบเอกสารรถ (อ่านให้อัตโนมัติ)" ด้านบนการ์ดยี่ห้อ + `<input type=file hidden>`
- handler: upload → spinner → สำเร็จ:
  `selectBrand(resolved.brand)` → cascade โหลด model → `selectModel` → `selectYear`
  (ใช้เมธอด cascade เดิม `onBrandChange/onModelChange` — ลอจิก facet ไม่เปลี่ยน)
  ช่อง `unresolved` เว้นไว้ + toast "อ่าน 'รุ่น' ไม่ชัด เลือกเองนะคะ"
- strip ยืนยัน "อ่านจากเอกสาร: {brand} · {model} · {year}"
- classify-reject/error → error() เดิมของหน้า

### 5.3 แชต (`features/chatbot/`)
- ไอคอน 📎 ใน `<footer class="chat-input">` ข้าง textarea + `<input type=file hidden>`
- handler: push user bubble "📎 แนบเอกสารรถ" → POST extract →
  - สำเร็จ: push assistant bubble = `response.table` (markdown) + seed `conversationContext`
    (brand/model/year + candidates ผ่าน facet.search) → เข้า scoping เหมือน `seedFromQuote`
  - reject/error: assistant bubble ขอโทษ + ชวนแนบใหม่/พิมพ์เอง
- disable ปุ่มตอน `loading()`

## 6. คัดกรอง + Error handling

| กรณี | HTTP | พฤติกรรม |
|------|------|----------|
| ไฟล์ผิด type / เกินขนาด | 400 | reject ก่อนเรียก LLM (client ก็ guard ชั้นแรก) |
| ไม่ใช่เอกสารรถ (classify) | 422 | ข้อความไทย "ดูเหมือนไม่ใช่เอกสารรถ ลองแนบตารางกรมธรรม์/เล่มทะเบียนนะคะ" |
| vision timeout/ล่ม | 502/500 | ข้อความไทย ไม่ prefill |
| resolve บางส่วน | 200 | prefill เท่าที่ได้ + `unresolved[]` ให้ผู้ใช้เลือกเอง |
| โมเดล vision ไม่ตั้ง/ไม่รองรับรูป | 500 | log ฝั่ง server (admin-facing) + ข้อความไทยทั่วไปฝั่งผู้ใช้ |

## 7. Privacy / Compliance (CONTEXT §12, CLAUDE.md §11–12)

- ephemeral 100%: ถือ `byte[]` ใน memory เฉพาะช่วง call → ปล่อยทิ้ง
- ไม่เขียนไฟล์ลง disk/DB, ไม่ผูกกับ `Orders`/`Conversations`
- ไม่ log ภาพหรือ PII (log เฉพาะ metadata: docType, success/fail, ขนาด)
- คืนกลับ client เฉพาะ ยี่ห้อ/รุ่น/ปี/cc + ตารางที่ผู้ใช้เห็นเอง (เอกสารของเจ้าตัว)

## 8. Testing (`InsureChatBot.Api.Tests`)

- mock `IDocumentExtractionService`/vision client (deterministic):
  - classify `isVehicleDocument=false` → 422
  - `ResolveVehicle`: `"K 2700 M"`→`"K2700"`, brand normalize, year miss → null
  - guard: type ผิด / ขนาดเกิน → 400
- `PdfRasterizer`: integration (PDF ตัวอย่าง 1 หน้า → PNG ไม่ว่าง)
- ไม่ทดสอบด้วย LLM จริง (non-deterministic) — golden ใช้ mock

## 9. เอกสารที่จะอัปเดต (grill-with-docs)

- **ADR 0018** — "Document intake via ephemeral vision extraction + facet-resolved prefill"
  (บันทึก D1–D8, ยืนยัน "ไม่แตะ multimodal ใน chat pipeline")
- **CONTEXT.md** — เพิ่มศัพท์:
  - **Document intake** = ทางเข้าที่ 3 (คู่ Structured intake [[docs/adr/0005]]) แนบเอกสารรถ → prefill
  - **Vision extraction** = classify+extract คอลเดียว, ephemeral
  - **Facet resolve** = แมตช์ค่าที่อ่านได้เข้าค่า canonical ใน DB ก่อน prefill
  - ขยาย **LLM connection settings** ให้รวม `VisionModel`

## 10. ขอบเขตที่ "ยังไม่ทำ" (Out of scope — YAGNI)

- ไม่รองรับ PDF หลายหน้า (หน้าแรกพอ)
- ไม่เก็บ/แนบไฟล์เข้า order
- ไม่ทำ multimodal ในบทสนทนาแชต (ถามต่อด้วยรูปกลางแชต)
- ไม่สกัดฟิลด์ PII (ชื่อ/ที่อยู่) ไปใช้อัตโนมัติ — โชว์ในตารางได้ แต่ flow ใช้แค่ ยี่ห้อ/รุ่น/ปี
