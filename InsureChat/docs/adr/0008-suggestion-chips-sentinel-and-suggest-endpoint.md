# 0008 — ปุ่มถามด่วน (suggestion chips): sentinel ติดมากับ /Message + /Suggest สำหรับเปิดแชต

**บริบท:** อยากลดแรงเสียดทานตอนเริ่มคุย — ให้ลูกค้า "กดถามได้เลย" ไม่ต้องคิดว่าจะพิมพ์อะไร (แบบ empty-state ของ AI chat composer ทั่วไป) และอยากให้ปุ่ม **อิงข้อมูลจริง** (candidate set: รถอะไร พบกี่แผน มีน้ำท่วม/ไฟไหม้ไหม ช่วงเบี้ย) ไม่ใช่ปุ่ม generic คงที่

**มติ:**
1. **chip = สตริงเดียว (โชว์ = ส่ง)** — กดปุ่มคือพิมพ์วลีนั้นแล้วส่งเป็น user message ทันที. ทำให้ conversation history ซื่อตรง (turn ของ user = สิ่งที่ลูกค้า "พูด" จริง) และ chip วิ่งกลับเข้า pipeline ที่ grounded ตามปกติ. response เพิ่มฟิลด์ `Suggestions: string[]`
2. **chip มาจาก LLM** (ไม่ใช่ static / ไม่ใช่ deterministic จาก candidate set) — เพราะ LLM เห็นทั้ง candidate summary + บทสนทนา จึงเลือกปุ่มที่ตรง step ได้ (ก่อนเห็นตาราง→เน้นเกณฑ์; หลังเห็น shortlist→เน้นเลือก/เทียบ)
3. **รีเฟรชทุกเทิร์น (follow-up chips)** ผ่าน **sentinel `[SUGGEST] a | b | c`** ที่ LLM วางเป็นบรรทัดท้ายสุดของคำตอบ — backend parse + strip แล้วใส่ `Suggestions` ใน response เดิม. **chips มาพร้อมคำตอบ ไม่มี LLM call เพิ่ม ไม่เพิ่ม latency** (ใช้ pattern เดียวกับ `[BUBBLE]` ADR 0006)
4. **ตอนเปิดแชตใช้ endpoint แยก `POST /Chatbot/Suggest`** — เพราะ greeting เป็น **template instant** ฝั่ง frontend (first paint ไว ไม่รอ round-trip) จึงยังไม่มี assistant turn จาก LLM ให้แนบ sentinel. /Suggest รับ context → ยิง LLM นอก tool loop (เบา) → คืน `string[]` อย่างเดียว. frontend แสดง greeting ทันทีแล้วค่อยดึงปุ่มตามมา (ไม่บล็อก)

**ขอบเขตที่จงใจ (presentation-only + grounding):**
- chips เป็น **presentation ล้วน** — strip เฉพาะตอนแสดง, **history คง `[SUGGEST]` ไว้เป็น few-shot** (เหตุผลเดียวกับ `[BUBBLE]` ใน ADR 0006)
- chip เป็น **คำพูดของลูกค้า (intent)** ไม่ใช่ claim — จึงไม่ละเมิด grounding เอง แต่ prompt ยังกำกับว่า **ห้ามชวนถามสิ่งที่ขัด `coverageInSet`** (เช่น ไม่มีแผนน้ำท่วมเลย → ห้ามมีปุ่ม "ขอแผนน้ำท่วม") เพื่อไม่พาลูกค้าชนทางตัน

**Guardrail:** prompt ขอ 2-3 ปุ่ม, ≤20 ตัวอักษร, มุมมองลูกค้า, ไม่มี emoji/`|`; backend cap ที่ 3 (`MaxSuggestions`). parser ของ /Suggest รองรับทั้ง JSON array และ fallback (แยกบรรทัด/`|`). ถ้า LLM ไม่พร้อม/ล่ม → `ChatbotService.FallbackSuggestions` คืนชุด deterministic (`ขอแผนถูกสุด/เพิ่มทุนประกัน/ดูทั้งหมด`) เมื่อมี candidate, คืน `[]` เมื่อไม่มี (สอดคล้องปรัชญา fallback ของ ADR 0003)

**ข้อยกเว้น — PII intake turn (เพิ่ม 2026-06-07):** เมื่อ turn **ปิดท้ายด้วยการขอข้อมูลระบุตัวตนของ Prospect** (ชื่อ/เบอร์/บัตรประชาชน/อีเมล) prompt สั่งให้ **เว้นบรรทัด `[SUGGEST]` (ไม่มี chips)** + ใช้ถ้อยคำชวน "พิมพ์" (ไม่ "จิ้ม/กดเลย/👇"). เหตุผล: chip คือ **intent ที่มีแต่ลูกค้าคนนั้นตอบได้** — ทำเป็นปุ่มสำเร็จรูปไม่ได้ (เดิม LLM แต่งชื่อตัวอย่าง "สมชาย/สมหญิง/สมศรี" มาเป็น chip ซึ่งไร้ความหมายและชวนกดข้อมูลปลอม). **กลไกเป็น prompt-only โดยตั้งใจ** — turn นี้ไม่มี tool call ให้ผูก deterministic guard และ path `/Message` ไม่เติม fallback อยู่แล้ว (เว้น sentinel → `Suggestions=[]` ตรง ๆ) จึงไม่ต้องแตะ backend. ขอบเขตแคบ: **ไม่รวม** turn `ready_to_propose` (ปุ่ม "เสนอแพคเกจ") ที่ยังคง chips ได้

**ทางเลือกที่ตัดทิ้ง:**
1. **ปุ่ม static generic** — ง่ายสุดแต่ไม่รู้ว่า candidate set จริงมีอะไร อาจเสนอสิ่งที่ไม่มี (ขัด grounding) และไม่ตาม step
2. **LLM สร้างปุ่มแบบ structured tool call แยก** — คุมรูปแบบได้ดีกว่า sentinel แต่บังคับ tool iteration เพิ่ม/ออกแบบ schema เกินจำเป็นสำหรับ output ที่เป็นแค่ list สั้น
3. **เรียก /Suggest ทุกเทิร์น (path เดียว)** — สม่ำเสมอ แต่ +1 LLM call/turn (latency+cost ซ้ำซ้อน) และ chips โผล่ช้ากว่า reply; เลือก sentinel ที่ "ฟรี" สำหรับ per-turn แทน
4. **greeting + ปุ่มจาก LLM ในคอลเดียวตอนเปิด** — natural สุดและ path เดียว แต่เสีย first paint ทันที (ต้องรอ round-trip) — เลือกคง greeting template instant ตามความสำคัญของความเร็วเปิดหน้า
5. **kickoff flag บน /Message** — ใช้ path เดียวกับ per-turn แต่ชนกับ tool loop + empty-reply fallback (`"ระบบประมวลผลนานเกินไป"`) และจะฝัง kickoff turn ปลอมเข้า history — แยก endpoint สะอาดกว่า

**ผลที่ตามมา:** มี 2 chip-path (sentinel ใน /Message + /Suggest ตอนเปิด) ที่ต้องคุมกฎ chip ให้ตรงกัน — แก้โดยให้ทั้งคู่อ่านกฎจาก system prompt เดียวกัน (`SystemPromptProvider.Get()`), /Suggest แค่เติมคำสั่ง "ขอเฉพาะ chips เป็น JSON"
