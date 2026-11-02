import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

/**
 * Brand color (global) — admin ตั้งผ่าน /admin, เก็บฝั่ง server (ADR 0010/0011).
 * ดึงตอน bootstrap แล้ว set CSS var --brand/--brand-hover/--on-brand บน :root.
 * ถ้าดึงไม่ได้ → ใช้ default ที่ฝังใน styles.css (แอปไม่พัง).
 */
@Injectable({ providedIn: 'root' })
export class BrandTheme {
  /** เรียกตอน bootstrap (provideAppInitializer) — public, ก่อน login ก็ธีมแล้ว */
  async init(): Promise<void> {
    try {
      const res = await fetch(`${environment.apiUrl}/Settings/Theme`);
      if (!res.ok) return;
      const data = await res.json();
      if (data?.brand) this.apply(data.brand);
    } catch {
      /* server ล่ม/ยังไม่ตั้ง → ใช้ default ใน CSS */
    }
  }

  /** set CSS variables ของแบรนด์ + derive hover/on-brand ให้ contrast ผ่าน */
  apply(brand: string): void {
    if (!/^#[0-9a-fA-F]{6}$/.test(brand)) return;
    const root = document.documentElement;
    root.style.setProperty('--brand', brand);
    root.style.setProperty('--brand-hover', this.darken(brand, 0.12));
    root.style.setProperty('--on-brand', this.onColor(brand));
  }

  private rgb(hex: string): [number, number, number] {
    const n = parseInt(hex.slice(1), 16);
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
  }

  private darken(hex: string, amt: number): string {
    const [r, g, b] = this.rgb(hex).map((c) => Math.max(0, Math.round(c * (1 - amt)))) as number[];
    return '#' + [r, g, b].map((c) => c.toString(16).padStart(2, '0')).join('');
  }

  /** ขาว/ดำ บนพื้นแบรนด์ — เลือกจาก relative luminance กัน contrast พัง */
  private onColor(hex: string): string {
    const [r, g, b] = this.rgb(hex).map((c) => {
      const s = c / 255;
      return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
    });
    const lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
    return lum > 0.45 ? '#0f172a' : '#ffffff';
  }
}
