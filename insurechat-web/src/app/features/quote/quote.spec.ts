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
  it('keeps a visible select-plan button beside the plan name', async () => {
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

    expect(fixture.nativeElement.querySelector('.prod .choose')?.textContent).toContain('เลือกแผนนี้');
  });
});
