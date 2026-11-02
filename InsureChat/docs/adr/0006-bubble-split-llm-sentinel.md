# 0006 — แตกคำตอบเป็นหลายกล่อง (bubble split) ด้วย sentinel จาก LLM

> ♻️ **Superseded เฉพาะ path สตรีม (2026-06-07):** [[docs/adr/0009]] เปลี่ยน UI หลัก (`/MessageStream`) เป็น **กล่องเดียวแบบ AI chat** — `[BUBBLE]` กลายเป็นเว้นบรรทัด, ตัด delay 700ms ทิ้ง. ADR นี้ยังมีผลกับ `/Message` เดิม (state-machine / smoke drivers). `[BUBBLE]` sentinel ยังคงอยู่ (LLM ใส่เหมือนเดิม) — ต่างกันแค่ "วิธีแสดง".

**บริบท:** คำตอบของแชตบอตมักยาว (เกริ่น + ตารางแผน + คำถามชวนต่อ) อัดอยู่ในกล่องเดียวอ่านยาก เราต้องการ UX แบบแชต (LINE/Messenger) ที่แตกเป็น 2-3 กล่องทยอยขึ้น

**มติ:** ให้ **LLM เป็นคนตัดสินจุดแบ่งตามความหมาย** โดยแทรก sentinel `[BUBBLE]` (บรรทัดเดี่ยว) แทนการแบ่งด้วยกฎ markdown ที่ฝั่ง backend — เพราะจุดแบ่งที่ดี (แยก "ข้อมูล/ตาราง" ออกจาก "คำถามปิดท้าย") เป็นเชิงความหมายที่กฎตายตัวจับไม่ได้

backend split + strip `[BUBBLE]` แล้วส่งเป็น `ChatbotMessageResponse.Replies` (`string[]`) — คง `Reply` เดิมไว้เป็น fallback (`string.Join`). frontend ทยอยเผยทีละกล่อง delay คงที่ (~700ms) โดย `loading()` ค้างจนกล่องสุดท้ายขึ้นเสร็จ

**ขอบเขตที่จงใจ (presentation-only):** การแบ่งกล่องเป็นเรื่อง **การนำเสนอล้วน ไม่รั่วเข้า conversation state** — `context.Turns` ยังเก็บคำตอบเป็น **1 turn เดียว (พร้อม sentinel)** ตามนิยาม turn = 1 รอบตอบ ไม่ใช่ 1 กล่อง (ดู CONTEXT.md: [[#Turn]] vs [[#Bubble]]). การคง sentinel ใน history ยังช่วยให้ LLM เห็น pattern คำตอบเก่าของตัวเอง (few-shot โดยธรรมชาติ)

**Guardrail:** prompt กำหนดแบ่งไม่เกิน 3 กล่อง; backend มี safety cap ที่ 4 (ส่วนเกินยุบรวมเข้ากล่องสุดท้าย ไม่ทิ้งเนื้อหา); trim + กรองชิ้นว่างทิ้ง; ถ้าเหลือ 0 ชิ้น → fallback กล่องเดียว. ใช้กับ LLM replies เท่านั้น — greeting/error โผล่กล่องเดียวทันที

**ทางเลือกที่ตัดทิ้ง:** (1) backend แบ่งด้วยกฎ markdown — เสถียรแต่ตัดผิดจุด (เช่นกลางตาราง); (2) แตกเป็นหลาย assistant turn — ทำให้ history บวมและผิดธรรมชาติ; (3) typing indicator คั่นทุกกล่อง (D3) — เนียนกว่าแต่ต้องแยก state typing ระหว่างกล่อง เกินคุณค่าใน MVP (ยกไปทำทีหลังได้)

---

> **อัปเดต (2026-06-07) — multi-box ใช้เฉพาะ path `/Message` ที่ไม่สตรีม.** เมื่อเพิ่ม [[docs/adr/0009]] (streaming reply แบบ AI chat) UI จริงเปลี่ยนเป็น **กล่องเดียวโตขึ้น** และ `[BUBBLE]` กลายเป็น **เว้นบรรทัด/ย่อหน้า** ในกล่องนั้น (ไม่แตกกล่อง, ไม่มี delay 700ms). การแตกหลายกล่องตาม ADR นี้จึงยังใช้กับ `/Message` (state-machine / smoke drivers / client เก่า) เท่านั้น. ส่วนที่ยังจริงทุก path: `[BUBBLE]` เป็น **presentation ล้วน**, `context.Turns` เก็บคำตอบเป็น **1 turn เดียวพร้อม sentinel**, และ LLM เป็นคนวางจุดแบ่งตามความหมาย.
