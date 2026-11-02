import { nextField, questionFor } from './intake-gate';

describe('intake-gate', () => {
  it('ไม่รู้อะไรเลย → ถามยี่ห้อก่อน', () => {
    expect(nextField({})).toBe('brand');
  });

  it('รู้ยี่ห้อ → ถามรุ่น', () => {
    expect(nextField({ brand: 'KIA' })).toBe('model');
  });

  it('รู้ยี่ห้อ+รุ่น → ถามปี', () => {
    expect(nextField({ brand: 'KIA', model: 'SORENTO 2.2' })).toBe('year');
  });

  it('รู้รถครบ → ถามจังหวัด', () => {
    expect(nextField({ brand: 'KIA', model: 'SORENTO 2.2', year: 2020 })).toBe('province');
  });

  it('ครบทุกฟิลด์ → null', () => {
    expect(nextField({ brand: 'KIA', model: 'SORENTO 2.2', year: 2020, province: '10' })).toBeNull();
  });

  it('null / undefined นับว่ายังขาด', () => {
    expect(nextField({ brand: null, model: 'X' })).toBe('brand');
    expect(nextField({ brand: 'KIA', model: 'X', year: null })).toBe('year');
  });

  it('คำถามรุ่นอ้างถึงยี่ห้อที่รู้แล้ว', () => {
    expect(questionFor('model', { brand: 'HONDA' })).toContain('HONDA');
  });

  it('คำถามปีอ้างถึงยี่ห้อ+รุ่น', () => {
    const q = questionFor('year', { brand: 'HONDA', model: 'CIVIC 1.5' });
    expect(q).toContain('HONDA');
    expect(q).toContain('CIVIC 1.5');
  });
});
