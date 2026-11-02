import { Injectable } from '@angular/core';
import { PackageRow } from './motor-facet';

export interface ShareableQuote {
  package: PackageRow;
  brand: string;
  model: string;
  year: number;
  provinceLabel?: string;
}

/** สร้างใบเสนอราคาแบบ print-ready และข้อความส่ง LINE จากแผนจริงที่ลูกค้าเลือก */
@Injectable({ providedIn: 'root' })
export class QuoteShare {
  printPdf(quote: ShareableQuote): void {
    const w = window.open('', '_blank');
    if (!w) return;
    w.opener = null;

    const p = quote.package;
    const money = (v: number) => new Intl.NumberFormat('th-TH').format(v || 0);
    const date = new Intl.DateTimeFormat('th-TH', { dateStyle: 'long', timeZone: 'Asia/Bangkok' }).format(new Date());
    const esc = (value: string) => (value || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    const row = (label: string, value: string) => `<div class="row"><span>${esc(label)}</span><strong>${esc(value)}</strong></div>`;

    w.document.write(`<!doctype html><html lang="th"><head><meta charset="utf-8"><title>ใบสรุปแผนประกัน - InsureChat</title>
      <style>
        @page { size: A4; margin: 16mm; }
        * { box-sizing: border-box; } body { margin:0; color:#172033; font-family: "Noto Sans Thai", Tahoma, Arial, sans-serif; font-size:13px; line-height:1.55; }
        .top { display:flex; justify-content:space-between; align-items:flex-start; border-bottom:2px solid #1e3a5f; padding-bottom:18px; }
        .brand { color:#17345a; font-size:25px; font-weight:800; letter-spacing:-.4px; margin:0; } .label { color:#64748b; font-size:11px; font-weight:700; letter-spacing:.06em; text-transform:uppercase; margin:3px 0 0; }
        .meta { text-align:right; color:#64748b; font-size:11px; } .meta strong { display:block; color:#172033; font-size:13px; }
        h1 { font-size:22px; line-height:1.25; margin:28px 0 4px; } .sub { color:#64748b; margin:0 0 22px; }
        .hero { background:#eff6ff; border:1px solid #bfdbfe; border-radius:10px; padding:18px 20px; margin:0 0 20px; } .insurer { font-size:18px; font-weight:800; margin:0; } .plan { color:#475569; margin:3px 0 0; }
        .premium { margin-top:14px; color:#0f3b6d; font-size:14px; } .premium strong { font-size:26px; letter-spacing:-.5px; }
        .grid { display:grid; grid-template-columns:1fr 1fr; gap:14px; } .card { border:1px solid #dbe3ee; border-radius:10px; overflow:hidden; } .card h2 { margin:0; padding:10px 14px; font-size:12px; background:#f8fafc; border-bottom:1px solid #dbe3ee; } .body { padding:4px 14px; }
        .row { display:flex; justify-content:space-between; gap:16px; padding:8px 0; border-bottom:1px solid #edf1f6; } .row:last-child { border-bottom:none; } .row span { color:#64748b; } .row strong { text-align:right; font-weight:650; }
        .notice { margin-top:18px; padding:12px 14px; border-left:3px solid #d97706; background:#fffbeb; color:#78350f; font-size:11px; } footer { margin-top:25px; color:#94a3b8; font-size:10px; border-top:1px solid #e2e8f0; padding-top:10px; }
        @media print { body { -webkit-print-color-adjust:exact; print-color-adjust:exact; } }
      </style></head><body>
      <header class="top"><div><p class="brand">InsureChat</p><p class="label">Insurance plan summary</p></div><div class="meta"><strong>ใบสรุปแผนประกันภัย</strong>จัดทำเมื่อ ${esc(date)}</div></header>
      <h1>แผนประกันสำหรับ ${esc(quote.brand)} ${esc(quote.model)} ปี ${quote.year}</h1>
      <p class="sub">${quote.provinceLabel ? `จังหวัดจดทะเบียน: ${esc(quote.provinceLabel)}` : 'กรุณาตรวจสอบรายละเอียดรถก่อนยืนยัน'}</p>
      <section class="hero"><p class="insurer">${esc(p.insurerName)}</p><p class="plan">${esc(p.packageName)} · ${esc(p.polTypeName)} · ซ่อม${esc(p.renovType || 'ตามเงื่อนไข')}</p><div class="premium">เบี้ยรวมอากรต่อปี<br><strong>${money(p.premStm)} บาท</strong></div></section>
      <div class="grid"><section class="card"><h2>ข้อมูลแผน</h2><div class="body">${row('ประเภทประกัน', p.polTypeName)}${row('รูปแบบการซ่อม', p.renovType ? `ซ่อม${p.renovType}` : 'ตามเงื่อนไข')}${row('ทุนประกันรถยนต์', `${money(p.sumInsureMax)} บาท`)}${row('เบี้ยสุทธิ', `${money(p.premNet)} บาท/ปี`)}</div></section>
      <section class="card"><h2>ความคุ้มครองที่ระบุในแผน</h2><div class="body">${row('สูญหาย / ไฟไหม้', p.fireSuminsure > 0 ? `${money(p.fireSuminsure)} บาท` : 'ตามเงื่อนไขแผน')}${row('น้ำท่วม', p.floodSuminsure > 0 ? `${money(p.floodSuminsure)} บาท` : 'ตามเงื่อนไขแผน')}${row('ส่วนลด', p.discount > 0 ? `${money(p.discount)} ${p.discType || ''}` : 'ไม่มีข้อมูล')}</div></section></div>
      <aside class="notice"><strong>หมายเหตุสำคัญ:</strong> เอกสารนี้เป็นสรุปข้อมูลแผนเพื่อใช้เปรียบเทียบเท่านั้น อัตราเบี้ยและความคุ้มครองต้องยืนยันกับตัวแทนและเงื่อนไขกรมธรรม์ก่อนออกกรมธรรม์จริง</aside>
      <footer>InsureChat · สรุปจากข้อมูลแพ็กเกจที่เลือก · โปรดเก็บเอกสารนี้ไว้ประกอบการตัดสินใจ</footer>
      <script>window.onload = () => window.print();<\/script></body></html>`);
    w.document.close();
  }

  shareLine(quote: ShareableQuote): void {
    const p = quote.package;
    const money = (v: number) => new Intl.NumberFormat('th-TH').format(v || 0);
    const text = [
      'สรุปแผนประกันจาก InsureChat',
      `รถ: ${quote.brand} ${quote.model} ปี ${quote.year}`,
      `บริษัท: ${p.insurerName}`,
      `แผน: ${p.packageName} (${p.polTypeName}, ซ่อม${p.renovType || 'ตามเงื่อนไข'})`,
      `ทุนประกัน: ${money(p.sumInsureMax)} บาท`,
      `เบี้ยรวมอากร: ${money(p.premStm)} บาท/ปี`,
      'หมายเหตุ: ยืนยันเงื่อนไขและราคาอีกครั้งกับตัวแทนก่อนออกกรมธรรม์จริง'
    ].join('\n');
    window.open(`https://line.me/R/msg/text/?${encodeURIComponent(text)}`, '_blank', 'noopener,noreferrer');
  }
}
