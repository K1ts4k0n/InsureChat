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

describe('getSmartInsuranceReply', () => {
  it('moves an explicit plan selection to contact collection instead of rendering the table again', async () => {
    const { getSmartInsuranceReply } = await import('./chat-smart-reply');

    const reply = getSmartInsuranceReply('เลือก 1', 'NISSAN', 'NAVARA', 2014, [plan()]);

    expect(reply.selectedPlan?.packageId).toBe(10);
    expect(reply.reply).toContain('ชื่อ และเบอร์โทรศัพท์');
    expect(reply.reply).not.toContain('| # | บริษัท |');
  });

  it('accepts contact details after a selection and marks the fallback flow ready to submit', async () => {
    const { getSmartInsuranceReply } = await import('./chat-smart-reply');

    const reply = getSmartInsuranceReply('สมชาย 0812345678', 'NISSAN', 'NAVARA', 2014, [plan()], 'ask_customer');

    expect(reply.contact).toEqual({ name: 'สมชาย', phone: '0812345678' });
    expect(reply.readyToSubmit).toBe(true);
  });
});
