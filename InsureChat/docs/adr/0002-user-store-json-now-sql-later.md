# 0002 — User store เป็น JSON config ก่อน, SQL Server ภายหลัง (ผ่าน seam IUserStore)

> ♻️ **อัปเดต (2026-06-08):** [[docs/adr/0010]] เพิ่ม role ที่ 3 = `admin` (**seed ใน `users.json` เท่านั้น**; self-register ยังบังคับ `customer`). โครงสร้าง user store/`IUserStore` เหมือนเดิม.

standalone app ต้องมี auth ของตัวเอง (รองรับ role: `agent`, `customer`, และ `admin` — เพิ่มภายหลังใน [[docs/adr/0010]]) แต่ตอนนี้ยังไม่ตั้ง DB server. เราจึง **เก็บ user ใน `users.json`** (username, passwordHash, role, custCode, name, email) เป็นที่เก็บชั่วคราว และ **ซ่อนหลัง interface `IUserStore`** เพื่อให้สลับไป SQL Server ภายหลังได้โดยไม่กระทบ controller/auth logic.

## Consequences

- ตอนนี้: `JsonUserStore : IUserStore` — เพิ่ม user = แก้ไฟล์ (ยังไม่มี customer self-register).
- ภายหลัง: `SqlUserStore : IUserStore` + EF Core/migrations — สลับผ่าน DI ที่เดียว.
- token เป็น JWT bearer มี `role` claim; Angular เก็บ token + แนบผ่าน interceptor.
- ห้าม logic อื่นอ่าน `users.json` ตรง ๆ — ต้องผ่าน `IUserStore` เท่านั้น มิฉะนั้น seam จะรั่วและการย้ายไป SQL จะยาก.
