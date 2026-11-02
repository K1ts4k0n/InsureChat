// ปลายทาง backend จริงบนเซิร์ฟเวอร์ (โปรดักชัน)
// frontend (insurechatfrontend) อยู่คนละโดเมนกับ backend (insurechatbackend)
// จึงเรียกแบบ cross-origin → ต้องเปิด origin นี้ใน Cors:AllowedOrigins ฝั่ง backend (appsettings.json)
// build production จะสลับมาใช้ไฟล์นี้แทน environment.ts ผ่าน fileReplacements ใน angular.json
export const environment = {
  production: true,
  apiUrl: (typeof window !== 'undefined' && localStorage.getItem('insurechat_api_url')) || 'http://insurechatbackend.clicktodesign.net/api'
};
