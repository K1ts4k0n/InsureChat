# 0018 — Document intake: แนบเอกสารรถ → vision สกัด (ephemeral) → prefill ฟอร์ม/แชต

ลูกค้าต้องพิมพ์ ยี่ห้อ/รุ่น/ปี เองที่ [[docs/adr/0005|Structured intake 5 ฟิลด์]] ก่อนค้นแผน.
เอกสารรถที่ลูกค้ามีอยู่แล้ว ([[ตารางกรมธรรม์ประกันภัยรถยนต์]], เล่ม/รายการจดทะเบียน) มีข้อมูลนี้ครบ →
เปิดให้ **แนบรูป/PDF แล้วให้ vision LLM อ่านออกมาเป็นตาราง + prefill อัตโนมัติ** เป็น **ทางเข้าที่ 3**
คู่กับฟอร์มและแชต. เอกสารจริงมี **PII หนาแน่น** (ชื่อ/ที่อยู่/เลขตัวถัง) และแชตเป็น **public**
([[docs/adr/0011]]) → บังคับให้เป็น **ephemeral** ตาม CLAUDE.md §11–12.
รายละเอียดเต็มดู [[2026-07-06-document-intake-vision-extraction-design]].

## Considered Options

- **Endpoint สกัดแยกตัวเดียว + ephemeral + facet-resolve (เลือก)** — `POST /Motor/ExtractDocument`
  ที่ quote + chat เรียกร่วมกัน; classify+extract **คอลเดียว** ด้วย `VisionModel` แยก; รูป**ไม่เข้า**
  `context.Turns`; resolve ค่าที่อ่านได้เข้าค่า **canonical facet ใน DB** ก่อน prefill. ไม่แตะ
  pipeline สตรีม/tool-loop ([[docs/adr/0009]]) — `LlmMessage.Content` ยังเป็น `string`.
- **ทำ chat LLM ให้ multimodal เต็มตัว** — *ไม่เลือก*: ต้องเปลี่ยน `LlmMessage.Content` เป็น content-parts,
  รูปกลายเป็น chat turn จริง → แตะ serialization/streaming/tool-loop ที่เป็น load-bearing; และหน้า quote
  ก็ยังต้องมี path แยกอยู่ดี → เสี่ยงสูง ไม่คุ้ม.
- **OCR ฝั่ง client / OCR cloud (Tesseract ฯลฯ)** — *ไม่เลือก*: Thai OCR คุณภาพต่ำ, ไม่มี classify gate,
  เพิ่ม dependency ใหญ่.
- **เก็บไฟล์ไว้ (แนบ order/ให้ตัวแทนดู)** — *ไม่เลือกใน MVP*: ต้องทำ storage + ขอความยินยอม + retention;
  ephemeral ปลอดภัยกว่าและพอสำหรับ prefill.

## Consequences

- **โมเดล vision แยก:** โมเดลสนทนาปัจจุบัน (`deepseek/deepseek-v4-flash`) เป็น text-only อ่านรูปไม่ได้ →
  เพิ่ม `LlmSettings.VisionModel` (default `google/gemini-2.5-flash`, admin แก้ได้ตาม [[docs/adr/0011]]).
  ขยายนิยาม **LLM connection settings** ใน [[CONTEXT.md]].
- **Facet resolve เป็นเงื่อนไขความสำเร็จ:** ฟอร์มค้นด้วยค่า canonical เป๊ะ → ต้อง normalize + fuzzy match
  (`"K 2700 M"`→`"K2700"`); แมตช์ไม่ได้ = เว้นช่องให้ผู้ใช้เลือกเอง (safety net กัน vision อ่านผิด).
- **Privacy (ephemeral 100%):** ถือ `byte[]` ใน memory เฉพาะช่วง call → ทิ้ง; ไม่เขียน disk/DB,
  ไม่ผูก `Orders`/`Conversations`, ไม่ log ภาพ/PII; คืน client เฉพาะ ยี่ห้อ/รุ่น/ปี/cc + ตารางที่ผู้ใช้เห็นเอง.
- **Dependency ใหม่:** NuGet `PDFtoImage` (PDFium) สำหรับ render PDF หน้าแรก → PNG (รับ PDF หน้าแรกเท่านั้น).
- **Guard 2 ชั้น:** client จำกัด type/ขนาด + downscale; server guard ซ้ำ (400) ก่อนเรียก LLM;
  ไม่ใช่เอกสารรถ → 422.
- **ทางเข้าที่ 3:** เพิ่มศัพท์ **Document intake / Vision extraction / Facet resolve** ใน [[CONTEXT.md]]
  คู่กับ Structured intake ([[docs/adr/0005]]).
- **Out of scope (YAGNI):** ไม่รองรับ PDF หลายหน้า, ไม่เก็บไฟล์, ไม่ทำ multimodal ในบทสนทนาแชต,
  ไม่ใช้ฟิลด์ PII (ชื่อ/ที่อยู่) อัตโนมัติ — flow ใช้แค่ ยี่ห้อ/รุ่น/ปี.
