# 0019 — Chat intake gate: frontend เก็บฟิลด์ที่ขาดก่อน seed LLM

[[docs/adr/0018|Document intake]] แนบเอกสารรถแล้ว prefill ยี่ห้อ/รุ่น/ปี ได้ แต่เมื่อ **resolve ไม่ครบ**
(เช่น อ่านได้แค่ยี่ห้อ เพราะรุ่นไม่มีในแคตตาล็อก) เกิด **ทางตัน** ทั้งที่หน้า quote (ปล่อยให้กรอกฟอร์มเอง)
และในแชต (บอทพิมพ์ "บอกยี่ห้อ/รุ่น/ปี เพิ่มได้เลยค่ะ" แต่ไม่มีอะไรรับคำตอบไปค้นต่อ).

ข้อจำกัดที่บังคับรูปทรงของทางแก้: ตาม [[docs/adr/0005]] **frontend เป็นคนค้นแผน → AI แค่ "กรอง" `Candidates`**.
LLM มีแค่ `filter_packages` + `prepare_proposal` — **ไม่มี tool ค้นหา** — และถ้า `Candidates` ว่าง
system prompt สั่งบอทว่า *"แนะนำให้กลับไปกรอกข้อมูลรถที่หน้าคำนวณเบี้ยก่อน"* (`CandidateSummary`)
ซึ่งขัดกับเป้าหมาย "แนบแล้วคุยต่อในแชตได้เลย" โดยตรง.
จึงตัดสินใจให้ **การถามเก็บฟิลด์ที่ขาดเกิดก่อน LLM เข้ามาเกี่ยวข้อง**.
รายละเอียดเต็มดู [[2026-07-10-chat-intake-gate-design]].

## Considered Options

- **Intake gate ฝั่ง frontend ก่อน seed LLM (เลือก)** — แชตถามทีละฟิลด์ (ยี่ห้อ→รุ่น→ปี→จังหวัด)
  ด้วย **chip จากค่า facet จริง + พิมพ์เองได้** (พิมพ์เอง → `GET /Motor/ResolveVehicle` ซึ่ง reuse
  `VehicleResolver`), พอครบ → `facet.search()` → `seedContext()` → LLM รับช่วงโดยมี `Candidates` ครบ.
  **ไม่แตะ [[docs/adr/0005]]**, LLM ไม่ต้องเดาชื่อรุ่น, backend เพิ่มแค่ endpoint บาง ๆ 1 ตัว.
  reuse แถว suggestion chips เดิม ([[docs/adr/0008]]) เป็น UI — ไม่ต้องทำคอมโพเนนต์ใหม่.
- **ให้ LLM ถาม+ค้นเอง (เพิ่ม tools `list_models` / `search_packages`)** — *ไม่เลือก*: พลิก [[docs/adr/0005]]
  (AI กลายเป็นคนค้น), LLM มโนชื่อรุ่นที่ไม่มีใน DB ได้, เพิ่ม roundtrip/ค่าโทเคนต่อเทิร์น, ต้องรื้อ prompt.
- **Backend endpoint คืน "คำถามถัดไป + ตัวเลือก"** — *ไม่เลือก*: รวม logic ไว้ server แต่เพิ่ม endpoint + state
  โดยที่ frontend ก็ยังต้อง render chip อยู่ดี — machinery เกินความจำเป็น.
- **คงพฤติกรรมเดิม (กลับไปกรอกฟอร์ม)** — *ไม่เลือก*: ขัดเป้าหมาย และทิ้งทางตันของ [[docs/adr/0018]] ไว้.

## Consequences

- **LLM ถูกกันออกจาก gate:** ขณะ gate เปิด `sendMessage` **ไม่ยิง `/MessageStream`** แต่ป้อนคำตอบเข้า gate
  — กันบอทตอบ "กลับไปหน้าคำนวณเบี้ย" ตอนยังไม่มี `Candidates`.
- **หน้า quote เปลี่ยนพฤติกรรม:** `applyExtraction` **เลิกเติมฟอร์ม** → ส่ง handoff แล้ว `navigate('/chat')`
  (ปลายทางของ Document intake คือ **แชตเสมอ**). ฟอร์มกรอกเองยังทำงานเหมือนเดิม.
- **จังหวัดเคย "ตกหล่น":** `quote` ใส่ `province` ลง handoff แต่ `seedFromQuote` ทิ้ง และ
  `ChatbotConversationContext` ไม่มีฟิลด์นี้ → round-trip แล้วหาย. ADR นี้เพิ่ม
  **`Province` / `ProvinceLabel`** ลง context เพื่อให้ค่าที่ถามมามีที่เก็บจริง.
- **Endpoint ใหม่:** `GET /api/Motor/ResolveVehicle` — wrapper บาง ๆ ของ `ResolveVehicleAsync`
  ใช้ fuzzy-match คำตอบที่ผู้ใช้พิมพ์เอง → **ไม่ duplicate ลอจิก normalize/fuzzy ลง TypeScript**.
- **DRY:** ลอจิก "ยอดนิยม 8 ตัว" (ยี่ห้อ/จังหวัด) ย้ายจาก private ใน `quote.ts` → `core/motor-popular.ts`
  ให้ทั้ง tile ของ quote และ chip ของแชตใช้แหล่งเดียว.
- **ปุ่ม 📎 ในแชต** ใช้ gate เดียวกัน → อุดทางตันที่ [[docs/adr/0018]] ทิ้งไว้.
- **หน่วยที่เทสต์ได้:** `intake-gate.ts` เป็น pure function (`nextField`/`questionFor`) → unit test ตรง ๆ.
- **Out of scope (YAGNI):** ไม่เพิ่ม tool ค้นหาให้ LLM, ไม่ทำ gate ของ CC (ฟอร์มตัด step CC ไปแล้ว),
  ไม่แตะ state machine engine.
