# 0001 — แยก chatbot ออกเป็น standalone app ตัดขาดจาก KPN

> ♻️ **อัปเดต (2026-06-08):** โมเดล role ขยายจาก 2 (`agent`/`customer`) → 3 โดยเพิ่ม `admin` (ผู้ดูแลระบบ, seed เท่านั้น, ไม่มี chatbot flow) ใน [[docs/adr/0010]].

แต่เดิมโค้ด chatbot ถูกฝังลงใน repo ของ KPN (`KPN_Backend` = ASP.NET Core, `KPN_Frontend` = Angular) โดยพึ่ง `AuthBaseController`/JWT, `EFDbContext` และ DI ของ KPN. เราตัดสินใจ **ยกโค้ดออกมาเป็นโปรเจกต์อิสระใต้ `InsureChat\app\`** (`app\api\` = ASP.NET Core, `app\web\` = Angular) ที่ไม่อ้างอิง KPN เลย เพื่อให้ chatbot เป็นผลิตภัณฑ์ของตัวเอง พัฒนา/รัน/deploy ได้โดยไม่ผูกกับวงจรชีวิตของ KPN.

## Considered Options

- **ยกของเดิม .NET + Angular แยกโปรเจกต์ (เลือก)** — รักษาโค้ดที่ debug มาแล้ว, เสี่ยงต่ำสุด, แลกกับการต้องดูแล 3 stack (.NET + Angular + Python).
- **เขียนใหม่รวมบน Python (FastAPI + static)** — moving parts น้อยสุด เข้ากับ engine ข้อมูล Python ที่มีอยู่ แต่ต้อง port state machine จาก C# และทิ้งงาน Angular ที่ทำไว้.
- **Node/TS รวม stack** — ทิ้งทั้ง C# และยัง subprocess Python อยู่ดี.

## Consequences

- ตัด `AuthBaseController` → `ControllerBase`, ลบ `EFDbContext` (เป็นโค้ดตายในส่วน chatbot อยู่แล้ว), เปลี่ยน namespace `KPN_WebApi.*` → `InsureChatBot.*`, เขียน `Program.cs`/DI ของตัวเอง.
- app อ้าง `..\tools\motor\query.py` + `..\raw\*.json` (330k แพคเกจ) ผ่าน config — InsureChat ยังเป็น source of truth ของข้อมูล (ดู [[CONTEXT.md]]).
- โค้ด chatbot ใน repo KPN ควรถูกถอดออกภายหลังเพื่อไม่ให้มี 2 ก๊อปปี้ลอยอยู่.
- standalone ต้องมี auth ของตัวเอง (เดิมยืม JWT ของ KPN) — ดู [[docs/adr/0002]].
