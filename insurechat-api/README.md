# InsureChat API (backend)

ASP.NET Core (net10) — chatbot ขายประกันรถ. แยกจาก vault เป็น repo อิสระ (ADR 0017).

## รัน dev
1. ติดตั้ง .NET 10 SDK + เข้าถึง SQL Server ได้
2. `cp .env.example .env` แล้วเติมค่า (อย่างน้อย `EF_DB_CONNECTION`, `OPENROUTER_API_KEY`)
3. `dotnet run --project InsureChatBot.Api --urls http://localhost:5228`
4. OpenAPI: http://localhost:5228/openapi/v1.json
5. Swagger UI (dev): http://localhost:5228/swagger

## ทดสอบ
`dotnet test`

## หมายเหตุ
- frontend (`insurechat-web`) เรียก API ผ่าน proxy ที่ `:5228`
- ไฟล์ความรู้อยู่ใน `InsureChatBot.Api/Knowledge/` (sync มาจาก wiki ของ owner — source of truth)
- การค้นแพคเกจด้วย Python tool เป็น owner-only (ต้องตั้ง `MOTOR_SCRIPT_PATH`); ไม่ตั้ง = fallback ว่าง
- EF migrations: `dotnet ef database update --project InsureChatBot.Api`
