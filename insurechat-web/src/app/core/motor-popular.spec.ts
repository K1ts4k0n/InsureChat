import { brandSlug, topBrands, topProvinces } from './motor-popular';
import { FacetItem } from './motor-facet';

const F = (...v: string[]): FacetItem[] => v.map((x) => ({ value: x, label: x }));

describe('motor-popular', () => {
  it('brandSlug ตัดจุดและเว้นวรรค', () => {
    expect(brandSlug('B.M.W.')).toBe('bmw');
    expect(brandSlug('GWM TANK')).toBe('gwm-tank');
  });

  it('topBrands ดันยี่ห้อยอดนิยมขึ้นก่อน', () => {
    const out = topBrands(F('KIA', 'TOYOTA', 'HONDA'), 8).map((x) => x.value);
    expect(out).toEqual(['TOYOTA', 'HONDA', 'KIA']);
  });

  it('topBrands ตัดตามจำนวนที่ขอ', () => {
    expect(topBrands(F('TOYOTA', 'HONDA', 'ISUZU', 'MITSUBISHI'), 2).length).toBe(2);
  });

  it('topProvinces เอาจังหวัดที่เดาได้ขึ้นก่อน และตัด row ทดสอบ', () => {
    const all: FacetItem[] = [
      { value: '50', label: 'เชียงใหม่' },
      { value: '10', label: 'กรุงเทพมหานคร' },
      { value: '99', label: 'จังหวัดทดสอบ' }
    ];
    const out = topProvinces(all, '50', 8);
    expect(out[0].value).toBe('50');
    expect(out.some((p) => p.label.includes('ทดสอบ'))).toBe(false);
  });

  it('topProvinces เรียงตามประชากรเมื่อไม่มีจังหวัดที่เดาได้', () => {
    const all: FacetItem[] = [
      { value: '50', label: 'เชียงใหม่' },
      { value: '10', label: 'กรุงเทพมหานคร' }
    ];
    expect(topProvinces(all, '', 8)[0].label).toBe('กรุงเทพมหานคร');
  });
});
