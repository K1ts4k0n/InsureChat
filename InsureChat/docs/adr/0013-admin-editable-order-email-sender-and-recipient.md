# 0013 — ตั้งค่าการส่งอีเมลคำสั่งซื้อผ่านหน้า admin (บัญชีผู้ส่ง + ปลายทาง runtime-editable)

**สถานะ:** ตัดสินใจแล้ว 2026-06-10 (grill-with-docs) — ยังไม่ implement

**บริบท:** [[docs/adr/0012]] ให้ส่ง [[Order email|คำสั่งซื้อทางอีเมล]] ผ่าน Gmail SMTP โดย **ผูกค่ากับ env ระดับ deploy**: app password = `SMTP_PASSWORD`, บัญชีผู้ส่ง = `SMTP_USERNAME`, ปลายทาง override = `ORDER_EMAIL_TO` — ทุกค่าต้องแก้ที่เครื่อง/redeploy. ผู้ใช้ต้องการให้ **admin เปลี่ยน "บัญชีผู้ส่ง" และ "อีเมลปลายทาง" ได้เองจาก `/admin`** (ขนานกับ [[LLM connection settings]] ที่ทำใน [[docs/adr/0011]]) โดยไม่แตะ env/redeploy. ประเด็นสำคัญ: Gmail **บังคับ `From` = บัญชีที่ auth** (โค้ด `Program.cs` ตั้ง `Username`+`From` พร้อมกันอยู่แล้ว) → "เปลี่ยนต้นทาง" จึงไม่ใช่แค่พิมพ์ที่อยู่ From แต่คือ **เปลี่ยนทั้งบัญชี (username + app password)**

**มติ:**

1. **เพิ่มแท็บ "Email" ใน `/admin`** — admin แก้ได้ **3 ค่า**: อีเมลผู้ส่ง (Gmail), [[App Password (write-only)|App Password]], อีเมลปลายทาง (โบรคเกอร์). **host/port ตรึง** ที่ `smtp.gmail.com:587` (Gmail-only ตาม [[docs/adr/0012]] — ไม่เปิด provider/SMTP อื่น)

2. **`From` ผูก = อีเมลผู้ส่งเสมอ** — ไม่ให้ admin แก้ `From` แยก เพราะ Gmail เขียนทับเป็นบัญชีที่ auth อยู่ดี (กัน admin เข้าใจผิดว่าตั้ง From อิสระได้). UI โชว์ช่อง "อีเมลผู้ส่ง" ช่องเดียว → backend set ทั้ง `Username` และ `From`

3. **App Password = write-only + masked + env fallback** — ขนาน [[API key (write-only)]] ใน [[docs/adr/0011]] เป๊ะ: `GET` คืน masked (`••••wxyz`), ไม่เคยส่ง password เต็มกลับ client; admin ไม่เคยตั้ง → fallback env `SMTP_PASSWORD`; มีปุ่มล้าง (กลับไป fallback env); keySource = `admin`/`env`/`none`. ว่างทั้งคู่ → **mock** (ไม่ส่งจริง ตาม [[docs/adr/0012]])

4. **เก็บใน [[app-settings.json]] (Email overrides)** — ค่า null = ใช้ base จาก env/config. ปรัชญา "JSON now, SQL later" ([[docs/adr/0002]]). **runtime-editable**: `OrderEmailService` อ่าน effective settings จาก store **ตอนส่งแต่ละครั้ง** (เลิก `IOptions` snapshot ตอน startup เหมือนที่ [[LlmSettings]] ทำใน [[docs/adr/0011]]) → มีผลทันทีคำสั่งซื้อรอบถัดไป ไม่ต้อง restart

5. **ปุ่มส่งอีเมลทดสอบ** — `POST /api/Admin/Email/Test` ส่งอีเมลจริงไปยังปลายทางด้วยค่าที่เพิ่งบันทึก คืน `success`/SMTP error → admin ยืนยัน App Password ถูกก่อนลูกค้าใช้จริง. **bypass mock**: ถ้ายังไม่มี password คืน error บอกตรง ๆ ว่ายังส่งจริงไม่ได้ (ไม่หลอกว่าสำเร็จ)

**Guardrail:** endpoint Email ทุกตัว `[Authorize(Roles="admin")]` (chat/quote เป็น public — อย่าเผลอเปิดตาม); password ไม่เคยส่งกลับ client (masked เสมอ); `app-settings.json` ต้อง gitignored (มี secret); อีเมลผู้ส่ง/ปลายทาง validate เป็นรูปแบบอีเมลก่อนเซฟ; คง mock เมื่อ password ว่าง (ไม่ promise ว่าส่งทั้งที่ไม่ถึง)

**ทางเลือกที่ตัดทิ้ง:**
1. **ให้แก้ `From` แยกจาก Username** — ยืดหยุ่นบนกระดาษ แต่ Gmail rewrite ทับอยู่ดี → ลวงตา admin
2. **เปิด host/port (SMTP ใดก็ได้)** — รองรับ Outlook/บริษัทได้ แต่ขัด [[docs/adr/0012]] ที่เลือก Gmail โดยตั้งใจ + เพิ่มภาระให้ admin เข้าใจ STARTTLS/SSL/port. YAGNI — เปิดทีหลังได้ถ้าจำเป็น
3. **คง password ที่ env อย่างเดียว แก้แค่ To** — โค้ดน้อยกว่า แต่ admin ยังเปลี่ยนบัญชีผู้ส่งเองไม่ได้ → ไม่ตรงคำขอ "ปรับ email ต้นทาง"
4. **ไม่มีปุ่มทดสอบ** — ง่ายกว่า แต่ admin เซฟแล้วไม่รู้ว่า app password ถูกจนกว่าลูกค้ากดส่งจริง (พิมพ์ผิดเงียบ ๆ)

**ผลที่ตามมา:**
- `AppSettings` เพิ่ม `Email` overrides (`Username`/`Password`/`To` แบบ nullable); `AppSettingsStore` เพิ่ม `GetEffectiveEmail()` + key source, inject `IOptions<OrderEmailSettings>` เป็น base
- `OrderEmailService`: เปลี่ยน ctor จาก `IOptions<OrderEmailSettings>` → `IAppSettingsStore` (อ่าน effective per-send); `From` = effective `Username`
- `AdminController` เพิ่ม `GET/PUT /api/Admin/Email` + `POST /api/Admin/Email/Test`; DTO `EmailConfigDto`/`EmailUpdateDto` (masked password)
- `admin.ts`/`admin.html` เพิ่มแท็บ Email (mirror โครงแท็บ LLM) + ปุ่มทดสอบ + ข้อความผล
- **amend [[docs/adr/0012]]** ข้อ 3 (password env-only) + ข้อ 6 (To via `ORDER_EMAIL_TO`) — ตอนนี้ admin แก้ผ่าน panel, env = fallback
- *แยกประเด็น* (ไม่อยู่ใน ADR นี้): re-seed `admin` ให้ login `admin/admin` (hash เดิมไม่ตรง) + ลบ user กำพร้า `agent`/`customer`/test ตามโมเดล role เดียวใน [[docs/adr/0011]]
