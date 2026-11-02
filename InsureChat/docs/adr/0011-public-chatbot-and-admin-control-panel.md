# 0011 — chatbot เป็น public (ตัด login ฝั่งลูกค้า) + admin control panel (persona / LLM / theme)

**สถานะ:** ตัดสินใจแล้ว 2026-06-09 (grill-with-docs) — ยังไม่ implement

**บริบท:** เดิมระบบบังคับ login ทุกคน — `/quote` + `/chat` ติด `authGuard` (frontend) และ `ChatbotController` ติด `[Authorize]` (backend); User มี role `agent`/`customer` ที่ login เข้ามาใช้ chatbot ([[docs/adr/0001]]/[[docs/adr/0002]]). ผู้ใช้ต้องการ **กลับทิศ**: ให้ลูกค้าทั่วไปใช้ chatbot ได้เลยโดยไม่ต้อง login, เหลือ login ไว้เฉพาะหลังบ้าน `/admin` และอยากให้ admin จัดการ **LLM (system prompt + การเชื่อมต่อ API)** และ **ธีมสี** ได้เองโดยไม่แก้โค้ด/redeploy. ส่วน theme ([[docs/adr/0010]]) วาง `/admin` + global theme + `app-settings.json` ไว้แล้ว — ADR นี้ต่อยอด

**มติ:**

1. **chatbot/quote = public.** เอา `authGuard` ออกจาก route `/quote` + `/chat`, เอา `[Authorize]` ออกจาก `ChatbotController` (+ endpoint ที่ quote/facet เรียก) ให้ anonymous เข้าถึงได้. identity ของ [[Prospect]] (ชื่อ/เบอร์/อีเมล) มาจาก **PII intake turn ในแชต** ไม่พึ่ง JWT อีกต่อไป (สอดคล้อง session-based MVP)

2. **ตัด role `agent`/`customer` ทิ้ง — เหลือ `admin` role เดียวที่ login.** ยกเลิก self-register (`POST /Auth/Register` + หน้า register) เพราะไม่มีบัญชีลูกค้าแล้ว. admin ยัง **seed เท่านั้น** ใน `users.json` ตาม [[docs/adr/0010]]. → **amend [[docs/adr/0001]]** (โมเดล role 3→1) + **[[docs/adr/0002]]** (เหลือ seed admin)

3. **`/admin` = control panel guard 2 ชั้น.** frontend route guard ด้วย role `admin` + backend ทุก endpoint จัดการใช้ `[Authorize(Roles="admin")]` (JWT มี `role` claim + `RoleClaimType="role"` ตั้งไว้แล้วใน `Program.cs` — ใช้ได้ทันที). admin panel จัดการ 3 อย่าง: persona, LLM connection, theme

4. **System prompt = แยก 2 ชั้น (editable + locked).** admin แก้ได้เฉพาะ **persona/โทน/สไตล์การขาย**. ส่วน **กฎเหล็ก** (grounding, ห้ามแต่งเบี้ย, verify T Broker, จรรยาบรรณ คปภ.), **กลไก sentinel** (`[BUBBLE]`/`[SUGGEST]`/`coverageInSet`), และ **knowledge injection จากวิกิ** → **ล็อกในโค้ด**. prompt สุดท้าย = `editable persona` + `locked block` เสมอ. กัน admin ทำ compliance (CLAUDE.md ข้อ 11) หรือ UI พัง. มี default persona ให้ reset

5. **LLM connection = runtime-editable.** admin แก้ base URL/provider, model, API key, temperature, max tokens, timeout (= `LlmSettings`). "จัดการ API" = **การเชื่อมต่อ LLM เท่านั้น** (ไม่รวม T Broker / KPN DB / motor path — พวกนั้นยังเป็น config/env)

6. **API key = write-only + masked.** admin ตั้ง/เปลี่ยน key ได้ แต่ `GET` คืน masked (`••••xyz4`) ไม่เคยส่ง key เต็มกลับ client. ถ้า admin ไม่เคยตั้ง → **fallback** env `OPENROUTER_API_KEY` เดิม

7. **เก็บใน `app-settings.json` (ขยายจาก [[docs/adr/0010]]).** เพิ่ม key สำหรับ persona + LLM connection คู่กับ theme. ปรัชญา "JSON now, SQL later" ([[docs/adr/0002]]). ไฟล์ gitignored (มี secret)

8. **มีผลทันทีแชตรอบถัดไป** (ไม่ต้อง restart). `SystemPromptProvider` เลิกเป็น Singleton-cached-at-startup → re-read persona จาก store ตอนประกอบ prompt; LLM client resolve settings per-request จาก store (เช่น `IOptionsMonitor` หรือ settings service) แทน `IOptions` snapshot. **ยกเว้น theme คงเป็น "รอบโหลดถัดไป" ตาม [[docs/adr/0010]]** (ไม่ทำ live push)

**Guardrail:** endpoint จัดการต้อง `[Authorize(Roles="admin")]` ครบทุกตัว (chat เปิด public แล้ว — อย่าเผลอเปิด endpoint จัดการตาม); `GET` ที่ดึง LLM settings มาโชว์ในจอ admin ต้อง mask key เสมอ; `app-settings.json` ต้องอยู่ใน `.gitignore`; persona ว่าง/store ล่ม → ใช้ default persona ที่ฝังในโค้ด (แอปไม่พัง)

**ทางเลือกที่ตัดทิ้ง:**
1. **คง agent login ไว้ด้วย** (public + agent + admin) — เก็บโหมดตัวแทนกรอกแทนลูกค้า แต่ผู้ใช้ยืนยันว่าไม่ต้องการ → เพิ่มความซับซ้อน auth โดยไม่มีคนใช้
2. **ให้ admin แก้ system prompt ทั้งก้อน** — ยืดหยุ่นสุด แต่ admin ลบกฎ "ห้ามแต่งเบี้ย" (ผิด คปภ.) หรือลบ sentinel (UI พัง) ได้ → เสี่ยงเกิน
3. **เก็บ API key plaintext + GET คืนเต็ม** — สะดวกแก้ แต่ key หลุดผ่าน response ได้ → ตัดทิ้งด้วยเหตุผลความปลอดภัย
4. **ต้อง restart server หลังเซฟ** — โค้ดง่ายกว่า (ไม่แตะ Singleton/IOptions) แต่ทดสอบ prompt ลำบาก ขัดเป้าหมาย "จูนเองได้"

**ผลที่ตามมา:**
- ลบหน้า/ logic register + login ฝั่งลูกค้า; `/login` เหลือไว้เป็นทางเข้า admin เท่านั้น (หรือย้ายใต้ `/admin`)
- `ChatbotController` อ่าน identity จาก JWT ไม่ได้แล้ว (anonymous) → ต้องรับ identity จาก request/แชตแทน; ตรวจว่า service ไม่พึ่ง `userCode` จาก JWT
- `SystemPromptProvider` ต้อง refactor: แยก persona ออกเป็นค่าใน store + locked block ในโค้ด, เลิก cache-at-startup
- `LlmSettings` ต้องอ่าน runtime (settings service / `IOptionsMonitor`) ไม่ใช่ `IOptions` snapshot
- เพิ่ม endpoints: `GET/PUT /api/Admin/Persona`, `GET/PUT /api/Admin/Llm` (admin only); theme ตาม [[docs/adr/0010]] เดิม
- เพิ่ม `app-settings.json` ลง `.gitignore`
