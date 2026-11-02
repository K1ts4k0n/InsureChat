// ปลายทาง backend (ASP.NET Core). โปรดักชันค่อยทำ fileReplacements / environment.production.ts
export const environment = {
  production: false,
  // path สัมพัทธ์ → วิ่งผ่าน dev-server proxy (proxy.conf.json) ไปยัง localhost:5228
  // ทำให้ทุก API call เป็น same-origin: ไม่มี CORS, ผ่าน cloudflare tunnel ได้ด้วย tunnel เดียว
  apiUrl: '/api'
};
