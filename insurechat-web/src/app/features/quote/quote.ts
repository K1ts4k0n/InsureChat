import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MotorFacet, FacetItem, DocumentExtractResponse } from '../../core/motor-facet';
import { QuoteHandoff } from '../../core/quote-handoff';
import { ThemeToggle } from '../../core/theme-toggle';
import { validateDocFile, downscaleImage } from '../../core/doc-upload';
import { brandSlug, topBrands, topProvinces } from '../../core/motor-popular';
import { DocIntakeHandoff } from '../../core/doc-intake-handoff';

@Component({
  selector: 'app-quote',
  imports: [CommonModule, FormsModule, RouterLink, ThemeToggle],
  templateUrl: './quote.html',
  styleUrl: './quote.css'
})
export class Quote implements OnInit {
  // 4 ฟิลด์ = ระบุตัวรถ (ADR 0005). ความคุ้มครอง/งบ ไปสโคปในแชต
  brands = signal<FacetItem[]>([]);
  models = signal<FacetItem[]>([]);
  years = signal<FacetItem[]>([]);
  provinces = signal<FacetItem[]>([]);          // ฟิลด์ที่ 5 (ADR 0005) — เก็บลง proposal ไม่กรองผล

  brand = ''; model = ''; cc = ''; year = ''; province = '';
  detectedProvince = signal<string>('');         // value ของจังหวัดลูกค้า (เดาจาก IP) → tile แรก

  loading = signal(false);
  error = signal('');
  docLoading = signal(false);
  docDragging = signal(false);
  isOfflineMode;

  constructor(
    private facet: MotorFacet,
    private handoff: QuoteHandoff,
    private docIntake: DocIntakeHandoff,
    private router: Router
  ) {
    this.isOfflineMode = this.facet.isOfflineMode;
  }

  ngOnInit(): void { this.loadBrands(); this.loadProvinces(); }

  /** โหลดรายชื่อยี่ห้อ — แยกเมธอดเพื่อให้ปุ่ม "ลองใหม่" เรียกซ้ำได้เมื่อโหลดพลาด */
  loadBrands(): void {
    this.error.set('');
    this.facet.brands().subscribe({
      next: (b) => this.brands.set(b),
      error: () => this.error.set('โหลดยี่ห้อรถไม่สำเร็จ (เชื่อมฐานข้อมูลไม่ได้?)')
    });
  }

  loadProvinces(): void {
    this.facet.provinces().subscribe({
      next: (p) => { this.provinces.set(p); this.detectProvince(); },
      error: () => { /* เงียบ — จังหวัดเป็นฟิลด์เสริม ไม่บล็อกการค้นหา */ }
    });
  }

  /** เดาจังหวัดลูกค้าจาก IP (ส่งเฉพาะ IP ตัวเอง) → ตั้งเป็น tile แรก + เลือกให้อัตโนมัติ */
  private detectProvince(): void {
    fetch('https://ipapi.co/json/')
      .then((r) => (r.ok ? r.json() : null))
      .then((d) => {
        const th = this.enToThProvince((d?.region ?? '').toString());
        if (!th) return;
        const match = this.provinces().find((p) => p.label.includes(th) || th.includes(p.label));
        if (match) {
          this.detectedProvince.set(match.value);
          if (!this.province) this.province = match.value;
        }
      })
      .catch(() => { /* เน็ต/บริการล่ม → ไม่เดา ลูกค้าเลือกเอง */ });
  }

  // map ชื่อจังหวัดอังกฤษ (จาก IP) → ไทย (ครอบจังหวัดหลัก/เมือง/ท่องเที่ยว)
  private readonly enToTh: Record<string, string> = {
    'Bangkok': 'กรุงเทพ', 'Samut Prakan': 'สมุทรปราการ', 'Nonthaburi': 'นนทบุรี',
    'Pathum Thani': 'ปทุมธานี', 'Chon Buri': 'ชลบุรี', 'Chonburi': 'ชลบุรี',
    'Rayong': 'ระยอง', 'Chiang Mai': 'เชียงใหม่', 'Chiang Rai': 'เชียงราย',
    'Nakhon Ratchasima': 'นครราชสีมา', 'Khon Kaen': 'ขอนแก่น', 'Udon Thani': 'อุดรธานี',
    'Ubon Ratchathani': 'อุบลราชธานี', 'Nakhon Si Thammarat': 'นครศรีธรรมราช',
    'Songkhla': 'สงขลา', 'Phuket': 'ภูเก็ต', 'Surat Thani': 'สุราษฎร์ธานี',
    'Nakhon Pathom': 'นครปฐม', 'Samut Sakhon': 'สมุทรสาคร',
    'Phra Nakhon Si Ayutthaya': 'อยุธยา', 'Ayutthaya': 'อยุธยา', 'Saraburi': 'สระบุรี',
    'Lampang': 'ลำปาง', 'Phitsanulok': 'พิษณุโลก', 'Nakhon Sawan': 'นครสวรรค์',
    'Buri Ram': 'บุรีรัมย์', 'Buriram': 'บุรีรัมย์', 'Surin': 'สุรินทร์',
    'Si Sa Ket': 'ศรีสะเกษ', 'Sisaket': 'ศรีสะเกษ', 'Roi Et': 'ร้อยเอ็ด',
    'Chaiyaphum': 'ชัยภูมิ', 'Sakon Nakhon': 'สกลนคร', 'Kalasin': 'กาฬสินธุ์',
    'Maha Sarakham': 'มหาสารคาม', 'Trang': 'ตรัง', 'Krabi': 'กระบี่',
    'Prachuap Khiri Khan': 'ประจวบ', 'Phetchaburi': 'เพชรบุรี',
    'Chachoengsao': 'ฉะเชิงเทรา', 'Lop Buri': 'ลพบุรี', 'Lopburi': 'ลพบุรี'
  };
  private enToThProvince(region: string): string {
    const r = region.trim();
    if (this.enToTh[r]) return this.enToTh[r];
    for (const k of Object.keys(this.enToTh)) if (r && (r.includes(k) || k.includes(r))) return this.enToTh[k];
    return '';
  }

  /** tile จังหวัด 8 ตัว: จังหวัดลูกค้า (จาก IP) มาก่อน แล้วประชากรมากสุด */
  get provinceTiles(): FacetItem[] {
    return topProvinces(this.provinces(), this.detectedProvince(), 8);
  }

  selectProvince(v: string): void { this.province = v; this.onProvinceChange(); }
  onProvinceChange(): void { if (this.province) this.scrollToStep('search-row'); }

  onBrandChange(): void {
    this.model = this.year = '';
    this.models.set([]); this.years.set([]); this.error.set('');
    if (this.brand) {
      this.facet.models(this.brand).subscribe({
        next: (m) => this.models.set(m),
        error: () => this.error.set('โหลดรุ่นรถไม่สำเร็จ (เชื่อมฐานข้อมูลไม่ได้?)')
      });
      this.scrollToStep('step-model');   // เลือกยี่ห้อแล้ว → เลื่อนไปการ์ดรุ่น
    }
  }
  onModelChange(): void {
    this.year = ''; this.years.set([]); this.error.set('');
    if (this.model) {
      // ตัด step CC ออก → ส่ง cc ว่าง = ปีรวมทุกขนาดเครื่อง (AI สโคป CC ทีหลังถ้าต้องการ)
      this.facet.years(this.brand, this.model, '').subscribe({
        next: (y) => this.years.set(y),
        error: () => this.error.set('โหลดปีรถไม่สำเร็จ (เชื่อมฐานข้อมูลไม่ได้?)')
      });
      this.scrollToStep('step-year');   // เลือกรุ่นแล้ว → เลื่อนไปการ์ดปี
    }
  }
  onYearChange(): void {
    if (this.year) this.scrollToStep('step-province');   // เลือกปีแล้ว → เลื่อนไปการ์ดจังหวัด
  }

  /** เลื่อนการ์ด step ถัดไปเข้ามาในจอแบบนุ่ม (เผื่อ DOM/รายการ async ด้วย setTimeout) */
  private scrollToStep(id: string): void {
    setTimeout(() => document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' }), 80);
  }

  /** tile ยี่ห้อยอดนิยม 8 ตัว (2 แถว × 4 — ที่เหลือเลือกจาก dropdown ใต้การ์ด) */
  get brandTiles(): FacetItem[] {
    return topBrands(this.brands(), 8);
  }

  /** โลโก้แบรนด์รถจาก public CDN (car-logos-dataset). โหลดไม่ได้ → ซ่อนรูป เหลือชื่อ (ดู onLogoError) */
  brandLogo(v: string): string {
    return `https://cdn.jsdelivr.net/gh/filippofilip95/car-logos-dataset@master/logos/optimized/${brandSlug(v)}.png`;
  }

  /** โลโก้ที่ CDN ไม่มี (404/403) → ซ่อน <img> ให้เหลือแต่ชื่อยี่ห้อในปุ่ม */
  onLogoError(ev: Event): void { (ev.target as HTMLElement).style.display = 'none'; }

  // nameplate ยอดนิยมในไทย เรียงตามยอดขายคร่าว ๆ (เฉพาะ→ทั่วไป เช่น "HILUX REVO" ก่อน "HILUX")
  // ใช้ทั้ง (1) ดันรุ่นเด่นขึ้นก่อน (2) จับกลุ่ม nameplate เพื่อโชว์รุ่นละ 1 ปุ่ม (กันเครื่องยนต์ซ้ำ)
  private readonly popularModels = [
    'HILUX REVO', 'HILUX', 'REVO', 'D-MAX', 'RANGER', 'TRITON', 'NAVARA', 'BT-50',
    'YARIS ATIV', 'YARIS', 'VIOS', 'CITY', 'ALMERA', 'MARCH', 'SWIFT', 'CELERIO',
    'MAZDA2', 'ATTRAGE', 'MIRAGE', 'CIAZ', 'BRIO', 'JAZZ', 'NOTE',
    'COROLLA CROSS', 'FORTUNER', 'MU-X', 'EVEREST', 'TERRA', 'PAJERO',
    'CR-V', 'HR-V', 'BR-V', 'XPANDER', 'VELOZ', 'AVANZA', 'ERTIGA', 'XL7', 'MOBILIO',
    'KICKS', 'ZS', 'HS', 'CX-3', 'CX-30', 'CX-5', 'CX-8',
    'COROLLA ALTIS', 'COROLLA', 'CIVIC', 'CAMRY', 'ACCORD', 'C-HR',
    'SYLPHY', 'TEANA', 'MAZDA3', 'MG3', 'MG5', 'EP', 'VS', 'RAPTOR', 'ECOSPORT', 'MU-7'
  ];

  /** หา nameplate ที่ match (คืน index + คีย์) — index ต่ำ = นิยมกว่า; ไม่ match = (999, ชื่อเต็ม) */
  private namePlate(v: string): { rank: number; key: string } {
    const u = v.toUpperCase();
    for (let i = 0; i < this.popularModels.length; i++) {
      if (u.includes(this.popularModels[i])) return { rank: i, key: this.popularModels[i] };
    }
    return { rank: 999, key: u };
  }

  /** รุ่นเด่น 8 ตัวเป็น tile — รุ่นละ 1 ปุ่ม (dedup เครื่องยนต์), ที่เหลือเลือกจาก dropdown */
  get modelTiles(): FacetItem[] {
    const sorted = [...this.models()].sort((a, b) => this.namePlate(a.value).rank - this.namePlate(b.value).rank);
    const seen = new Set<string>();
    const out: FacetItem[] = [];
    for (const m of sorted) {
      const k = this.namePlate(m.value).key;
      if (seen.has(k)) continue;
      seen.add(k);
      out.push(m);
      if (out.length >= 8) break;
    }
    return out;
  }

  // กด tile = ตั้งค่าเดียวกับ select แล้วเรียก handler เดิม (ลอจิก facet ไม่เปลี่ยน)
  selectBrand(v: string): void { this.brand = v; this.onBrandChange(); }
  selectModel(v: string): void { this.model = v; this.onModelChange(); }
  selectYear(v: string): void { this.year = v; this.onYearChange(); }

  get canSearch(): boolean { return !!(this.brand && this.model && this.year); }

  /** แนบเอกสารรถ → สกัด → ส่งผลเข้าแชต (ฟิลด์ที่ขาด แชตถามเอง); ล้มเหลว → อยู่หน้านี้ โชว์ error */
  onDocSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';               // reset ให้เลือกไฟล์เดิมซ้ำได้
    if (file) this.ingestDoc(file);
  }

  onDocDragOver(ev: DragEvent): void {
    ev.preventDefault();
    if (!this.docLoading()) this.docDragging.set(true);
  }

  onDocDragLeave(ev: DragEvent): void {
    ev.preventDefault();
    this.docDragging.set(false);
  }

  onDocDrop(ev: DragEvent): void {
    ev.preventDefault();
    this.docDragging.set(false);
    if (this.docLoading()) return;
    const file = ev.dataTransfer?.files?.[0];
    if (file) this.ingestDoc(file);
  }

  private ingestDoc(file: File): void {
    const err = validateDocFile(file);
    if (err) { this.error.set(err); return; }

    this.docLoading.set(true); this.error.set('');
    downscaleImage(file).then((blob) => {
      this.facet.extractDocument(blob, file.name).subscribe({
        next: (r) => { this.docLoading.set(false); this.applyExtraction(r); },
        error: (e) => { this.docLoading.set(false); this.error.set(e?.error?.message ?? 'อ่านเอกสารไม่สำเร็จ'); }
      });
    });
  }

  /** แนบเอกสารสำเร็จ → ส่งผลเข้าแชตทันที; ฟิลด์ที่ขาด แชตจะถามเอง (intake gate — ADR 0019) */
  private applyExtraction(r: DocumentExtractResponse): void {
    const label = this.provinces().find((p) => p.value === this.province)?.label ?? '';
    this.docIntake.set({
      table: r.table,
      resolved: r.resolved,
      province: this.province || undefined,
      provinceLabel: label || undefined
    });
    this.router.navigate(['/chat']);
  }

  search(): void {
    if (!this.canSearch || this.loading()) return;
    this.loading.set(true); this.error.set('');
    this.facet.search({ brand: this.brand, model: this.model, cc: this.cc, year: +this.year, sort: 'prem' })
      .subscribe({
        next: (rows) => {
          this.loading.set(false);
          if (!rows || rows.length === 0) {
            this.error.set('ไม่พบแผนประกันสำหรับรถคันนี้ ลองปรับปี/รุ่น/CC อีกครั้งค่ะ');
            return;
          }
          const provLabel = this.provinces().find((p) => p.value === this.province)?.label ?? '';
          this.handoff.set({
            candidates: rows, brand: this.brand, model: this.model, cc: this.cc, year: +this.year,
            province: this.province, provinceLabel: provLabel
          });
          this.router.navigate(['/chat']);
        },
        error: () => { this.loading.set(false); this.error.set('ค้นหาไม่สำเร็จ โปรดลองใหม่'); }
      });
  }
}
