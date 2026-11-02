# 0012 — ส่งคำสั่งซื้อทางอีเมลถึงโบรคเกอร์ (แทน partner JSON POST)

**สถานะ:** ตัดสินใจแล้ว 2026-06-10 (grill-with-docs)

**บริบท:** ปุ่มเดิม "เสนอแพคเกจ" → `/Propose` → [[ProposalService]] ประกอบ JSON สคีมา partner แล้ว **mock** (แค่ log, ไม่ส่งจริง) หรือ **live** (POST ไป `TBroker:ApiUrl` — endpoint จริงยังไม่พร้อม, ดู [[docs/adr/0001]]). ผลคือ "คำสั่งซื้อ" ไม่เคยถึงมนุษย์คนไหนเลย. ผู้ใช้ต้องการให้กดปุ่มแล้ว **คำสั่งซื้อถึงโบรคเกอร์จริง** เพื่อให้เจ้าหน้าที่ติดต่อกลับ โดยส่งเป็น **อีเมล**ไป `clickto.helper@gmail.com` พร้อมรายละเอียดลูกค้า+แพคเกจ

**มติ:**
1. **เปลี่ยนปุ่มเป็น "ส่งคำสั่งซื้อ"** (เดิม "เสนอแพคเกจ") — ทุกที่ที่ลูกค้าเห็น/บอทอ้างถึงปุ่ม. ข้อความสำเร็จใหม่: *"ระบบจะส่งคำสั่งซื้อไปยังโบรคเกอร์ เพื่อให้เจ้าหน้าที่ติดต่อกลับ โปรดดำเนินการตามเงื่อนไขที่เจ้าหน้าที่แจ้ง เพื่อดำเนินการออกกรมธรรม์ต่อไป"*
2. **อีเมล = ช่องทางส่งคำสั่งซื้อหลัก แทน partner POST** — ปุ่มนี้ส่งอีเมล ไม่ยิง partner endpoint. โค้ด partner JSON (`BuildProposalJson`/T Broker live) **เก็บไว้** เป็น integration แยกในอนาคต แต่ไม่ผูกกับปุ่มแล้ว
3. **กลไกส่ง = Gmail SMTP + App Password** (`smtp.gmail.com:587` STARTTLS) ผ่าน **MailKit**. ส่งจาก `clickto.helper@gmail.com` หาตัวเอง (เจ้าหน้าที่เปิด inbox อ่าน). App Password เก็บใน `.env` (`SMTP_PASSWORD`) — ไม่ commit (เหมือน `OPENROUTER_API_KEY`)
4. **เนื้ออีเมล = ฟอร์ม HTML** — 2 ส่วน: **ลูกค้า** (ชื่อ, เบอร์, อีเมลถ้ามี, + บัตร/ทะเบียนถ้ามี) + **แพคเกจที่เลือก** (บริษัท, ชั้น, ทุนประกัน, เบี้ยสุทธิ/รวม, รถ ยี่ห้อ/รุ่น/ปี). Subject: `คำสั่งซื้อประกันรถ — {ชื่อลูกค้า} ({รถ})`
5. **mock เมื่อยังไม่ตั้ง credential / error เมื่อส่งจริงล้ม** — `SMTP_PASSWORD` ว่าง → log เนื้ออีเมล + คืน success (ข้อความใหม่) เพื่อ test ได้; ตั้งแล้ว → ส่งจริง **synchronous**, ถ้า SMTP error → คืน `success:false` (ไม่หลอกว่าสำเร็จ). มิเรอร์ pattern mock/live ของ [[docs/adr/0001]]
6. **ปลายทาง config ได้** — default `clickto.helper@gmail.com`, override ผ่าน `ORDER_EMAIL_TO`

**Guardrail:** การ gate ชื่อ+เบอร์ ([[Required identity]]) ยังอยู่ก่อนถึงขั้นส่งอีเมล; App Password ห้าม commit (อยู่ `.env`, gitignored); ส่ง synchronous แต่ Gmail ปกติ <2s — ยอมรับได้

**ทางเลือกที่ตัดทิ้ง:**
1. **ส่งทั้งอีเมล + partner POST** — เก็บ 2 ช่องทาง แต่ partner endpoint ยังไม่จริง → ซับซ้อนเกินจำเป็น
2. **Transactional API (SendGrid/Resend/Brevo)** — deliverability ดีกว่า production แต่ต้องสมัครบริการ+ตั้ง domain; Gmail SMTP เพียงพอสำหรับปริมาณนี้และคุม inbox เอง
3. **fire-and-forget (สำเร็จเสมอ)** — ง่ายแต่ promise ลูกค้าว่าส่งแล้วทั้งที่อาจไม่ถึง → เสี่ยง mis-communication
4. **บังคับ live เสมอ (ไม่มี mock)** — test ไม่ได้จนกว่าจะมี app password

**ผลที่ตามมา:**
- เพิ่ม dependency **MailKit**; `OrderEmailService` + `OrderEmailSettings` (host/port/from/to/password/enabled); bind ใน `Program.cs` + env `SMTP_PASSWORD`/`ORDER_EMAIL_TO`
- `ChatbotService.SubmitProposalAsync` เรียก `OrderEmailService` แทน `ProposalService`
- เปลี่ยน label "เสนอแพคเกจ"→"ส่งคำสั่งซื้อ" ทั้ง frontend + ข้อความบอท (prompt/state machine)
- `.env` ต้องมี `SMTP_PASSWORD` ตอน production (ไม่งั้น mock); เพิ่มลงเอกสาร setup
