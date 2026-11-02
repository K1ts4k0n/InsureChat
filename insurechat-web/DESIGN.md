# Design System: InsureChat

> Source of truth สำหรับหน้าตา (UI) ของ InsureChat — chatbot ขายประกัน + control panel หลังบ้าน
> ทุกหน้าผูกกับ **design token ใน [`src/styles.css`](src/styles.css)** เท่านั้น — ห้าม hardcode hex แบรนด์/พื้น
> 2 แกนอิสระ: **Brand color** (admin ตั้ง global, ADR 0010) × **Surface theme** light/dark (per-user, ADR 0010 amend)

---

## 1. Visual Theme & Atmosphere

**Trust-first fintech** — สงบ อ่านง่าย มั่นใจได้ เหมือนเคาน์เตอร์ธนาคารที่แสงดีและจัดระเบียบเรียบร้อย ไม่ใช่ dashboard ที่ตะโกน

- **Density: 5/10 (Daily App Balanced)** — โปร่งพอให้สแกนเร็ว แน่นพอให้เห็นข้อมูลครบในจอเดียว
- **Variance: 3/10 (Predictable, deliberately)** — *จงใจต่ำ* ขัดกับดีฟอลต์ของสกิล (8) เพราะนี่คือเครื่องมือจัดการ **เงินและความลับ** (API key / app password) — ความชัดเจนและคาดเดาได้สำคัญกว่าความเท่แบบ asymmetric. ฟอร์มตั้งค่าเรียงแกนเดียว อ่านบนลงล่าง
- **Motion: 4/10 (Restrained-Fluid)** — micro-interaction ที่รู้สึกได้ (กดปุ่มยุบ, การ์ดไหลขึ้นตอนสลับแท็บ) แต่ **ไม่มี loop ถาวร**ที่กวนสมาธิในงาน ops

หลักคิด: ผู้ใช้กำลังตัดสินใจเรื่องกรมธรรม์/ความคุ้มครอง และ admin กำลังวางรหัสลับ — ดีไซน์ต้องทำให้ "เชื่อใจ" ไม่ใช่ "ว้าว"

---

## 2. Color Palette & Roles

ค่าจริงจาก token (light / dark). **accent เดียว = brand** (admin ปรับได้, ดีฟอลต์น้ำเงิน saturation < 80%).

### Surface & text
| Token | Light | Dark | บทบาท |
|------|-------|------|------|
| `--bg` | `#f4f6fb` | `#0b1120` | พื้นหลังนอกสุด (ไม่เคยขาว/ดำล้วน) |
| `--surface` | `#ffffff` | `#141d30` | พื้นการ์ด/แผง |
| `--surface-2` | `#f8fafc` | `#0f1830` | พื้น input, secure field |
| `--surface-3` | `#eef2f8` | `#1e293b` | chip/code/hover |
| `--text` | `#0f172a` | `#e9eef7` | ตัวอักษรหลัก (Zinc-950 depth ไม่ใช่ `#000`) |
| `--text-muted` | `#64748b` | `#9aa8bd` | label, คำอธิบาย |
| `--text-subtle` | `#94a3b8` | `#6b7a91` | โน้ตเล็ก, metadata |
| `--border` | `#e4e9f2` | `#243150` | เส้นโครงสร้าง 1px |
| `--border-strong` | `#cbd5e1` | `#36486a` | ขอบ input/ปุ่ม ghost |

### Brand (accent เดียว — admin override runtime)
- `--brand` `#2563eb` — ปุ่ม primary, แท็บ active, focus ring
- `--brand-hover` derive เข้มขึ้น 12% — hover ปุ่ม
- `--brand-ink` — แบรนด์เมื่อเป็น "ตัวอักษร/เส้น" (บนพื้นมืดสว่างขึ้นอัตโนมัติ = `--brand-on-dark` `#60a5fa`)
- `--brand-soft` `#eff6ff` — พื้น tint badge/selected
- `--on-brand` — ขาว/ดำบนพื้นแบรนด์ (เลือกจาก luminance กัน contrast พัง)

### Semantic (คงที่ — นอก brand theme)
- success `--success-text` `#047857` (dark `#4ade80`) — บันทึกสำเร็จ, ทดสอบผ่าน
- error `--error-text` `#b91c1c` (dark `#f87171`) — ทดสอบล้ม, validation, ปุ่ม danger
- warn `--warn-text` `#b45309` — เตือนแบบไม่ถึงขั้น error

**Elevation:** เงา tint สี slate ไม่ใช่ดำล้วน — `--shadow-sm/md/lg`. **ห้าม** เงาดำล้วน/นีออน

---

## 3. Typography Rules

- **Sans (ทั้งระบบ):** `Anuphan Variable` (self-hosted ผ่าน fontsource) → fallback `IBM Plex Sans Thai`, system Thai. เลือกเพราะ **อ่านไทยคม น้ำหนักครบ** และมีคาแรกเตอร์ — *ไม่ใช่* Inter
- **Mono (เลข/รหัสลับ):** `--font-mono` = `ui-monospace, 'Cascadia Code', 'JetBrains Mono', Menlo, monospace` (system stack, ไม่เพิ่ม dependency) — ใช้กับ **masked secret** (`••••wxyz`) และเลข tabular
- ลำดับชั้นด้วย **น้ำหนัก + สี** ไม่ใช่ขนาดมหึมา: `h2` การ์ด 18px/700, label 13px/600 muted, hint 13px muted, โน้ต 12px subtle
- body 15px / line-height 1.55; มือถือ form control บังคับ ≥ 16px กัน iOS ซูม
- **Banned:** Inter, serif ทั่วไป (Times/Georgia) ในทุกหน้า dashboard

---

## 4. Component Stylings

- **Buttons** — แบน ไม่มี glow. radius `--r-control` (10px). primary = พื้น `--brand` + `--on-brand`; ghost = ขอบ `--border-strong` + พื้น `--surface-2`; danger = ตัวอักษร/ขอบ error. **Tactile:** กด `:active` ยุบ `translateY(1px)`. disabled = opacity .6
- **Cards** — `--surface` + ขอบ `--border` 1px + radius `--r-card` (16px) + `--shadow-sm`. เข้าฉากด้วย fade + ไหลขึ้น 6px (`--ease`) ตอน mount/สลับแท็บ
- **Tabs** — underline bar: ตัวอักษร muted → active = `--brand-ink` + เส้นใต้ `--brand` 2px. transition สี/เส้นด้วย `--dur`
- **Inputs** — label เหนือช่อง (ไม่ใช่ floating). พื้น `--surface-2` + ขอบ `--border-strong` + radius 10px. focus = `--ring` (3px brand 35%). โน้ตช่วยใต้ช่อง = `.sub` 12px subtle. error อยู่ใต้ช่อง
- **Secure field (secret box)** — กล่องเฉพาะสำหรับ **app password / API key**: พื้น `--surface-2`, ขอบทึบ + **แถบ accent ซ้าย** สื่อ "ข้อมูลอ่อนไหว", ค่าปัจจุบันโชว์เป็น **masked mono** (`••••wxyz`) + ป้ายแหล่งที่มา (ตั้งผ่านหน้านี้ / จาก environment / ยังไม่ตั้ง). ช่องกรอกใหม่ = `type=password` เว้นว่าง = ไม่เปลี่ยน. มีปุ่มล้างกลับ env. **ไม่เคยโชว์ค่าเต็ม** (write-only)
- **Prompt section card** (แท็บ **"คำสั่ง AI (System Prompt)"** — เปลี่ยนชื่อจาก "บุคลิก (Persona)", ADR 0014) — การ์ด `.card` 1 ใบ = 1 ช่องแก้ได้ใน 4 ช่อง (① บุคลิก/โทน ② วิธีการขาย & สโคป ③ กฎเหล็ก compliance ④ สไตล์การตอบ). หัวการ์ด = `h2` ชื่อช่อง + badge "กำหนดเอง"/"ค่าตั้งต้น" + `.hint` บอกขอบเขต. `textarea` พื้น `--surface-2` (rows: persona ~6, อื่น ~12-16). ปุ่ม **บันทึก/รีเซ็ตรายช่อง** (ไม่มีปุ่มรวม) + `.msg` ผลลัพธ์. เรียงตั้ง stack ไม่ใช่ 3 คอลัมน์
- **Compliance warning (ช่อง ③)** — เหนือ textarea ของกฎเหล็ก: กล่อง `--warn-bg`/`--warn-border`/`--warn-text` ข้อความสั้น "แก้กฎนี้กระทบจรรยาบรรณ คปภ. — บอทอาจสร้างเลขเบี้ยเองถ้าลบ 'ห้ามแต่งเบี้ย'". ปุ่มบันทึกช่องนี้ **confirm ก่อนเซฟ**
- **Locked preview block** (knowledge วิกิ + กลไก sentinel `[BUBBLE]`/`[SUGGEST]`/`coverageInSet`) — read-only โชว์ system prompt ส่วนที่ **ล็อกในโค้ด** ให้ admin เห็นของจริงที่ส่ง LLM. ใช้แนวเดียวกับ secret box: แถบ accent ซ้าย + พื้น `--surface-2` + ตัวอักษรจางลง (`--text-subtle`) + token เป็น **mono**. label "ล็อก — แก้ไม่ได้ (กัน parser/UI พัง)". **ไม่มี textarea/ปุ่ม**
- **Feedback message** — `.msg` ตัวอักษรเล็ก: สำเร็จ = success-text, ล้ม = error-text (`.msg.err`). อย่าโชว์ error เป็นสีเขียว
- **Loaders** — ข้อความสถานะตามบริบท ("กำลังบันทึก...", "กำลังส่ง...") บนปุ่ม ไม่ใช้ spinner วงกลมลอย
- **Empty/error states** — รายงาน inline ชัด ๆ พร้อมบอกวิธีแก้ (เช่น 535 = App Password ผิด)

---

## 5. Layout Principles

- คอนเทนเนอร์ **max-width 760px จัดกลาง** สำหรับแผงตั้งค่า (อ่านสบาย ไม่ยืดเต็มจอ)
- **CSS Grid** สำหรับกลุ่มฟิลด์ (เช่น `.grid3` 3 คอลัมน์) — ไม่ใช้ `calc()` % hack
- **Single-column collapse < 768px** ทุก multi-column → คอลัมน์เดียว, ห้าม horizontal scroll
- ทุกอย่างมีโซนของตัวเอง — **ไม่ซ้อนทับ** (no absolute stacking)
- full-height ใช้ `100dvh` ไม่ใช่ `h-screen` (กัน iOS Safari กระโดด)
- spacing การ์ด: padding ภายใน 24px, ช่องว่างฟิลด์ 14px

---

## 6. Motion & Interaction

- **Ease มาตรฐาน:** `--ease` `cubic-bezier(0.16, 1, 0.3, 1)` (ออกตัวเร็ว-หน่วงนุ่ม คล้าย spring) / `--dur` 0.18s — ไม่ใช้ linear
- **Tactile press:** ปุ่มยุบ `translateY(1px)` ตอน `:active`
- **Card reveal:** การ์ดเข้าฉาก fade + ไหลขึ้น ตอนสลับแท็บ (staggered ได้ถ้าหลาย element)
- **เคารพ `prefers-reduced-motion`** — ปิด animation ให้อัตโนมัติ (มี global rule แล้ว)
- Animate เฉพาะ `transform`/`opacity` — ห้าม `top/left/width/height`
- **ไม่มี loop ถาวร** ในแผง ops (ต่างจากหน้า marketing) — micro-interaction ตอบสนองการกระทำเท่านั้น

---

## 7. Anti-Patterns (ห้าม)

- ❌ อีโมจิใน UI · ❌ ฟอนต์ Inter / serif ทั่วไป · ❌ ดำล้วน `#000000` (ใช้ `--text` `#0f172a`)
- ❌ เงานีออน/outer glow · ❌ accent หลายสี (มี brand เดียว) · ❌ gradient text บนหัวใหญ่
- ❌ custom mouse cursor · ❌ element ซ้อนทับ · ❌ การ์ด 3 คอลัมน์เท่ากันแบบ generic
- ❌ โชว์ secret เต็ม (mask เสมอ) · ❌ โชว์ error เป็นสีเขียว · ❌ spinner วงกลมลอย
- ❌ ชื่อ/ข้อมูลปลอม generic ("John Doe", "Acme") · ❌ คำคลีเช่การตลาด ("Elevate", "Seamless")
- ❌ hardcode hex แบรนด์/พื้น — **ใช้ `var(--token)` เท่านั้น** (กฎเหล็กของระบบนี้)
