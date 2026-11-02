---
title: Chat intake gate — แนบเอกสารแล้วเด้งเข้าแชต, ข้อมูลที่ขาดให้แชตถามต่อ
type: design-spec
created: 2026-07-10
updated: 2026-07-10
status: approved
related: [docs/adr/0005, docs/adr/0008, docs/adr/0011, docs/adr/0018, ADR 0019, 2026-07-06-document-intake-vision-extraction-design]
---

# Design Spec — Chat intake gate (เก็บฟิลด์ที่ขาดในแชต ก่อนส่งต่อ LLM)

## 1. บริบท & เป้าหมาย (Context & Goal)

[[docs/adr/0018|Document intake]] ทำให้แนบเอกสารรถแล้ว prefill ยี่ห้อ/รุ่น/ปี ได้ แต่
**เมื่อ resolve ไม่ครบ (เช่น อ่านได้แค่ยี่ห้อ)** ปัจจุบันเกิดทางตัน 2 ที่:

1. **หน้า quote** — เติมช่องที่ resolve ได้ แล้วปล่อยให้ผู้ใช้กรอกที่เหลือเองในฟอร์ม
2. **ในแชต (📎)** — บอทพิมพ์ "บอกยี่ห้อ/รุ่น/ปี เพิ่มได้เลยค่ะ" แต่ **ไม่มีอะไรรับคำตอบไปค้นต่อ**

**เป้าหมาย:** แนบเอกสารที่หน้า quote → **เด้งเข้าแชตทันที** → ถ้าฟิลด์ไหนขาด **ให้แชตถามเก็บให้ครบ**
แล้วค้นแผน + เข้าสู่บทสนทนาสโคปตามปกติ

### ข้อจำกัดจากสถาปัตยกรรมเดิม (สำคัญที่สุด)
ตาม [[docs/adr/0005]]: **frontend เป็นคนค้นแผน → ส่ง `Candidates` ให้ AI "กรอง" เท่านั้น**
- LLM มีแค่ 2 tools: `filter_packages` (กรอง candidate set) + `prepare_proposal`
- **ไม่มี tool ค้นหา** → แชตหาแผนเองไม่ได้ถ้าไม่มี candidate set
- ถ้า `Candidates` ว่าง system prompt สั่งบอทว่า *"แนะนำให้กลับไปกรอกข้อมูลรถที่หน้าคำนวณเบี้ยก่อน"*
  (`LlmConversationEngine.CandidateSummary`) → **ขัดกับเป้าหมายโดยตรง**

→ ดังนั้นการ "ถามเก็บฟิลด์ที่ขาด" ต้องเกิด **ก่อน** LLM เข้ามาเกี่ยวข้อง

## 2. Decisions (ล็อกแล้ว — จาก brainstorming 2026-07-10)

| # | Decision | เหตุผล |
|---|----------|--------|
| D1 | **Intake gate ฝั่ง frontend** ถามฟิลด์ที่ขาดก่อน seed LLM | ไม่แตะ [[docs/adr/0005]]; LLM ไม่ต้องเดาชื่อรุ่น; ไม่ต้องเพิ่ม tool |
| D2 | **ผสม chip + พิมพ์เอง** — chip มาจากค่า facet จริง, พิมพ์เองได้แล้ว fuzzy-match | ปลอดภัย (ค่า canonical) แต่ยืดหยุ่น |
| D3 | **ถาม 4 ฟิลด์: ยี่ห้อ → รุ่น → ปี → จังหวัด** (ลำดับเดียวกับฟอร์ม quote) | สอดคล้อง [[docs/adr/0005]] 5-field intake |
| D4 | **ยี่ห้ออ่านไม่ได้ → ไปแชตแล้วให้พิมพ์เอง** (+ chip ยอดนิยม 8) ไม่โชว์ chip 37 ตัว | กันรก; ยี่ห้อพิมพ์ง่ายกว่ารุ่น |
| D5 | **เพิ่ม `Province`/`ProvinceLabel` ลง `ChatbotConversationContext`** (backend) | ปัจจุบัน quote ใส่จังหวัดลง handoff แต่ `seedFromQuote` ทิ้ง + backend ไม่มีฟิลด์ → round-trip แล้วหาย. ถ้าจะถาม ต้องเก็บได้จริง |
| D6 | **reuse `GET /Motor/ResolveVehicle`** (wrapper ของ `ResolveVehicleAsync`) สำหรับคำตอบที่พิมพ์เอง | reuse `VehicleResolver` ที่มีแล้ว — ไม่ duplicate fuzzy logic ลง TypeScript |
| D7 | **หน้า quote: `applyExtraction` เลิกเติมฟอร์ม → ส่ง handoff + navigate `/chat`** | ตรงเป้าหมาย "แนบแล้วไปแชตเลย"; ฟอร์มยังกรอกเองได้ปกติ |
| D8 | **reuse แถว suggestion chips เดิม** ([[docs/adr/0008]]) เป็น UI ตัวเลือกของ gate | ไม่ต้องทำ UI ใหม่, กลมกลืนกับแอป |
| D9 | **gate เปิดอยู่ → `sendMessage` ไม่ยิง `/MessageStream`** แต่ป้อนคำตอบเข้า gate | กัน LLM ถูกเรียกตอนไม่มี candidates (จะได้ข้อความ "กลับไปหน้าคำนวณเบี้ย") |

## 3. Data flow

```
[หน้า quote] กด "แนบเอกสารรถ" → POST /Motor/ExtractDocument
   ├─ 400 / 422 / 502 → อยู่หน้า quote, โชว์ error (ไม่ navigate)
   └─ 200 → DocIntakeHandoff.set({ table, resolved, province(จาก IP) })
            router.navigate(['/chat'])
                    │
[หน้า chat] ngOnInit → DocIntakeHandoff.consume()
   ├─ push กล่อง "ตารางเอกสาร" (markdown)
   ├─ ครบ brand+model+year? ──yes──→ facet.search() → seedContext() → LLM คุยต่อ (เดิม)
   └─ ไม่ครบ → INTAKE GATE (frontend-owned, LLM ยังไม่ถูกเรียก)
        ยี่ห้อ   → พิมพ์เอง (+chip ยอดนิยม 8) → GET /Motor/ResolveVehicle
        รุ่น     → chip จาก GET /Motor/Models?brand=      (+พิมพ์เอง → ResolveVehicle)
        ปี       → chip จาก GET /Motor/Years?brand=&model=&cc=  (+พิมพ์เอง)
        จังหวัด  → chip 8 ยอดนิยม (จังหวัดจาก IP มาก่อน)   (+พิมพ์เอง)
        → ครบ → facet.search()
                 ├─ rows > 0 → seedContext(brand, model, year, rows) → LLM รับช่วง
                 └─ rows = 0 → บอทแจ้ง "ไม่พบแผน" + วนถามรุ่น/ปีใหม่
```

## 4. Backend (เพิ่มน้อย)

### 4.1 endpoint ใหม่ (thin wrapper)
- `Controllers/MotorController.cs` → `[HttpGet("ResolveVehicle")]`
  รับ `brand`, `model`, `year` (nullable) → เรียก `_facets.ResolveVehicleAsync(...)`
  → คืน `VehicleResolveResult` (Brand/Model/Year + BrandOk/ModelOk/YearOk)
  ใช้ pattern `Guarded(...)` เดิมของ controller

### 4.2 context เพิ่มฟิลด์จังหวัด
- `Models/Chatbot/ChatbotModels.cs` → `ChatbotConversationContext` เพิ่ม:
  ```csharp
  public string Province { get; set; }        // รหัสจังหวัดจดทะเบียน (ADR 0005 — เก็บลง proposal, ไม่กรองผล)
  public string ProvinceLabel { get; set; }
  ```
  (ไม่แตะ engine/tool — แค่ทำให้ค่าไม่หายตอน round-trip)

## 5. Frontend

### 5.1 `core/doc-intake-handoff.ts` (ใหม่, เล็ก — หน้าที่เดียว)
one-shot handoff จาก quote → chat:
```ts
export interface DocIntakeData {
  table: string;                 // markdown ตารางเอกสาร
  resolved: ResolvedVehicle;     // brand/model/year + *Ok
  province?: string;             // จาก IP detect ที่หน้า quote (อาจว่าง)
  provinceLabel?: string;
}
```
`set()` / `consume()` เหมือน `QuoteHandoff` (ไม่ปนกัน — `QuoteHandoff` ส่ง candidates ที่ค้นแล้ว)

### 5.2 `features/chatbot/intake-gate.ts` (ใหม่, pure → unit-testable)
```ts
export type IntakeField = 'brand' | 'model' | 'year' | 'province';
export interface IntakeKnown {
  brand?: string | null; model?: string | null; year?: number | null; province?: string | null;
}
/** ฟิลด์ถัดไปที่ต้องถาม (ตามลำดับ brand→model→year→province); null = ครบแล้ว */
export function nextField(known: IntakeKnown): IntakeField | null;
/** ข้อความคำถามภาษาไทยของแต่ละฟิลด์ */
export function questionFor(field: IntakeField, known: IntakeKnown): string;
```
ไม่พึ่ง DOM/HTTP → ทดสอบได้ตรง

### 5.3 `core/motor-facet.ts`
เพิ่ม `resolveVehicle(brand, model?, year?): Observable<VehicleResolveResult>` → `GET /Motor/ResolveVehicle`

### 5.3.1 `core/motor-popular.ts` (ใหม่ — DRY)
ลอจิก "ยอดนิยม 8 ตัว" ปัจจุบันเป็น **private ใน `quote.ts`** (`brandRank`/`brandTiles`, `popProvinces`/`provinceTiles`)
ถ้าแชตจะโชว์ chip ชุดเดียวกันจะเกิดโค้ดซ้ำ → **ย้ายออกมาเป็น util ร่วม**:
```ts
export function topBrands(all: FacetItem[], n = 8): FacetItem[];
export function topProvinces(all: FacetItem[], detectedValue: string, n = 8): FacetItem[];
```
`quote.ts` เปลี่ยนมาเรียก util นี้แทน (พฤติกรรม tile เดิมต้องไม่เปลี่ยน), `chatbot.ts` ใช้ตัวเดียวกันสร้าง chip
— เป็นการปรับปรุงเฉพาะโค้ดที่เรากำลังแตะ ไม่ refactor เกินขอบเขต

### 5.4 `features/chatbot/chatbot.ts`
- state ใหม่: `intake = signal<IntakeKnown | null>(null)` (null = gate ปิด)
- `ngOnInit`: consume `DocIntakeHandoff` ก่อน `QuoteHandoff`
  → push table bubble → `startIntakeOrSeed(resolved, province)`
- `startIntakeOrSeed()`: ครบ → `searchAndSeed()`; ไม่ครบ → เปิด gate + `askNext()`
- `askNext()`: push คำถาม (assistant bubble) + set `suggestions()` = ตัวเลือกจริง (chip)
  - brand → 8 ยอดนิยม, model → `facet.models(brand)`, year → `facet.years(brand,model,'')`,
    province → 8 ยอดนิยม (IP มาก่อน)
- `sendMessage()` **แตกทาง**: ถ้า `intake()` ไม่ null → `answerIntake(text)` (ไม่ยิง `/MessageStream`)
- `sendSuggestion(text)`: ถ้า gate เปิด → `answerIntake(text)` (chip = คำตอบ)
- `answerIntake(text)`:
  - brand/model/year → `facet.resolveVehicle(...)` เพื่อ map เป็นค่า canonical
    (chip ส่งค่า canonical อยู่แล้ว → resolve คืนค่าเดิม; พิมพ์เอง → fuzzy)
  - แมตช์ไม่ได้ → บอทขอใหม่ + โชว์ chip เดิมซ้ำ
  - province → เก็บ label/value ตรง ๆ (ไม่ต้อง resolve)
  - ครบ → `searchAndSeed()`
- `searchAndSeed()`: `facet.search({brand,model,cc:'',year,sort:'prem'})`
  - rows > 0 → ปิด gate → `seedContext(brand, model, year, rows)` + ใส่ `province`/`provinceLabel` ลง context
  - rows = 0 → บอทแจ้ง + ถอยกลับไปถาม `model` ใหม่
- ปุ่ม 📎 ในแชต (`applyDocToChat`) → เรียก `startIntakeOrSeed()` ตัวเดียวกัน (อุดทางตันเดิม)

### 5.5 `features/quote/quote.ts`
- `applyExtraction(r)` เปลี่ยนเป็น:
  ```
  docIntake.set({ table: r.table, resolved: r.resolved, province: this.province, provinceLabel: <label> });
  this.router.navigate(['/chat']);
  ```
  (เลิกเติมฟอร์ม/เลิก cascade prefill; ฟอร์มกรอกเองยังทำงานเหมือนเดิม)
- ลบ `docNote` signal ที่ไม่ได้ใช้แล้ว

## 6. Error handling

| กรณี | พฤติกรรม |
|------|----------|
| extract 400/422/502 | อยู่หน้า quote, `error.set(...)` (ไม่ navigate) |
| `ResolveVehicle` ล่ม | บอทขอให้เลือกจาก chip แทนการพิมพ์ |
| พิมพ์แล้วแมตช์ไม่ได้ | บอทบอก "ไม่พบรุ่นนี้" + โชว์ chip เดิมซ้ำ |
| `facet.search` = 0 แถว | บอทแจ้ง + ถอยกลับถามรุ่น/ปีใหม่ |
| gate เปิดอยู่ | ปุ่ม 📎 ปิด; `sendMessage` ไม่ยิง LLM |

## 7. Testing

- **`intake-gate.ts`** (pure) → unit: `nextField` ทุกคอมบิเนชัน (ขาดยี่ห้อ / ขาดรุ่น / ครบ → null), `questionFor`
- **`ResolveVehicle` endpoint** → controller test ด้วย fake `IMotorFacetService` (คืน canonical / คืน null)
- **`motor-popular.ts`** → ยืนยัน `topBrands`/`topProvinces` คืนลำดับเดิมกับ tile ของ quote (กัน regression ตอนย้าย)
- **Manual E2E:** แนบ `รายการจดทะเบียน.png` (KIA, ขาดรุ่น/ปี) → เด้งเข้าแชต → เห็นตาราง →
  ถามรุ่น (chip 18 รุ่น KIA) → ถามปี → ถามจังหวัด → เห็นแผน → LLM สโคปต่อ

## 8. เอกสารที่จะอัปเดต

- **ADR 0019** — "Chat intake gate: frontend เก็บฟิลด์ที่ขาดก่อน seed LLM (คง ADR 0005)"
- **CONTEXT.md** — เพิ่มศัพท์ **Intake gate**; ขยาย **Document intake** ว่าปลายทางคือแชตเสมอ

## 9. Out of scope (YAGNI)

- ไม่เพิ่ม tool ค้นหาให้ LLM (คง [[docs/adr/0005]])
- ไม่ทำ gate สำหรับ CC (ฟอร์มตัด step CC ออกแล้ว — ส่ง `cc=''`)
- ไม่ย้ายการถามจังหวัดไปตอน proposal (เก็บใน gate ครั้งเดียว)
- ไม่แตะ state machine engine (fallback path)
