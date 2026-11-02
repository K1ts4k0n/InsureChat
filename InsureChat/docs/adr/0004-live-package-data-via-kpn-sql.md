# 0004 — ดึงข้อมูลแพคเกจสดจาก KPN SQL DB ตรง (Dapper) + quote form

ผู้ใช้ต้องการ **ฟอร์มกรอกข้อมูลรถ (dropdown cascade) + ตารางเปรียบเทียบเบี้ย** ก่อนเข้าแชต เพื่อให้เร็ว/แม่นกว่าพิมพ์ตอบ และต้องการ **ข้อมูลสด** (เหมือนหน้า check-insure-premiums2 ของ KPN). endpoint `/CheckPrm/Master` + `/CheckPrmCheckPrm/Search_GetData` ของ KPN เป็น `[Authorize]` และอ่านจาก **SQL DB ของ KPN** (`PackagePolType`/`PackageMotor`/`PolicyType`/`Insure`, DB ชื่อ `KPNNew`).

มติ: ให้ `app/api` **เชื่อม SQL DB ของ KPN ตรง ๆ ด้วย Dapper (read-only)** แล้ว derive facet (brand→model→cc→year→ผลิตภัณฑ์→ทุน) + ตารางแพคเกจ (premNet/premStm) เอง — **ไม่เรียก KPN_Backend HTTP และไม่ต้อง login KPN**. connection string เก็บใน `.env` (`KPN_DB_CONNECTION`, ไม่ commit).

## Considered Options

- **ต่อ KPN SQL DB ตรง + Dapper (เลือก)** — ได้ข้อมูลสด, ไม่ต้องรัน/login KPN_Backend, โค้ด chatbot ยัง standalone. แลกกับ: ผูกกับ **schema DB** ของ KPN + ต้องเข้าถึง DB ได้ (network/credential).
- **Proxy HTTP ไป KPN_Backend** — logic ตรงเดิมเป๊ะ แต่ต้องรัน KPN_Backend + service account login + ผูกแอป KPN เต็มตัว (ขัด [[docs/adr/0001]] มากสุด).
- **Local 330k snapshot** — decoupled สุด ฟรี แต่ข้อมูล "ไม่สด" (เป็น export ของ DB เดียวกัน). ใช้เป็น fallback ได้.

## Consequences

- **Qualify [[docs/adr/0001]]:** โค้ด chatbot ยังไม่อยู่ใน repo KPN และ chat/auth ยังเป็นของตัวเอง — แต่บัดนี้มี **dependency แบบ read-only ต่อ DB ของ KPN** สำหรับข้อมูลแพคเกจ motor (ไม่ใช่ "ตัดขาด 100%" อีกต่อไปสำหรับฟีเจอร์นี้). ถ้า DB เข้าไม่ได้ → ฟอร์มใช้ไม่ได้ (อาจ fallback ไป local 330k ภายหลัง).
- ใช้ **Dapper + Microsoft.Data.SqlClient** อ่านอย่างเดียว (ไม่ต้องโมเดล EF ทั้ง schema). query หลักจาก `PackagePolType` (denormalized: CarBrandName/CarModelName/Year/SumInsureMax/PremNet/PremStm/Discount/DiscType) + join `PackageMotor`(Ccmax/RenovType) / `PolicyType`(PolTypeName) / `Insure`(Name).
- **flow:** quote form → ตารางเปรียบเทียบ (เรียงตามเบี้ย) → ลูกค้าเลือก 1 แพคเกจ → **handoff เข้า chat** (seed package ลง conversationContext) → LLM ปิดการขาย/รับมือข้อโต้แย้ง → ปุ่มเสนอแพคเกจ → `/Propose` (เดิม). ฟอร์มเป็น "ทางเข้าแบบ structured" คู่กับการพิมพ์คุย (ดู [[CONTEXT.md]]).
- เบี้ย: lookup `premStm/premNet` ที่มีในแพคเกจ (มี `SetPremStm` คิดส่วนลดเล็กน้อยฝั่ง KPN — จะ mirror ตามจำเป็น) — ยังคงคำเตือน "ต้องยืนยันใน T Broker ก่อนออกจริง".
