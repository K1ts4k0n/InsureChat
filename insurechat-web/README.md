# InsureChat Web (frontend)

Angular 21 — หน้า chatbot + admin. แยกจาก vault เป็น repo อิสระ (ADR 0017).

## รัน dev
1. `npm ci`
2. `npm start` (= `ng serve`, proxy ที่ `proxy.conf.json` → backend `http://localhost:5228`)
3. เปิด http://localhost:4200

## build
`npm run build` → `dist/`

## หมายเหตุ
- ต้องมี backend (`insurechat-api`) รันที่ `:5228` (หรือแก้ target ใน `proxy.conf.json`)
