/** ฟิลด์ที่ intake gate ต้องเก็บให้ครบก่อนค้นแผน (ลำดับเดียวกับฟอร์ม quote) — ADR 0019 */
export type IntakeField = 'brand' | 'model' | 'year' | 'province';

export interface IntakeKnown {
  brand?: string | null;
  model?: string | null;
  year?: number | null;
  province?: string | null;        // รหัสจังหวัด (value)
  provinceLabel?: string | null;   // ชื่อไทย (label)
}

/** ฟิลด์ถัดไปที่ต้องถาม; null = ครบแล้ว พร้อมค้นแผน */
export function nextField(known: IntakeKnown): IntakeField | null {
  if (!known.brand) return 'brand';
  if (!known.model) return 'model';
  if (!known.year) return 'year';
  if (!known.province) return 'province';
  return null;
}

/** ข้อความคำถามภาษาไทยของแต่ละฟิลด์ (โทน "พิมพ์" — ที่ปรึกษาประกัน) */
export function questionFor(field: IntakeField, known: IntakeKnown): string {
  switch (field) {
    case 'brand':
      return 'ขอทราบ **ยี่ห้อรถ** หน่อยค่ะ 🚗 พิมพ์ชื่อยี่ห้อ หรือกดเลือกจากปุ่มด้านล่างได้เลยค่ะ';
    case 'model':
      return `รถ **${known.brand}** รุ่นไหนคะ? กดเลือกจากปุ่มด้านล่าง หรือพิมพ์ชื่อรุ่นได้เลยค่ะ`;
    case 'year':
      return `**${known.brand} ${known.model}** ปีอะไรคะ?`;
    case 'province':
      return 'รถจดทะเบียนที่ **จังหวัด** อะไรคะ?';
  }
}
