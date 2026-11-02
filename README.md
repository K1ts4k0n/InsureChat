# InsureChat — แชทบอทขายประกันรถยนต์

ระบบแชทบอทให้คำปรึกษาและเสนอราคาประกันภัยรถยนต์ ประกอบด้วย backend (ASP.NET Core) + frontend (Angular)

## โครงสร้างโปรเจกต์

```
ChatBot/
├── insurechat-api/     backend — ASP.NET Core (.NET 8) Web API
├── insurechat-web/     frontend — Angular 21
└── InsureChat/         wiki, ADR (docs/adr/), และ Python motor tool (tools/motor/)
```

## สิ่งที่ต้องติดตั้งก่อน (Prerequisites)

| เครื่องมือ | เวอร์ชัน | ใช้ทำอะไร |
|---|---|---|
| **.NET SDK** | **8.0** | build/run backend |
| **Node.js** | 20 LTS ขึ้นไป | build/run frontend |
| **SQL Server** | 2016 ขึ้นไป | ฐานข้อมูลของแอป + KPN package DB |

> **สำคัญ:** โปรเจกต์นี้ตรึงไว้ที่ **.NET 8 (LTS)** โดยตั้งใจ เพราะเซิร์ฟเวอร์ปลายทาง (IIS บน Windows Server 2012 R2) รองรับได้แค่ ASP.NET Core 8 Hosting Bundle — อย่าอัป target framework เป็นเวอร์ชันสูงกว่านี้โดยไม่ได้ตรวจเซิร์ฟเวอร์ก่อน ไม่งั้นจะเจอ HTTP 502.5 ตอน deploy

## เริ่มต้นใช้งาน (Getting Started)

### 1. Backend

```bash
cd insurechat-api

# ตั้งค่า secret — คัดลอกไฟล์ตัวอย่างแล้วกรอกค่าจริง
cp .env.example .env
#   แก้ .env ให้ครบ (ดูรายละเอียดแต่ละ key ในไฟล์)
#   อย่างน้อยต้องมี: EF_DB_CONNECTION, KPN_DB_CONNECTION, JWT_KEY, OPENROUTER_API_KEY

dotnet restore
dotnet run --project InsureChatBot.Api
```

รันแล้วจะขึ้นที่ `http://localhost:5228` (Swagger อยู่ที่ `/swagger` เฉพาะโหมด Development)

> แอปจะ seed ข้อมูลตั้งต้นให้อัตโนมัติตอน start (idempotent) — ถ้าต่อ DB ไม่ได้ แอปจะ**ยังขึ้น**แต่ log error ไว้ ให้เช็ค connection string ใน `.env`

### 2. Frontend

```bash
cd insurechat-web
npm install
npm start
```

รันแล้วเปิด `http://localhost:4200`

Dev server มี proxy ตั้งไว้ ([proxy.conf.json](insurechat-web/proxy.conf.json)) — ทุก request ที่ขึ้นต้นด้วย `/api` จะถูก forward ไปที่ backend `localhost:5228` แบบ same-origin (ไม่มีปัญหา CORS ตอน dev)

> ถ้า backend รันคนละ port ให้แก้ `target` ใน `proxy.conf.json`

## การตั้งค่า Backend URL (สำคัญตอน deploy)

frontend เลือก backend URL จากไฟล์ใน `insurechat-web/src/environments/`:

| ไฟล์ | ใช้ตอน | ค่า `apiUrl` |
|---|---|---|
| `environment.ts` | dev (`npm start`) | `/api` → วิ่งผ่าน proxy |
| `environment.production.ts` | `ng build` (production) | URL เต็มของ backend จริง |

ถ้า deploy ไปโดเมนอื่น ต้องแก้ 2 ที่ให้ตรงกัน:
1. `apiUrl` ใน `environment.production.ts` → ชี้ไป backend จริง
2. `Cors:AllowedOrigins` ใน `insurechat-api/InsureChatBot.Api/appsettings.json` → เพิ่มโดเมนของ frontend (เพราะ frontend กับ backend อยู่คนละโดเมน = cross-origin)

## Build สำหรับ production

```bash
# backend — ได้ผลลัพธ์พร้อม web.config สำหรับ IIS
cd insurechat-api
dotnet publish InsureChatBot.Api/InsureChatBot.Api.csproj -c Release -r win-x64 --self-contained false -o publish-win

# frontend
cd insurechat-web
npm run build      # ผลลัพธ์อยู่ใน dist/
```

## เอกสารเพิ่มเติม

- `InsureChat/docs/adr/` — Architecture Decision Records (เหตุผลเบื้องหลังการตัดสินใจแต่ละเรื่อง)
- `insurechat-web/DESIGN.md` — แนวทางดีไซน์ frontend

## หมายเหตุเรื่องความปลอดภัย

ไฟล์ `.env` (ทั้งของ `insurechat-api/` และ `InsureChat/`) มี secret จริง (รหัส DB, JWT key, API key) — **ไม่ได้รวมมาใน zip นี้** และถูก ignore ใน git อยู่แล้ว ให้สร้างเองจาก `.env.example`
