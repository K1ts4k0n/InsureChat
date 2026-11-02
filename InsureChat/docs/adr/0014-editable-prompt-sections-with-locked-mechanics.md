# 0014 — แตก system prompt เป็น 4 ช่องแก้ได้รายช่อง (คงล็อกเฉพาะกลไก parser + knowledge วิกิ)

**สถานะ:** ตัดสินใจแล้ว 2026-06-11 (grill-with-docs) — ยังไม่ implement. **amend [[docs/adr/0011]] ข้อ 4** (เส้นแบ่ง editable/locked ของ system prompt)

**บริบท:** [[docs/adr/0011]] วาง system prompt เป็น 2 ชั้น — admin แก้ได้แค่ **persona** (~2 บรรทัด) ส่วนที่เหลือ (กฎเหล็ก compliance + กลไก sentinel + knowledge วิกิ) ล็อกรวมเป็นก้อนเดียว และ **ตัดทิ้งทางเลือก "แก้ทั้งก้อน"** เพราะกลัว admin ลบกฎ "ห้ามแต่งเบี้ย" (ผิด คปภ.) หรือลบ sentinel (UI พัง). ผู้ใช้ต้องการแก้ system prompt ได้กว้างกว่า persona — แต่การแกะ `BuildLocked` พบว่า "locked" ปนของ 2 ชนิดที่ความเสี่ยงต่างกันมาก: **(A) compliance** (แก้แล้วเสี่ยงจรรยาบรรณ แต่แอปยังทำงาน) กับ **(B) กลไกที่ผูกกับ parser ฝั่ง backend** (`[BUBBLE]`/`[SUGGEST]`/`coverageInSet`/mapping `filter_packages`+`prepare_proposal` — token ที่ backend อ่านแล้ว split/strip จริง; แก้แล้ว **UI/parser พังทันที**).

**มติ:**

1. **แตก editable เป็น 4 ช่องแยกกัน** (แท็บเปลี่ยนชื่อจาก "บุคลิก (Persona)" → **"คำสั่ง AI (System Prompt)"**): ① **บุคลิก/โทน** (persona เดิม) ② **วิธีการขาย & การสโคป** (consultative/SPIN + 4 ประเด็นสโคป — เฉพาะ *เจตนาการขาย* ไม่รวมประโยคที่อ้าง tool) ③ **กฎเหล็ก compliance** ④ **สไตล์การตอบ/ตาราง**. แต่ละช่อง **แก้ + รีเซ็ตเป็น default ได้รายช่อง** (null/ว่าง = ใช้ default ในโค้ด).

2. **กลุ่ม B + knowledge วิกิ = ยังล็อก.** กลไก sentinel/tool (parser-coupled) คงฝังในโค้ด; knowledge inject อัตโนมัติจาก 6 ไฟล์วิกิเหมือนเดิม (wiki = source of truth). ทั้งสองโชว์ใน UI แบบ **read-only** ให้ admin เห็น system prompt เต็มที่ส่งให้ LLM จริง.

3. **ย้าย "กฎเหล็ก compliance" จาก locked → editable (③).** = **กลับครึ่งหนึ่งของ [[docs/adr/0011]]** ที่ตัดทิ้งการแก้กฎ. ยอมรับความเสี่ยงว่า admin ลด/ลบกฎ "ห้ามแต่งเบี้ย"/คปภ. ได้ แลกกับการจูนโทน compliance เองโดยไม่ redeploy.

4. **prompt สุดท้าย** = ① + ② + ③ + ④ (จาก store, ว่าง→default) + locked mechanics (knowledge + B). re-read ทุกครั้งที่ประกอบ prompt → มีผลแชตรอบถัดไป (คง pattern [[docs/adr/0011]] ข้อ 8).

5. **Schema (`app-settings.json`):** ขยาย `AppSettings` จาก `Persona` เดี่ยว → เพิ่มฟิลด์ nullable พี่น้อง `SalesApproach` / `Compliance` / `Style` (flat, backward-compatible — json เก่าที่มีแค่ `Persona` ยังอ่านได้ ฟิลด์ใหม่=null→default). `SystemPromptProvider` เพิ่ม `DefaultSalesApproach`/`DefaultCompliance`/`DefaultStyle` consts (สกัดจาก `BuildLocked` เดิม).

**Guardrail / ความเสี่ยงคงเหลือ (CLAUDE.md ข้อ 11):**
- การเปิดให้แก้ ③ compliance = ผ่อน guardrail เดิมโดยตั้งใจ. **ป้องกันชั้นที่เหลือ:** grounding บางส่วนยัง enforce ที่ server (เบี้ย/ชื่อแผนมาจากผล tool เท่านั้น — prompt ลบกฎได้ แต่ระบบไม่ได้ป้อนตัวเลขมั่วให้ LLM ตั้งแต่ต้น), และกลไก B ที่ล็อกไว้กันการ์ด/ตาราง/ชิปพัง. แต่ถ้า admin ลบ "ห้ามแต่งเบี้ย" LLM ยัง **hallucinate ราคาได้** → เป็นความเสี่ยงที่รับไว้รู้ตัว.
- ช่อง ③ ควรมี **คำเตือน + ยืนยันก่อนเซฟ** (กันแก้พลาด); ช่องอื่นเซฟตรงได้.
- ห้ามย้าย token กลุ่ม B ออกจาก locked ไม่ว่ากรณีใด — แก้แล้ว parser พัง ไม่ใช่แค่ compliance.

**ทางเลือกที่ตัดทิ้ง:**
1. **คงแก้แค่ persona เดียว (สถานะเดิม)** — ปลอดภัยสุด แต่ไม่ตอบโจทย์ผู้ใช้ที่อยากจูนวิธีขาย/compliance เอง.
2. **5 ช่อง — เปิดกลุ่ม B (sentinel) ให้แก้ด้วย** — ยืดหยุ่นสุด แต่ admin แก้ `[BUBBLE]` พลาด=แชตพัง → เสี่ยงเกินโดยไม่มีประโยชน์ (sentinel ไม่ใช่สิ่งที่ควรจูน).
3. **ช่องเดียว textarea prompt เต็ม (= ทางเลือกที่ 2 ใน [[docs/adr/0011]])** — ง่ายสุดแต่ไม่มี guardrail เลย ทั้ง compliance และ parser พังได้พร้อมกัน.
4. **ทำ knowledge เป็นช่องแก้ได้** — จะ detach prompt จากไฟล์วิกิ ขัดหลัก compounding artifact (CLAUDE.md) → คง auto-inject.

**ผลที่ตามมา:**
- `AppSettings` + `app-settings.json` เพิ่ม 3 ฟิลด์; `SystemPromptProvider.BuildLocked` ต้อง refactor แยกเป็น default consts (รายช่อง) + locked mechanics builder, และ `Get()` ประกอบ 4 ช่อง + locked.
- `AdminController`: `GET/PUT /api/Admin/Persona` ขยาย/แทนด้วย endpoint ที่คืน-รับ 4 ช่อง + default รายช่อง + read-only locked preview (ออกแบบ DTO ใหม่ `PromptSectionsDto`).
- Frontend แท็บ `persona` → เปลี่ยนชื่อ + เป็น 4 textarea (แต่ละช่อง badge "กำหนดเอง" + ปุ่มบันทึก/รีเซ็ตรายช่อง) + กล่อง read-only โชว์ knowledge/sentinel.
- ต้องแยกเนื้อหา section `# บทบาทในแชตนี้` เดิมที่ปนโค้ช+tool ออกจากกัน (เจตนาการขาย→②, ประโยคอ้าง tool→locked).
