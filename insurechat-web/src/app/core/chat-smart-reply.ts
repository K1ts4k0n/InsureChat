import { PackageRow } from './motor-facet';

export interface SmartReplyResult { reply: string; suggestions: string[]; }

const money = (value: number | undefined): string =>
  typeof value === 'number' && value >= 0 ? value.toLocaleString('en-US') : 'ไม่มีข้อมูล';

/** Renders only package-search data. One package is always exactly one table row. */
export function renderPlanTable(plans: PackageRow[]): string {
  const header = [
    '| # | บริษัท | ชั้น | ซ่อม | ทุนประกัน | เบี้ย/ปี | ไฟไหม้ | Deduct | อุปกรณ์ตกแต่ง | น้ำท่วม |',
    '|---:|---|---|---|---:|---:|---:|---:|---|---:|'
  ];
  const rows = plans.map((p, index) =>
    `| ${index + 1} | ${p.insurerName || 'ไม่มีข้อมูล'} | ${p.polTypeName || 'ไม่มีข้อมูล'} | ${p.renovType || 'ไม่มีข้อมูล'} | ${money(p.sumInsureMax)} | ${money(p.premStm || p.premNet)} | ${money(p.fireSuminsure)} | ${money(p.deductible)} | ไม่มีข้อมูล | ${money(p.floodSuminsure)} |`
  );
  return [...header, ...rows].join('\n');
}

function requestedClass(query: string): string | undefined {
  if (/(^|\s)1($|\s)|ชั้น\s*1|ประเภท\s*1/.test(query)) return '1';
  if (/2\+|สองบวก|2plus/.test(query)) return '2+';
  if (/3\+|สามบวก|3plus/.test(query)) return '3+';
  if (/ชั้น\s*3|ประเภท\s*3/.test(query)) return '3';
  return undefined;
}

export function getSmartInsuranceReply(
  userText: string, carBrand?: string, carModel?: string, carYear?: number,
  candidates: PackageRow[] = []
): SmartReplyResult {
  const wanted = requestedClass((userText || '').toLowerCase().trim());
  const matching = wanted
    ? candidates.filter((p) => p.polTypeName?.replace(/\s/g, '').includes(wanted))
    : candidates;
  const plans = matching.slice(0, 10);
  if (plans.length > 0) {
    const vehicle = [carBrand, carModel, carYear ? `ปี ${carYear}` : ''].filter(Boolean).join(' ');
    const more = matching.length > plans.length ? `\n\nพบ ${matching.length} แผน แสดง 10 แผนแรกนะคะ` : '';
    return {
      reply: `${vehicle ? `แผนที่มีจริงสำหรับ ${vehicle} ค่ะ` : 'แผนที่มีจริงค่ะ'}\n\n${renderPlanTable(plans)}${more}\n\nเลือกหมายเลขแผนที่ต้องการได้เลยนะคะ`,
      suggestions: ['ดูทั้งหมด', 'แผนถูกสุด', 'เลือกแผน 1']
    };
  }
  return {
    reply: wanted
      ? `ยังไม่มีแผนชั้น ${wanted} จากข้อมูลที่เลือกค่ะ กรุณากด “คำนวณเบี้ย” เพื่อค้นหาแผนจริงก่อนนะคะ`
      : 'กรุณากด “คำนวณเบี้ย” และเลือกรถก่อน ระบบจะแสดงแผนจริงเป็นตารางให้เลือกนะคะ',
    suggestions: ['คำนวณเบี้ย', 'เลือกประเภทรถ']
  };
}
