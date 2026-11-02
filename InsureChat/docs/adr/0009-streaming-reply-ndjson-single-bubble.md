# 0009 — Streaming คำตอบแชต (NDJSON บน /MessageStream) + กล่องเดียวแบบ AI chat

**บริบท:** คำตอบ LLM ใช้เวลา ~30–60 วิ/รอบ และ 1 เทิร์นอาจวน `CompleteAsync` หลายรอบ (tool loop: filter→prepare→พิมพ์). ปัจจุบัน `/Chatbot/Message` รอ**เต็มก้อน**แล้วค่อยส่ง → ผู้ใช้เห็นหน้าจอค้างยาว ไม่มี feedback. ต้องการ UX แบบ AI chat (token ไหลทีละตัว) เพื่อลด *perceived latency*. แต่ contract เดิมพึ่ง sentinel (`[BUBBLE]`/`[SUGGEST]`) ที่ parse **หลังได้ข้อความเต็ม** และรอบ tool ไม่มี prose ให้สตรีม.

**มติ:**
1. **เพิ่ม endpoint `POST /Chatbot/MessageStream`** คืน **NDJSON** (1 JSON event ต่อบรรทัด) อ่านฝั่ง client ด้วย **`fetch` + ReadableStream** — *ไม่ใช้ SSE/`EventSource`* เพราะ `conversationContext` เป็น POST body ก้อนใหญ่ที่ `EventSource` (GET-only) รับไม่ได้. **คง `/Message` เดิมไว้** สำหรับ state-machine mode / smoke drivers / client เก่า — ไม่แตะ contract เดิม.
2. **4 event types:**
   - `status` — ระหว่าง tool loop ที่ยังไม่มี prose (กัน dead air). ข้อความ**แมพจากชื่อ tool** (`filter_packages`→"กำลังค้นแผนประกัน…", `prepare_proposal`→"กำลังเตรียมใบเสนอ…"). **ถูกแทนทันทีที่ `delta` แรกไหล**
   - `delta` — token/ชิ้นข้อความของคำตอบ (strip sentinel แล้ว)
   - `suggestions` — chips (จาก `[SUGGEST]`)
   - `done` — แบก `UpdatedContext` + `IsReadyToPropose` กลับมา; **เป็นสัญญาณ unblock** ของ client (token ล้วนพา state ไม่ได้)
3. **backend เป็นเจ้าของ sentinel parsing กลางสตรีม** (single source of truth ตาม [[docs/adr/0006]]/[[docs/adr/0008]]): detect `[BUBBLE]`/`[SUGGEST]` ด้วย **buffer lookahead สั้น ๆ** (กันปล่อย token ที่อาจเป็นต้น sentinel), strip ก่อนส่ง `delta`; พอเจอ `[SUGGEST]` → หยุดข้อความ เก็บที่เหลือเป็น `suggestions`.
4. **UX = กล่องเดียวโตขึ้นแบบ ChatGPT** — `[BUBBLE]` กลายเป็น**เว้นบรรทัด/ย่อหน้าในกล่องเดียว** (ไม่แตกเป็นหลายกล่อง). **ตัด delay 700ms ของ [[docs/adr/0006]] ทิ้ง** (ใช้เวลาจริงของการสตรีมแทน typing indicator คั่นกล่อง).

**ขอบเขตที่จงใจ (presentation-only):** สตรีมเป็น **presentation ล้วน** — `context.Turns` ยังเก็บ **finalText 1 turn เดียว (พร้อม sentinel)** เหมือนเดิม (ต่อหลักของ [[docs/adr/0006]]); state model ไม่เปลี่ยน, few-shot ยังคง sentinel ไว้.

**Error / fallback (ปิดท้ายด้วย `done` เสมอเพื่อ unblock client):**
- **error ก่อน `delta` แรก** (เช่น final completion ล่ม) → emit `error` → `done`; client โชว์กล่อง "ขออภัยค่ะ ระบบมีปัญหา รบกวนพิมพ์อีกครั้งนะคะ"
- **error กลางสตรีม** → **คงข้อความที่ไหลมาแล้ว** (ไม่ลบทิ้ง) + emit `error` → client ต่อท้ายหมายเหตุสั้น ("(การเชื่อมต่อขัดข้อง ลองใหม่นะคะ)"); commit finalText **เท่าที่ได้** เป็น turn
- **empty หลัง tool loop** → forced final completion (ตาม engine เดิม) → ถ้ายังว่าง stream fallback "ขออภัยค่ะ ระบบประมวลผลนานเกินไป รบกวนพิมพ์อีกครั้งนะคะ"

**Guardrail:** `status` แมพจาก **allowlist ของชื่อ tool** (ไม่ใช่ข้อความอิสระจาก LLM); buffer lookahead ครอบแค่ความยาว sentinel ที่ยาวที่สุด; `done` **ต้องมาเสมอ** (ทุก path รวม error); กฎ `suggestions` ใช้ร่วมกับ [[docs/adr/0008]] (เว้น chips ตอน [[#PII intake turn]]); ทั้ง `/Message` และ `/MessageStream` **แชร์ฟังก์ชัน parse sentinel เดียวกัน** เพื่อไม่ให้กฎเพี้ยนระหว่าง 2 path.

**ทางเลือกที่ตัดทิ้ง:**
1. **SSE/`EventSource`** — POST body ใหญ่ไม่ได้ (GET-only) จึงเลือก NDJSON บน `fetch` stream
2. **WebSocket** — two-way เกินจำเป็นสำหรับ one-way server→client
3. **คง multi-bubble ตอนสตรีม** (สตรีมทีละกล่องตามลำดับ) — รักษาเอกลักษณ์ LINE แต่ซับซ้อนและผู้ใช้ต้องการ feel AI chat กล่องเดียว
4. **parse sentinel ฝั่ง client** — กระจาย logic + เสี่ยง flash sentinel ระหว่างพิมพ์; backend owns ดีกว่า
5. **แทนที่ `/Message` ตรง ๆ** — พัง SM mode / smoke drivers / contract เดิม

**ผลที่ตามมา:** [[docs/adr/0006]] (multi-bubble + delay 700ms) ถูก **supersede เฉพาะ path สตรีม** — ยังมีผลกับ `/Message` เดิม (SM/test). มี **2 path ตอบแชต** (สตรีม = UI หลัก, `/Message` = SM/driver) ที่ต้องคุมกฎ sentinel/suggestions ให้ตรงกันผ่าน parser ที่แชร์กัน. `IChatCompletionClient` ต้องเพิ่มเมธอด streaming (เช่น `CompleteStreamAsync` คืน `IAsyncEnumerable`) ข้าง `CompleteAsync` เดิม.
