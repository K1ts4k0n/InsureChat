# 0010 — role admin (ที่ 3) + ธีมสีระดับแอป (global) ผ่าน CSS variables

**บริบท:** อยากให้ปรับ **สีแบรนด์ของแอปทั้งระบบ** ได้เองผ่านเมนูผู้ดูแล โดยไม่ต้องแก้โค้ด/รี-deploy. ปัญหา 2 ชั้น: (1) ระบบมีแค่ 2 role (`agent`/`customer`) — ไม่มี "ผู้ดูแล" ([[docs/adr/0001]]/[[docs/adr/0002]]); (2) สียัง **hardcode เป็น hex กระจาย ~19 จุดใน 4 ไฟล์ CSS** ไม่มีชั้นธีมให้เปลี่ยนได้

**มติ:**
1. **เพิ่ม role `admin` (ที่ 3)** — `UserRoles.Admin`. **seed เท่านั้น** (ใส่ใน `users.json` ตอน setup); **self-register ยังบังคับ `customer`** เหมือนเดิม (ไม่เปิดทางสมัครเป็น admin). admin **ไม่มี chatbot flow** ของตัวเอง — ใช้เข้า **admin menu** (`/admin`, guard ด้วย role) เท่านั้น. amend [[docs/adr/0001]] (โมเดล role 2→3) + [[docs/adr/0002]] (seed admin)
2. **ธีม = global** (ไม่ใช่ per-user) — admin ตั้งครั้งเดียว มีผลกับทุก user. สอดคล้องคำว่า "สีธีมของแอป"
3. **ธีม = สี primary/แบรนด์ 1 สี** + เฉด **hover** (derive โดย darken) + สี **ตัวอักษรบนพื้น brand** (`--on-brand` = ขาว/ดำ เลือกอัตโนมัติจาก relative luminance กัน contrast พัง). **สี semantic (success เขียว / error แดง / warn เหลือง) คงที่** — ไม่อยู่ในธีม เพื่อความสม่ำเสมอ + ปลอดภัยเรื่องความหมาย/contrast
4. **เลือกสีแบบ preset + custom hex** — มี palette คัดมา 4–5 ชุด (vetted) ให้กดเลือกเร็ว และช่องกรอก hex เองสำหรับแบรนด์เฉพาะ
5. **Infra = CSS custom properties** — refactor hex ที่ hardcode → ตัวแปรบน `:root` (`--brand`, `--brand-hover`, `--on-brand`). เปลี่ยนธีม = set ตัวแปรพวกนี้ที่ runtime (ไม่ rebuild)
6. **เก็บฝั่ง server (ยังไม่มี DB)** — `app-settings.json` ที่ root (คู่กับ `users.json`, ตามปรัชญา [[docs/adr/0002]] "JSON now, SQL later"). endpoints: **`GET /api/Settings/Theme` = public** (เพื่อให้หน้า login/register ธีมก่อน auth ด้วย) + **`PUT /api/Admin/Theme` = admin JWT เท่านั้น**
7. **apply ตอน bootstrap** — frontend ดึงธีมตอนเปิดแอป (APP_INITIALIZER) แล้ว set CSS vars บน `:root`. admin กดเซฟ → `PUT` → client อื่นเห็นใน **รอบโหลดถัดไป** (ไม่ทำ live push — เกินจำเป็น)

**ขอบเขตที่จงใจ (ตัดออกตอนนี้):** ไม่ทำ **dark mode**, ไม่ทำ **per-user theme**, ไม่ทำ live broadcast ข้าม client, ไม่ธีมสี semantic. ธีม = "สีแบรนด์เดียว global" เท่านั้น

**Guardrail:** custom hex ต้อง validate รูปแบบ (`#rrggbb`); `--on-brand` + `--brand-hover` derive จากสีที่ใส่ (ไม่ให้ admin ตั้งจนอ่านไม่ออก); ถ้า `GET /Settings/Theme` ล่ม/ยังไม่เคยตั้ง → ใช้ค่า default ที่ฝังใน CSS (`--brand: #2563eb`) — แอปไม่พังถ้าไม่มี settings

**ทางเลือกที่ตัดทิ้ง:**
1. **ใช้ agent เป็นผู้ดูแล (ไม่เพิ่ม role)** — ไม่แตะ auth แต่ agent ทุกคนจะแก้ธีมทั้งแอปได้ (สิทธิ์กว้างเกิน)
2. **`isAdmin` flag บน user (คง 2 role)** — เบากว่า แต่ทำให้ "สิทธิ์" แตกเป็น 2 แกน (role + flag) สับสนกว่าการมี role ชัด ๆ
3. **ธีม per-user (localStorage)** — ไม่ต้องมี server/admin เลย แต่ไม่ตรงโจทย์ "แอปทั้งระบบ"
4. **theme เป็นไฟล์ CSS หลายชุดสลับกัน** — เปลี่ยนทั้งชุดได้ แต่ custom hex ทำไม่ได้ และคุม contrast ยาก; CSS variable ยืดหยุ่นกว่า
5. **live push ผ่าน WebSocket** — ธีมเปลี่ยนทันทีทุก client แต่เกินคุณค่า (ธีมเปลี่ยนนาน ๆ ครั้ง) — โหลดรอบหน้าก็พอ

**ผลที่ตามมา:** ต้องมี seed admin ตอน setup + เอกสารบอกวิธีเพิ่ม admin (แก้ `users.json`); การ refactor สีเป็นตัวแปรแตะ 4 ไฟล์ CSS ครั้งเดียว แล้วของใหม่ต้องใช้ `var(--brand)` แทน hex เสมอ (ใส่เป็นกฎใน lint/รีวิว). `GET /Settings/Theme` เป็น endpoint สาธารณะตัวแรก (ก่อนหน้านี้ทุก endpoint ต้อง auth) — ระวังอย่าให้รั่วข้อมูลอื่น

---

## อัปเดต [2026-06-09] — เปิด dark mode (กลับมติเดิมข้อ "ขอบเขตที่จงใจ")

**บริบท:** งาน redesign frontend ทั้งหมด (สไตล์ trust-first fintech) ตัดสินใจ **เพิ่ม dark mode** ซึ่งเดิม ADR นี้ระบุ "ไม่ทำ dark mode" ไว้ในขอบเขตที่จงใจ. ตอนวาง refactor `var(--brand)` (ที่ ADR นี้สั่งไว้แต่ยังไม่เคย build ฝั่ง frontend) จึงวางชั้น **design token** เต็มรูปไปเลย แทนที่จะมีแค่ 3 ตัวแปร brand

**มติเพิ่มเติม:**
1. **dark mode = แกนคนละแกนกับ brand color.** `--brand`/`--brand-hover`/`--on-brand` ยังคงเป็น **global ตั้งโดย admin** (ตามมติเดิม). ส่วน light/dark เป็น **surface theme** ที่สลับ token พื้น/ตัวอักษร/เส้นขอบ ผ่าน `[data-theme="dark"]` บน `<html>`
2. **dark = per-user + เครื่อง.** เลือกจาก `prefers-color-scheme` เป็นค่าเริ่มต้น, override ได้ด้วย toggle, จำใส่ `localStorage` (ไม่ขึ้น server — ต่างจาก brand color). เหตุผล: dark เป็นความชอบส่วนตัว/ตามอุปกรณ์ ไม่ใช่อัตลักษณ์แบรนด์
3. **brand color ต้องอ่านออกทั้ง 2 โหมด.** ใช้เป็น fill เป็นหลัก (ปุ่ม/ฟอง user) คู่กับ `--on-brand`; ตอนใช้เป็น accent text/border บนพื้นมืดมี `--brand-on-dark` (lighten) กัน contrast พัง
4. **semantic (success/error/warn) ยังคงที่** แต่ในโหมดมืดใช้เฉดที่ปรับ token พื้น/ตัวอักษรของ alert ให้ contrast ผ่าน (ตัวสีหลักความหมายเดิม)

**ขอบเขตที่ยังตัด:** per-user **brand** theme, live broadcast ข้าม client — ยังไม่ทำ (เหมือนเดิม)
