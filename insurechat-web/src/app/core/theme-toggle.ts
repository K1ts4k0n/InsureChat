import { Component, inject } from '@angular/core';
import { ThemeService } from './theme';

/**
 * ปุ่มสลับโหมดสว่าง/มืด — reuse ได้ทุกที่ (header แบรนด์ / ลอยมุมหน้า auth).
 * สไตล์ ghost อิง currentColor → กลมกลืนทั้งบนพื้นแบรนด์ (ตัวอักษรขาว) และพื้น surface.
 * ตำแหน่ง (fixed/มุม) ปล่อยให้ parent กำหนดบน host element `theme-toggle`.
 */
@Component({
  selector: 'theme-toggle',
  standalone: true,
  template: `
    <button
      type="button"
      (click)="theme.toggle()"
      [attr.aria-pressed]="theme.isDark()"
      [attr.aria-label]="theme.isDark() ? 'สลับเป็นโหมดสว่าง' : 'สลับเป็นโหมดมืด'"
      title="สลับโหมดสว่าง / มืด"
    >
      @if (theme.isDark()) {
        <!-- sun (กำลังมืด → กดเพื่อสว่าง) -->
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"
             stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <circle cx="12" cy="12" r="4" />
          <path d="M12 2v2M12 20v2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M2 12h2M20 12h2M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4" />
        </svg>
      } @else {
        <!-- moon (กำลังสว่าง → กดเพื่อมืด) -->
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"
             stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z" />
        </svg>
      }
    </button>
  `,
  styles: `
    :host { display: inline-grid; place-items: center; }
    button {
      display: inline-grid;
      place-items: center;
      width: 36px;
      height: 36px;
      padding: 0;
      color: inherit;
      background: transparent;
      border: 1px solid color-mix(in srgb, currentColor 38%, transparent);
      border-radius: var(--r-pill);
      cursor: pointer;
      transition: background var(--dur) var(--ease), transform var(--dur) var(--ease),
        border-color var(--dur) var(--ease);
    }
    button:hover { background: color-mix(in srgb, currentColor 14%, transparent); }
    button:active { transform: scale(0.94); }
    svg { width: 18px; height: 18px; display: block; }
  `
})
export class ThemeToggle {
  protected readonly theme = inject(ThemeService);
}
