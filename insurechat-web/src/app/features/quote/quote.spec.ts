import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { Quote } from './quote';
import { MotorFacet, PackageRow } from '../../core/motor-facet';
import { QuoteHandoff } from '../../core/quote-handoff';
import { DocIntakeHandoff } from '../../core/doc-intake-handoff';
import { QuoteShare } from '../../core/quote-share';

const plan: PackageRow = {
  packageId: 10, insurerName: 'บริษัททดสอบ', insurerLogo: '', packageName: 'แผนทดสอบ',
  polTypeName: 'ประเภท 1', renovType: 'ห้าง', sumInsureMax: 650000,
  premNet: 19000, premStm: 19800, premTotal: 19800, discount: 0, discType: '',
  fireSuminsure: 650000, floodSuminsure: 650000
};

describe('Quote', () => {
  it('shows all coverage facts and a visible select action in one plan card', async () => {
    await TestBed.configureTestingModule({
      imports: [Quote],
      providers: [
        provideRouter([]),
        { provide: MotorFacet, useValue: { brands: () => of([]), provinces: () => of([]) } },
        { provide: QuoteHandoff, useValue: { set: () => {} } },
        { provide: DocIntakeHandoff, useValue: {} },
        { provide: QuoteShare, useValue: {} }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(Quote);
    fixture.componentInstance.packages.set([plan]);
    fixture.detectChanges();

    const card = fixture.nativeElement.querySelector('[data-testid="plan-card"]');
    expect(card?.textContent).toContain('ไฟไหม้');
    expect(card?.textContent).toContain('Deduct');
    expect(card?.textContent).toContain('อุปกรณ์ตกแต่ง');
    expect(card?.textContent).toContain('น้ำท่วม');
    expect(card?.querySelector('.choose')?.textContent).toContain('เลือกแผนนี้');
  });
});
