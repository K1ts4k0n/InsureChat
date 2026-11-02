import { FacetItem } from './motor-facet';

/** normalize ชื่อยี่ห้อ → slug ของไฟล์โลโก้ (เช่น "B.M.W." → "bmw", "GWM TANK" → "gwm-tank") */
export function brandSlug(v: string): string {
  return v.toLowerCase().replace(/\./g, '').trim().replace(/\s+/g, '-');
}

// ลำดับยี่ห้อยอดนิยมในตลาดไทย (slug → อันดับ) — ดันตัวที่มีโลโก้ขึ้นก่อน เลียนการจัดเรียงของ TQM
const BRAND_RANK: Record<string, number> = {
  toyota: 0, honda: 1, isuzu: 2, mitsubishi: 3, nissan: 4, mazda: 5, ford: 6,
  mg: 7, suzuki: 8, 'mercedes-benz': 9, bmw: 10, hyundai: 11, byd: 12, kia: 13,
  chevrolet: 14, subaru: 15, lexus: 16, volvo: 17, mini: 18, audi: 19
};

/** ยี่ห้อยอดนิยม n ตัว (ที่เหลือให้เลือกจาก dropdown / พิมพ์เอง) */
export function topBrands(all: FacetItem[], n = 8): FacetItem[] {
  const rank = (x: FacetItem) => BRAND_RANK[brandSlug(x.value)] ?? 999;
  return [...all].sort((a, b) => rank(a) - rank(b)).slice(0, n);
}

// จังหวัดประชากรมากสุด (ไทย) เรียงลำดับ — ดันขึ้นก่อนใน tile (ไม่รวมจังหวัดลูกค้า)
const POP_PROVINCES = [
  'กรุงเทพ', 'นครราชสีมา', 'อุบลราชธานี', 'ขอนแก่น', 'เชียงใหม่', 'บุรีรัมย์',
  'อุดรธานี', 'นครศรีธรรมราช', 'ศรีสะเกษ', 'ชลบุรี', 'สงขลา', 'สุรินทร์',
  'นนทบุรี', 'เชียงราย', 'สมุทรปราการ', 'ร้อยเอ็ด', 'ชัยภูมิ', 'สกลนคร',
  'ปทุมธานี', 'นครสวรรค์', 'สุราษฎร์ธานี', 'นครปฐม', 'กาฬสินธุ์', 'พิษณุโลก'
];

function provRank(label: string): number {
  for (let i = 0; i < POP_PROVINCES.length; i++) if (label.includes(POP_PROVINCES[i])) return i;
  return 999;
}

/** จังหวัด n ตัว: จังหวัดที่เดาได้ (จาก IP) มาก่อน แล้วประชากรมากสุด (ไม่ซ้ำ); ตัด row "ทดสอบ" ใน DB ทิ้ง */
export function topProvinces(all: FacetItem[], detectedValue: string, n = 8): FacetItem[] {
  const list = all.filter((p) => !p.label.includes('ทดสอบ'));
  const det = list.find((p) => p.value === detectedValue);
  const rest = list
    .filter((p) => p.value !== detectedValue)
    .sort((a, b) => provRank(a.label) - provRank(b.label));
  return (det ? [det, ...rest] : rest).slice(0, n);
}
