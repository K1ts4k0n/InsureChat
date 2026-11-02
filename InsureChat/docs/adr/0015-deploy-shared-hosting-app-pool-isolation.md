# 0015 — Deploy InsureChat บน shared hosting: self-contained + out-of-process + แยก app pool

InsureChat app (`app/`) ถูก deploy บน **shared Windows hosting (Plesk, clicktodesign.net)** ร่วมกับโปรเจกต์ .NET อื่นของเจ้าของเดียวกัน (`KPN`, `tcap`, `tbrok`) โดยแต่ละโปรเจกต์เป็นโฟลเดอร์ระดับบนสุดแยกกัน (`/appCahtBot/{frontend,backend}`, `/KPN/{api,Web}`, ...) แต่ **หลาย backend ถูกจับรวมไว้ใน IIS Application Pool เดียวกัน**

ตอน deploy ครั้งแรกทำให้ **service อื่นล่มพร้อมกัน** (`insurechatbackend` + `kpnnewapi` ตอบ 503 พร้อมกัน ส่วน frontend static ยังขึ้น 200) — สอบสวนแล้วเป็นปัญหา hosting model + การแชร์ app pool

## บริบทที่ค้นพบ (เทียบกับ KPN เป็น reference)

- `KPN_Backend/publish/web.config` + `KPN_Frontend/publish/web.config` ตั้ง `hostingModel="outofprocess"` และรันแบบ **framework-dependent** (`processPath="dotnet" arguments=".\*.dll"`, .NET 6)
- แปลว่า **เซิร์ฟเวอร์มี .NET 6 runtime** แต่ **ไม่มี .NET 10** (ที่ InsureChat ใช้)
- IIS hosting model มีกฎ: **in-process = 1 แอปต่อ pool**; **out-of-process = หลายแอปต่อ pool ได้ แต่ทุกแอปต้อง out-of-process เหมือนกัน**

## Decision

1. **Backend = self-contained `win-x64`** — เซิร์ฟเวอร์ไม่มี .NET 10 runtime (มีแค่ .NET 6 ที่ KPN ใช้) framework-dependent จึง 503; self-contained รวม runtime มาเอง (ดู `FolderProfile.pubxml`, ~120 MB)
2. **Backend = `hostingModel=outofprocess`** — ให้ตรงกับ KPN ที่อยู่ pool เดียวกัน (in-process จะ HTTP 500.34 mixing hosting models)
3. **แยก Application Pool ให้ InsureChat (dedicated pool)** — ถึง hosting model จะตรงกัน แต่การแชร์ pool ทำให้ความล้มเหลวลามถึงกัน: รอบแรก InsureChat เป็น in-process → 500.34 ซ้ำ → IIS rapid-fail protection สั่ง stop ทั้ง pool → KPN ล่มตาม การแยก pool ตัด blast radius นี้
4. **`.env` auto-copy เข้า publish** ผ่าน csproj (`CopyToPublishDirectory`) — กันลืมอัป secrets
5. **Frontend = static Angular + CORS** — `environment.production.ts` ชี้ backend ตรง ๆ คนละ domain; backend เปิด origin ใน `Cors:AllowedOrigins`

## Considered Options

- **แยก pool + out-of-process (เลือก)** — ทำงานร่วม KPN ได้ + isolation; แลกกับ proxy overhead เล็กน้อยของ out-of-process
- **แชร์ pool + ตั้ง out-of-process ให้ตรง KPN** — ใช้ได้ทางเทคนิค แต่ InsureChat crash ตอน start จะลาก KPN ล่ม (rapid-fail ปิดทั้ง pool) — เสี่ยงเกินรับ
- **framework-dependent + ขอ host ลง .NET 10 runtime** — พึ่ง host (shared hosting มักไม่ลง runtime ใหม่ให้), .NET 10 ใหม่เกิน

## Consequences

- ต้องขอ/เปิด **dedicated app pool** ทุกครั้งที่ตั้ง site ใหม่บน shared hosting นี้
- publish ใหญ่ (~120 MB) เพราะ bundle runtime; FTP นานขึ้น และต้องเปิด show-hidden-files เพื่ออัป `.env`
- web.config ทั้ง backend (SDK) และ frontend (เพิ่มเอง) ต้องมี `<location path="." inheritInChildApplications="false">` กัน config รั่วลง sub-application
- เมื่อแยก pool แล้ว InsureChat backend จะกลับเป็น in-process (เร็วกว่า) ได้ เพราะ pool มีแอปเดียว
- ดู [[docs/adr/0001]] (แยก standalone จาก KPN) และ memory `insurechat-app-deployment`
