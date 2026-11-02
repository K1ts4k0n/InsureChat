# 0009 — สตรีมคำตอบแชต (streaming reply): NDJSON บน POST + tool-progress + กล่องเดียวแบบ AI chat

**บริบท:** คำตอบแชตใช้เวลา ~30–60 วิ/ข้อความ และ 1 เทิร์นอาจเรียก LLM **หลายรอบ** (tool loop: `filter_packages` → `prepare_proposal` → ข้อความสุดท้าย — ดู [[docs/adr/0003]]). ปัจจุบัน `/Chatbot/Message` รอจน LLM ตอบเต็มก้อน แล้วค่อย parse `[BUBBLE]`/`[SUGGEST]` และส่งกลับทีเดียว → ผู้ใช้นั่งมองหน้าจอเงียบ ๆ ทั้งเทิร์น. ต้องการ UX แบบ AI chat ทั่วไป (ChatGPT/Claude): เห็นคำตอบไหลทีละ token และไม่มีช่วงเงียบ

**มติ:**
1. **เพิ่ม `POST /Chatbot/MessageStream` แยกจาก `/Message`** — `/Message` (non-stream, คืน `Replies[]`+`UpdatedContext` ทีเดียว) คงไว้ให้ state-machine smoke drivers / client เก่า / regression. UI จริงย้ายไป `/MessageStream`. ไม่แตะ contract เดิม
2. **Transport = NDJSON บน fetch ReadableStream** (1 JSON object/บรรทัด) — **ไม่ใช้ `EventSource`** เพราะมันรับ POST ไม่ได้ และ `conversationContext` เป็น body ก้อนใหญ่ที่ต้อง POST. event มี 5 ชนิด:
   - `{type:"status", text}` — ระหว่าง tool loop (ดูข้อ 3)
   - `{type:"delta", text}` — ชิ้นข้อความคำตอบ (สะอาดแล้ว: strip sentinel ออก)
   - `{type:"suggestions", items}` — chips จาก `[SUGGEST]`
   - `{type:"done", context, isReadyToPropose}` — แบก **state กลับ** (token ล้วนพา state ไม่ได้)
   - `{type:"error", message}`
3. **tool-progress (กำจัด dead air):** รอบที่โมเดลสั่ง tool ไม่มี prose ให้สตรีม → ส่ง `status` ที่ **แมพ deterministic จากชื่อ tool** (`filter_packages`→"กำลังค้นแผนประกัน…", `prepare_proposal`→"กำลังเตรียมใบเสนอ…"). `status` **ถูกแทนที่ทันทีที่ `delta` แรกมา** — ไม่ค้างใน history
4. **กล่องเดียวโตขึ้นแบบ AI chat** — ในสตรีม `[BUBBLE]` กลายเป็น **เว้นบรรทัด/ย่อหน้า** ในกล่องเดียว ไม่แตกเป็นหลายกล่องสไตล์ LINE (deprecate multi-box เฉพาะ path สตรีม — amend [[docs/adr/0006]]). ตัด artificial delay 700ms ทิ้ง (cadence จริงมาจากการไหลของ token)
5. **sentinel parsing อยู่ฝั่ง backend** — incremental parser มี buffer lookahead กัน `[BUBBLE]`/`[SUGGEST]` (และเศษ `[`) โผล่แวบในสตรีม. frontend แค่ render `delta` ต่อท้าย — single source of truth เดียวกับ non-stream

**ขอบเขตที่จงใจ (presentation-only):** streaming **ไม่เปลี่ยน conversation state** — `context.Turns` ยังเก็บ finalText เต็ม (พร้อม sentinel) เป็น **1 turn** เหมือน `/Message` (ปรัชญาเดียวกับ [[docs/adr/0006]]). `done.context` คือ context ก้อนเดียวกับที่ `/Message` เคยคืน → logic ฝั่ง front (การ์ดแพคเกจ collapse, `isReadyToPropose`, chips) ทำงานต่อได้ไม่ต้องแก้

**Error/fallback:**
- error **ก่อน** `delta` แรก → `{type:"error"}` → front โชว์กล่อง "ขออภัยค่ะ ระบบประมวลผลนานเกินไป รบกวนพิมพ์อีกครั้งนะคะ" (ข้อความเดียวกับ non-stream)
- error **หลัง** มี partial แล้ว → คงข้อความที่ไหลไว้ + ต่อท้ายหมายเหตุสั้น, **ไม่ส่ง `done`** → front รู้ว่าไม่สมบูรณ์ (ไม่อัปเดต context)
- LLM ปิด/ไม่ตั้งค่า → `/MessageStream` ยังใช้ได้: ปล่อย full text เป็น `delta` เดียว + `done` (UI เดิน path เดียว)

**Guardrail:** `done` ต้องมาเป็น event สุดท้ายเสมอเมื่อสำเร็จ; ไม่มี `done` = ถือว่าไม่สมบูรณ์. parser ฝั่ง front ต้องทนบรรทัด NDJSON ที่มาไม่ครบ (buffer จนเจอ `\n`). `status` ห้ามถูกเก็บลง `messages[]` ถาวร (ephemeral)

**ทางเลือกที่ตัดทิ้ง:**
1. **SSE + `EventSource`** — มาตรฐาน AI chat ทั่วไป แต่ `EventSource` POST ไม่ได้ (ต้องยัด context ใหญ่ใน query/cookie — เลอะ); fetch+ReadableStream ทำ POST ได้ตรงกว่า. (จะใช้ SSE framing `data:` บน fetch ก็ได้ แต่ NDJSON parse ง่ายกว่าโดยไม่เสียอะไร)
2. **แก้ `/Message` ให้สตรีมเลย (ไม่เพิ่ม endpoint)** — path เดียว แต่พัง contract ของ SM smoke drivers/test + client ที่รอ JSON ก้อนเดียว
3. **คง multi-bubble สตรีมทีละกล่อง** — รักษาเอกลักษณ์ LINE ([[docs/adr/0006]]) ไว้ แต่ผู้ใช้เลือก "แบบ AI chat ทั่วไป" = กล่องเดียว; ความซับซ้อนของการสตรีมข้ามกล่องไม่คุ้ม
4. **สตรีมเฉพาะข้อความสุดท้าย (ไม่มี tool-progress)** — ง่ายกว่า แต่เทิร์นที่เรียก tool ยังเงียบ 1–2 รอบ — แก้ dead air ไม่หมด
5. **parse sentinel ฝั่ง client** — front ฉลาดขึ้นแต่ sentinel logic แตกเป็น 2 ที่ (stream/non-stream) เสี่ยง drift; เก็บไว้ backend ที่เดียว

**ผลที่ตามมา:** มี 2 path ตอบแชต (`/Message` ก้อนเดียว + `/MessageStream` สตรีม) ที่ต้องคุมกฎ sentinel/grounding ให้ตรงกัน — แก้โดยให้ทั้งคู่ใช้ tool loop + `ExtractSuggestions`/`SplitBubbles` ตรรกะร่วม (สตรีมแค่ห่อด้วย incremental emitter). `/Suggest` ([[docs/adr/0008]]) ยังเหมือนเดิม
