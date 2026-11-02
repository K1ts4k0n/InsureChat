import { Injectable } from '@angular/core';
import { PackageRow } from './motor-facet';

export interface QuoteHandoffData {
  candidates: PackageRow[];   // candidate set จากการค้น 4 ฟิลด์ → ให้ AI สโคป (ADR 0005)
  brand: string;
  model: string;
  cc: string;
  year: number;
  province?: string;          // จังหวัดจดทะเบียน (ADR 0005 — เก็บลง proposal, ไม่กรองผลค้น/เบี้ย)
  provinceLabel?: string;
}

/** ส่งต่อ candidate set จากหน้า quote (5 ฟิลด์) → หน้า chat (one-shot) */
@Injectable({ providedIn: 'root' })
export class QuoteHandoff {
  private pending: QuoteHandoffData | null = null;

  set(data: QuoteHandoffData): void { this.pending = data; }

  /** อ่านแล้วล้าง (ใช้ครั้งเดียว) */
  consume(): QuoteHandoffData | null {
    const d = this.pending;
    this.pending = null;
    return d;
  }
}
