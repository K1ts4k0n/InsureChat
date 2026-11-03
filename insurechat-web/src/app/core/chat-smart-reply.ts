import { PackageRow } from './motor-facet';

export interface SmartReplyResult {
  reply: string;
  suggestions: string[];
  selectedPlan?: PackageRow;
  contact?: { name: string; phone: string };
  readyToSubmit?: boolean;
}

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

function selectedChoice(query: string): number | undefined {
  const match = query.match(/^(?:เลือก(?:แผน)?|แผน(?:ที่)?|เอา(?:แผน)?|ขอ(?:แผน)?)?\s*(\d+)\s*$/);
  return match ? Number(match[1]) : undefined;
}

function contactFrom(message: string): { name: string; phone: string } | undefined {
  const phoneMatch = message.match(/0\d(?:[ -]?\d){7,8}/);
  if (!phoneMatch) return undefined;
  const phone = phoneMatch[0].replace(/\D/g, '');
  const name = message.replace(phoneMatch[0], '').trim();
  return name && phone.length >= 9 && phone.length <= 10 ? { name, phone } : undefined;
}

export function getSmartInsuranceReply(
  userText: string, carBrand?: string, carModel?: string, carYear?: number,
  candidates: PackageRow[] = [], currentStep?: string
): SmartReplyResult {
  const query = (userText || '').toLowerCase().trim();
  if (currentStep === 'ask_customer') {
    const contact = contactFrom(userText);
    if (contact) {
      return {
        reply: `บันทึกข้อมูลติดต่อแล้วค่ะ กรุณากด “ส่งคำสั่งซื้อ” เพื่อส่งข้อมูลให้เจ้าหน้าที่นะคะ`,
        suggestions: ['ส่งคำสั่งซื้อ'], contact, readyToSubmit: true
      };
    }
    return { reply: 'รบกวนพิมพ์ชื่อ และเบอร์โทรศัพท์ 9–10 หลัก เพื่อดำเนินการต่อค่ะ', suggestions: [] };
  }

  const choice = selectedChoice(query);
  if (choice && candidates[choice - 1]) {
    const selectedPlan = candidates[choice - 1];
    return {
      reply: `เลือกแผน ${choice}: ${selectedPlan.insurerName} แล้วค่ะ\n\nรบกวนพิมพ์ชื่อ และเบอร์โทรศัพท์ เพื่อให้เจ้าหน้าที่ดำเนินการต่อค่ะ`,
      suggestions: [], selectedPlan
    };
  }

  const wanted = requestedClass(query);
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
