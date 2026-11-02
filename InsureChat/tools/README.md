# tools/ — เครื่องมือ CLI ของวิกิ

สคริปต์ช่วยให้ Claude/ผู้ใช้ทำงานกับข้อมูลขนาดใหญ่ที่ใส่ markdown ตรง ๆ ไม่ได้
(ตามแนวคิด "Optional: CLI tools" ของ LLM Wiki) ต้องมี **Python 3** (พบที่เครื่องแล้ว)

## motor/ — ชุดข้อมูลแพคเกจรถยนต์ T Broker (330,408 แพคเกจ)
อ่านข้อมูลดิบจาก `raw/ข้อมูลแพคเกจรถยนต์_Page*.json` โดยตรง (ไม่ duplicate ลง wiki)

| ไฟล์ | หน้าที่ | รันเมื่อไหร่ |
|------|---------|-------------|
| `motorlib.py` | ไลบรารีกลาง (โหลด + จัดชั้นประกัน) | ไม่ต้องรันเอง |
| `query.py` | **ค้นหา/กรองแพคเกจ** (workhorse) | ทุกครั้งที่ลูกค้าถามหาเบี้ย/แพคเกจ |
| `build_catalog.py` | สร้าง `wiki/products/แพคเกจรถยนต์-T-Broker.md` อัตโนมัติ | เมื่อข้อมูล `raw/` เปลี่ยน |
| `explore.py` | สแกนสรุปหมวดหมู่ + เซฟ `_summary.json` | ตรวจสุขภาพข้อมูล |

```bash
python tools/motor/query.py --brand HONDA --model CIVIC --class 1 --year 2020
python tools/motor/build_catalog.py
```
คู่มือเต็ม: [[T-Broker-โบรกเกอร์]] · คลังข้อมูล: [[แพคเกจรถยนต์-T-Broker]]

> ไฟล์ `_summary.json` เป็น output ชั่วคราว (ไม่ใช่หน้าวิกิ)
