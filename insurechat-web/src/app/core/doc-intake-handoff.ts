import { Injectable } from '@angular/core';
import { ResolvedVehicle } from './motor-facet';

export interface DocIntakeData {
  table: string;                 // markdown ตารางที่ vision สกัดได้
  resolved: ResolvedVehicle;     // ยี่ห้อ/รุ่น/ปี ที่ resolve เข้าค่า DB ได้ (อาจไม่ครบ)
  province?: string;             // จังหวัดที่เดาจาก IP ที่หน้า quote (อาจว่าง)
  provinceLabel?: string;
}

/** ส่งผลสกัดเอกสารจากหน้า quote → หน้า chat (one-shot). ดู ADR 0019 */
@Injectable({ providedIn: 'root' })
export class DocIntakeHandoff {
  private pending: DocIntakeData | null = null;

  set(data: DocIntakeData): void { this.pending = data; }

  /** อ่านแล้วล้าง (ใช้ครั้งเดียว) */
  consume(): DocIntakeData | null {
    const d = this.pending;
    this.pending = null;
    return d;
  }
}
