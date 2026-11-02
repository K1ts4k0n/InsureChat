import { Injectable, signal } from '@angular/core';

/**
 * Surface theme (light/dark) — แกนแยกจาก brand color (ADR-0010 amend 2026-06-09).
 * per-user + ตามอุปกรณ์: ค่าเริ่มจาก prefers-color-scheme, override ด้วย toggle, จำใน localStorage.
 * ไม่ขึ้น server (ต่างจาก brand color ที่ admin ตั้ง global).
 */
type Mode = 'light' | 'dark';
const KEY = 'insurechat-surface-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly isDark = signal(false);

  /** เรียกตอน bootstrap (provideAppInitializer) — set [data-theme] บน <html> ก่อนหน้าแรกวาด */
  init(): void {
    const stored = localStorage.getItem(KEY) as Mode | null;
    const prefersDark = window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
    this.apply(stored ?? (prefersDark ? 'dark' : 'light'));
  }

  toggle(): void {
    this.apply(this.isDark() ? 'light' : 'dark');
  }

  private apply(mode: Mode): void {
    document.documentElement.setAttribute('data-theme', mode);
    localStorage.setItem(KEY, mode);
    this.isDark.set(mode === 'dark');
  }
}
