import { renderPlanTable } from './chat-smart-reply';
import { PackageRow } from './motor-facet';

const plan = (overrides: Partial<PackageRow> = {}): PackageRow => ({
  packageId: 10, insurerName: 'บริษัททดสอบ', insurerLogo: '', packageName: 'แผนทดสอบ',
  polTypeName: '1', renovType: 'ห้าง', sumInsureMax: 550000, premNet: 17000,
  premStm: 17500, premTotal: 18000, discount: 0, discType: '',
  fireSuminsure: 550000, floodSuminsure: 100000, deductible: 2000,
  ...overrides
});

describe('renderPlanTable', () => {
  it('renders the required columns and one factual row per plan', () => {
    const output = renderPlanTable([plan(), plan({ packageId: 11, insurerName: 'บริษัทสอง' })]);

    expect(output).toContain('| # | บริษัท | ชั้น | ซ่อม | ทุนประกัน | เบี้ย/ปี | ไฟไหม้ | Deduct | อุปกรณ์ตกแต่ง | น้ำท่วม |');
    expect(output).toContain('| 1 | บริษัททดสอบ | 1 | ห้าง | 550,000 | 17,500 | 550,000 | 2,000 | ไม่มีข้อมูล | 100,000 |');
    expect(output).toContain('| 2 | บริษัทสอง | 1 | ห้าง | 550,000 | 17,500 | 550,000 | 2,000 | ไม่มีข้อมูล | 100,000 |');
  });
});
