import { Component, OnInit, computed, signal, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { marked } from 'marked';
import { environment } from '../../../environments/environment';
import { Auth } from '../../core/auth';
import { QuoteHandoff } from '../../core/quote-handoff';
import { ThemeToggle } from '../../core/theme-toggle';
import { MotorFacet, MotorPackageDetail, DocumentExtractResponse, ResolvedVehicle, FacetItem } from '../../core/motor-facet';
import { validateDocFile, downscaleImage } from '../../core/doc-upload';
import { DocIntakeHandoff, DocIntakeData } from '../../core/doc-intake-handoff';
import { IntakeField, IntakeKnown, nextField, questionFor } from './intake-gate';
import { topBrands, topProvinces } from '../../core/motor-popular';
import { getSmartInsuranceReply } from '../../core/chat-smart-reply';

interface ChatbotTurn {
  timestamp: string;
  role: 'user' | 'assistant';
  text: string;
}

interface ChatbotMessageResponse {
  replies: string[];   // คำตอบแตกเป็นหลายกล่อง (ADR 0006); reply = fallback ก้อนเดียว
  reply: string;
  suggestions: string[];  // ปุ่มถามด่วนเทิร์นถัดไป (ADR 0008)
  updatedContext: any;
  isReadyToPropose: boolean;
  proposalSummary: any;
}

interface ChatbotSuggestResponse {
  suggestions: string[];
}

interface ChatbotProposalResponse {
  success: boolean;
  message: string;
  transactionId: string;
}

@Component({
  selector: 'app-chatbot',
  imports: [CommonModule, FormsModule, RouterLink, ThemeToggle],
  templateUrl: './chatbot.html',
  styleUrl: './chatbot.css'
})
export class Chatbot implements OnInit {
  messages: ChatbotTurn[] = [];
  userInput = '';
  loading = signal(false);
  isReadyToPropose = signal(false);
  proposalCollapsed = signal(false);   // ย่อ/ขยายการ์ด "แพคเกจที่เลือก" (กันบังแชต)
  proposalResult = signal<string>('');
  suggestions = signal<string[]>([]);   // ปุ่มถามด่วน (ADR 0008)

  // intake gate (ADR 0019) — ไม่ null = กำลังถามฟิลด์ที่ขาด, LLM ยังไม่ถูกเรียก
  intake = signal<IntakeKnown | null>(null);
  private intakeProvinces: FacetItem[] = [];   // cache ไว้ map label → value

  // streaming (ADR 0009) — คำตอบไหลเข้ามาเป็น token
  streamingActive = signal(false);      // กำลังสตรีมคำตอบ → โชว์กล่อง live
  streamingText = signal('');           // เนื้อหาที่ไหลมาแล้ว (signal → re-render ทุก delta แม้ zoneless)
  statusText = signal('');              // สถานะระหว่าง tool loop ("กำลังค้นแผน…") ก่อน delta แรก

  /** ข้อความที่กำลังสตรีม แตกเป็นกล่องตาม [BUBBLE] (ADR 0006) — กล่องท้ายสุด = กล่องที่ยังไหลอยู่ */
  streamingBubbles = computed(() => Chatbot.splitBubbles(this.streamingText()));

  // auto-scroll: เกาะล่างเมื่อมีข้อความ/สตรีมใหม่ — เว้นถ้าผู้ใช้เลื่อนขึ้นอ่านประวัติเอง
  @ViewChild('chatBody') private chatBodyRef?: ElementRef<HTMLElement>;
  private stick = true;

  // round-trip ทั้งก้อน (backend เป็นเจ้าของ state machine)
  conversationContext: any = null;

  // ติดตามว่าการ์ดควร "กางเอง" เมื่อใด — กางเฉพาะตอนเสนอแพคเกจ "รอบใหม่"
  // (เพิ่งเข้า ready หรือแผนที่เลือกเปลี่ยน) ไม่กางซ้ำตอนถาม-ตอบทั่วไปขณะ ready อยู่แล้ว
  private prevReady = false;
  private prevProposalSig = '';

  private readonly apiUrl = `${environment.apiUrl}/Chatbot`;
  private lastSentMessage = '';

  // modal "ดูรายละเอียด" — ความคุ้มครองเต็มของแผน (ดึงสดจาก /Motor/PackageDetail)
  detailOpen = signal(false);
  detail = signal<MotorPackageDetail | null>(null);
  detailLoading = signal(false);
  detailError = signal('');

  constructor(
    private http: HttpClient,
    protected auth: Auth,
    private router: Router,
    private handoff: QuoteHandoff,
    private docIntake: DocIntakeHandoff,
    private motor: MotorFacet
  ) {}

  /** shortlist ปัจจุบัน (filter_packages เก็บไว้ใน context.proposals) — ใช้สร้างปุ่ม "ดูรายละเอียด" ต่อแผน */
  get shortlist(): any[] { return this.conversationContext?.proposals ?? []; }

  openDetail(packageId: number): void {
    if (!packageId) return;
    this.detailOpen.set(true);
    this.detail.set(null);
    this.detailError.set('');
    this.detailLoading.set(true);
    this.motor.packageDetail(packageId).subscribe({
      next: (d) => { this.detail.set(d); this.detailLoading.set(false); },
      error: () => { this.detailError.set('โหลดรายละเอียดไม่สำเร็จ ลองใหม่อีกครั้งค่ะ'); this.detailLoading.set(false); }
    });
  }
  closeDetail(): void { this.detailOpen.set(false); this.detail.set(null); this.detailError.set(''); }

  onImgError(ev: Event): void { (ev.target as HTMLElement).style.display = 'none'; }

  // CarUsage ใน DB เป็นรหัส (เช่น "110") — แปลงเป็นชื่อไทยที่พบบ่อย, ไม่รู้จัก = ซ่อนแถว
  private readonly carUsageMap: Record<string, string> = {
    '110': 'รถยนต์นั่งส่วนบุคคลไม่เกิน 7 คน',
    '120': 'รถยนต์นั่งส่วนบุคคลเกิน 7 คน',
    '210': 'รถยนต์นั่งรับจ้าง / ให้เช่า',
    '220': 'รถยนต์โดยสาร',
    '320': 'รถยนต์บรรทุกส่วนบุคคล'
  };
  carUsageText(code: string): string { return this.carUsageMap[(code || '').trim()] || ''; }

  ngOnInit(): void {
    const doc = this.docIntake.consume();
    if (doc) { this.startFromDocument(doc); return; }

    const sel = this.handoff.consume();
    if (sel) { this.seedFromQuote(sel); return; }

    this.messages.push({
      timestamp: new Date().toISOString(),
      role: 'assistant',
      text: 'สวัสดีค่ะ 😊 พิมพ์เป็น "ผู้ช่วยหาประกันรถ" ของคุณเองค่ะ — อยากให้ช่วยหาแผนที่ใช่ เริ่มจากกรอกข้อมูลรถที่หน้า **คำนวณเบี้ย 🚗** ได้เลยนะคะ'
    });
    this.scrollChatToBottom(true);
  }

  /** มาจากหน้า quote (5 ฟิลด์) → seed candidate set + เกริ่นเริ่มสโคป (ADR 0005) */
  private seedFromQuote(d: any): void {
    this.seedContext(d.brand, d.model, d.year, d.candidates ?? [], d.province, d.provinceLabel);
  }

  /** seed context + greeting + ปุ่มถามด่วนชุดแรก (ใช้ทั้ง quote handoff, document intake และ intake gate) */
  private seedContext(
    brand: string, model: string, year: number, cands: any[],
    province?: string | null, provinceLabel?: string | null
  ): void {
    const n = cands.length;
    const insurers = new Set(cands.map((p: any) => p.insurerName)).size;
    const greeting =
      `เยี่ยมเลยค่ะ 😊 ดูให้รถ **${brand} ${model} ปี ${year}** แล้วนะคะ — ` +
      `เจอ **${n} แผน** จาก **${insurers} บริษัท** ที่เลือกได้เลยค่ะ\n\n` +
      `เดี๋ยวพิมพ์ช่วยกรองให้เหลือแผนที่ใช่ บอกงบเบี้ย/ทุนประกันที่ต้องการ หรือกดปุ่มด้านล่างได้เลยค่ะ`;

    this.conversationContext = {
      turns: [{ timestamp: new Date().toISOString(), role: 'assistant', text: greeting }],
      currentStep: 'llm',
      productType: 'motor',
      carBrand: brand, carModel: model, carYear: year,
      province: province ?? null, provinceLabel: provinceLabel ?? null,
      discoveredNeeds: [],
      proposals: [],
      candidates: cands
    };
    this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: greeting });
    this.scrollChatToBottom(true);
    this.isReadyToPropose.set(false);
    this.prevReady = false; this.prevProposalSig = '';
    this.fetchOpeningSuggestions();
  }

  /** มาจากหน้า quote พร้อมผลสกัดเอกสาร → โชว์ตาราง แล้วเข้า gate หรือ seed เลย */
  private startFromDocument(d: DocIntakeData): void {
    this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: 'อ่านเอกสารให้แล้วค่ะ 😊 นี่คือข้อมูลที่อ่านได้' });
    if (d.table) this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: d.table });
    this.scrollChatToBottom(true);
    this.startIntakeOrSeed(d.resolved, d.province, d.provinceLabel);
  }

  /** ครบ → ค้นแผนแล้ว seed; ไม่ครบ → เปิด gate ถามทีละฟิลด์ */
  private startIntakeOrSeed(rv: ResolvedVehicle, province?: string, provinceLabel?: string): void {
    const known: IntakeKnown = {
      brand: rv.brand, model: rv.model, year: rv.year,
      province: province || null, provinceLabel: provinceLabel || null
    };
    if (nextField(known) === null) { this.searchAndSeed(known); return; }
    this.intake.set(known);
    this.askNext();
  }

  /** ถามฟิลด์ถัดไป + โหลด chip ตัวเลือกจริงจาก facet */
  private askNext(): void {
    const known = this.intake();
    if (!known) return;
    const field = nextField(known);
    if (!field) { this.searchAndSeed(known); return; }

    this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: questionFor(field, known) });
    this.scrollChatToBottom(true);
    this.loadChips(field, known);
  }

  /** chip = ค่า canonical จริงจาก DB (ยี่ห้อ/จังหวัด โชว์เฉพาะยอดนิยม 8) */
  private loadChips(field: IntakeField, known: IntakeKnown): void {
    this.suggestions.set([]);
    if (field === 'brand') {
      this.motor.brands().subscribe({
        next: (b) => this.suggestions.set(topBrands(b, 8).map((x) => x.value)),
        error: () => this.suggestions.set([])
      });
    } else if (field === 'model') {
      this.motor.models(known.brand!).subscribe({
        next: (m) => this.suggestions.set(m.map((x) => x.value)),
        error: () => this.suggestions.set([])
      });
    } else if (field === 'year') {
      this.motor.years(known.brand!, known.model!, '').subscribe({
        next: (y) => this.suggestions.set(y.map((x) => x.value)),
        error: () => this.suggestions.set([])
      });
    } else {
      this.motor.provinces().subscribe({
        next: (p) => {
          this.intakeProvinces = p;
          this.suggestions.set(topProvinces(p, known.province ?? '', 8).map((x) => x.label));
        },
        error: () => this.suggestions.set([])
      });
    }
  }

  /** รับคำตอบของ gate (จาก chip หรือพิมพ์เอง) */
  private answerIntake(text: string): void {
    const known = this.intake();
    if (!known) return;
    const field = nextField(known);
    // ครบทุกฟิลด์แล้วแต่ยังอยู่ใน gate = ค้นแผนรอบก่อนล้มเหลว → ลองค้นใหม่
    if (!field) { this.searchAndSeed(known); return; }

    this.suggestions.set([]);

    if (field === 'province') {
      // chip อาจยังโหลดไม่เสร็จ (ผู้ใช้พิมพ์เร็ว) → โหลดก่อนค่อยแมตช์ กัน "ไม่พบจังหวัด" หลอก
      if (this.intakeProvinces.length === 0) {
        this.loading.set(true);
        this.statusText.set('กำลังโหลดรายชื่อจังหวัด');
        this.motor.provinces().subscribe({
          next: (p) => {
            this.loading.set(false); this.statusText.set('');
            this.intakeProvinces = p;
            this.matchProvince(text, known, field);
          },
          error: () => {
            this.loading.set(false); this.statusText.set('');
            this.retryIntake('โหลดรายชื่อจังหวัดไม่สำเร็จค่ะ ลองใหม่อีกครั้งนะคะ', field, known);
          }
        });
        return;
      }
      this.matchProvince(text, known, field);
      return;
    }

    // brand / model / year → ให้ backend resolve เป็นค่า canonical (พิมพ์เอง = fuzzy, chip = ตรงอยู่แล้ว)
    const probeBrand = field === 'brand' ? text : (known.brand ?? '');
    const probeModel = field === 'model' ? text : (known.model ?? undefined);
    const probeYear = field === 'year' ? (Number(text.replace(/\D/g, '')) || undefined) : (known.year ?? undefined);

    this.loading.set(true);
    this.statusText.set('กำลังตรวจข้อมูลรถ');
    this.motor.resolveVehicle(probeBrand, probeModel, probeYear).subscribe({
      next: (r) => {
        this.loading.set(false); this.statusText.set('');
        const ok = field === 'brand' ? r.brandOk : field === 'model' ? r.modelOk : r.yearOk;
        if (!ok) { this.retryIntake(`ไม่พบ "${text}" ในระบบค่ะ ลองกดเลือกจากปุ่มด้านล่างนะคะ`, field, known); return; }
        this.intake.set({
          ...known,
          brand: r.brand ?? known.brand,
          model: r.model ?? known.model,
          year: r.year ?? known.year
        });
        this.askNext();
      },
      error: () => {
        this.loading.set(false); this.statusText.set('');
        this.retryIntake('ตรวจข้อมูลไม่สำเร็จค่ะ ลองกดเลือกจากปุ่มด้านล่างนะคะ', field, known);
      }
    });
  }

  /** แมตช์จังหวัดจาก label/value; ยอมรับพิมพ์บางส่วน ("กรุงเทพ" → "กรุงเทพมหานคร") */
  private matchProvince(text: string, known: IntakeKnown, field: IntakeField): void {
    const t = text.trim();
    const p = this.intakeProvinces.find((x) => x.label === t || x.value === t)
           ?? (t.length >= 3 ? this.intakeProvinces.find((x) => x.label.includes(t)) : undefined);
    if (!p) { this.retryIntake(`ไม่พบจังหวัด "${text}" ค่ะ ลองกดเลือกจากปุ่มด้านล่างนะคะ`, field, known); return; }
    this.intake.set({ ...known, province: p.value, provinceLabel: p.label });
    this.askNext();
  }

  /** ตอบไม่ผ่าน → บอกเหตุผลแล้วโชว์ chip เดิมซ้ำ */
  private retryIntake(msg: string, field: IntakeField, known: IntakeKnown): void {
    this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: '⚠️ ' + msg });
    this.scrollChatToBottom();
    this.loadChips(field, known);
  }

  /** ครบแล้ว → ค้นแผนจริง; เจอ → ปิด gate ให้ LLM รับช่วง, ไม่เจอ → ถอยกลับไปถามรุ่น */
  private searchAndSeed(known: IntakeKnown): void {
    this.loading.set(true);
    this.statusText.set('กำลังค้นแผนประกัน');
    this.suggestions.set([]);
    this.motor.search({ brand: known.brand!, model: known.model!, cc: '', year: known.year!, sort: 'prem' }).subscribe({
      next: (rows) => {
        this.loading.set(false); this.statusText.set('');
        if (!rows || rows.length === 0) {
          this.messages.push({
            timestamp: new Date().toISOString(), role: 'assistant',
            text: `ไม่พบแผนประกันสำหรับ **${known.brand} ${known.model} ปี ${known.year}** ค่ะ ลองเลือกรุ่นหรือปีใหม่นะคะ`
          });
          this.intake.set({ ...known, model: null, year: null });   // ถอยกลับไปถามรุ่น
          this.askNext();
          return;
        }
        this.intake.set(null);   // ปิด gate → LLM รับช่วง (มี candidates แล้ว)
        this.seedContext(known.brand!, known.model!, known.year!, rows, known.province, known.provinceLabel);
      },
      error: () => {
        this.loading.set(false); this.statusText.set('');
        this.messages.push({
          timestamp: new Date().toISOString(), role: 'assistant',
          text: '⚠️ ค้นแผนไม่สำเร็จค่ะ กดปุ่มด้านล่างเพื่อลองใหม่ได้เลยนะคะ'
        });
        this.suggestions.set(['ลองค้นอีกครั้ง']);   // กด/พิมพ์อะไรก็ได้ → answerIntake จะเรียก searchAndSeed ซ้ำ
        this.scrollChatToBottom();
      }
    });
  }

  /** ดึงปุ่มถามด่วนชุดแรกจาก LLM หลัง greeting (template) แสดงแล้ว — ไม่บล็อก first paint (ADR 0008) */
  private fetchOpeningSuggestions(): void {
    this.http
      .post<ChatbotSuggestResponse>(`${this.apiUrl}/Suggest`, {
        conversationContext: this.conversationContext
      })
      .subscribe({
        next: (res) => this.suggestions.set(res.suggestions ?? []),
        error: () => this.suggestions.set(['สนใจประกันชั้น 1', 'สนใจประกัน 2+', 'ดูตารางความคุ้มครอง', 'สอบถามเรื่องเบี้ย'])
      });
  }

  sendMessage(): void {
    const text = this.userInput.trim();
    if (!text || this.loading()) return;
    this.lastSentMessage = text;

    // gate เปิดอยู่ → คำตอบเข้า gate ไม่ยิง /MessageStream (ADR 0019)
    if (this.intake()) {
      this.userInput = '';
      this.messages.push({ timestamp: new Date().toISOString(), role: 'user', text });
      this.scrollChatToBottom(true);
      this.answerIntake(text);
      return;
    }

    this.messages.push({ timestamp: new Date().toISOString(), role: 'user', text });
    this.userInput = '';
    this.stick = true;
    this.scrollChatToBottom(true);   // เพิ่งส่ง → เลื่อนลงล่างเสมอ
    this.loading.set(true);
    this.suggestions.set([]);   // ซ่อนปุ่มเก่าระหว่างรอคำตอบใหม่ (ADR 0008)
    this.proposalCollapsed.set(true);   // ผู้ใช้พิมพ์ถามต่อ → ย่อการ์ดทันที กันบังบทสนทนา
    this.statusText.set('กำลังอ่านคำถาม');   // สถานะแรก (ก่อน event แรก) — ให้เห็นว่าทำงานอยู่ทันที
    this.streamingText.set('');
    this.streamingActive.set(true);

    void this.streamReply(text);
  }

  /** แนบเอกสารรถในแชต → โชว์ตารางสกัด + (ถ้า resolve ครบ) ค้น candidates แล้วเข้า scoping (ADR 0018) */
  onChatDocSelected(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';   // reset ให้เลือกไฟล์เดิมซ้ำได้
    if (!file || this.loading()) return;

    const err = validateDocFile(file);
    if (err) {
      this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: '⚠️ ' + err });
      return;
    }

    this.messages.push({ timestamp: new Date().toISOString(), role: 'user', text: '📎 แนบเอกสารรถ: ' + file.name });
    this.loading.set(true);
    this.suggestions.set([]);
    this.statusText.set('กำลังอ่านเอกสาร');
    this.scrollChatToBottom(true);

    downscaleImage(file).then((blob) => {
      this.motor.extractDocument(blob, file.name).subscribe({
        next: (r) => { this.loading.set(false); this.statusText.set(''); this.applyDocToChat(r); },
        error: (e) => {
          this.loading.set(false); this.statusText.set('');
          this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: '⚠️ ' + (e?.error?.message ?? 'อ่านเอกสารไม่สำเร็จ') });
          this.scrollChatToBottom();
        }
      });
    });
  }

  /** โชว์ตารางที่สกัดได้ แล้วเข้า gate/seed แบบเดียวกับที่มาจากหน้า quote */
  private applyDocToChat(r: DocumentExtractResponse): void {
    if (r.table) this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: r.table });
    this.scrollChatToBottom();
    this.startIntakeOrSeed(r.resolved);
  }

  /** สตรีมคำตอบจาก /MessageStream (NDJSON) — render กล่องเดียวโตขึ้นแบบ AI chat. ดู ADR 0009 */
  private async streamReply(text: string): Promise<void> {
    let gotDelta = false;
    try {
      const resp = await fetch(`${this.apiUrl}/MessageStream`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(this.auth.token ? { Authorization: `Bearer ${this.auth.token}` } : {})
        },
        body: JSON.stringify({ message: text, conversationContext: this.conversationContext })
      });
      if (!resp.ok || !resp.body) throw new Error(`stream ${resp.status}`);

      const reader = resp.body.getReader();
      const decoder = new TextDecoder();
      let buf = '';
      while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        buf += decoder.decode(value, { stream: true });
        let nl: number;
        while ((nl = buf.indexOf('\n')) >= 0) {
          const line = buf.slice(0, nl).trim();
          buf = buf.slice(nl + 1);
          if (!line) continue;
          const ev = JSON.parse(line);
          if (ev.type === 'delta') gotDelta = true;
          this.handleStreamEvent(ev);
        }
      }
    } catch {
      // error ก่อน delta แรก → จำลองการตอบแบบ Client-Side Smart Fallback
      if (gotDelta && this.streamingText()) {
        this.commitBubble(this.streamingText() + '\n\n⚠️ การเชื่อมต่อขัดข้อง คำตอบอาจไม่ครบ');
        this.endStreaming();
      } else {
        this.simulateClientReply(text);
      }
    }
  }

  private handleStreamEvent(ev: any): void {
    switch (ev.type) {
      case 'status':
        this.statusText.set(ev.text ?? '');
        break;
      case 'delta':
        this.statusText.set('');           // มี prose แล้ว → เลิกโชว์สถานะ
        this.streamingText.set(this.streamingText() + (ev.text ?? ''));
        this.scrollChatToBottom();         // เลื่อนตามทุก token (ถ้ายังเกาะล่าง)
        break;
      case 'suggestions':
        this.suggestions.set(ev.items ?? []);   // ซ่อนอยู่จนกว่า loading=false (หลัง done)
        break;
      case 'done':
        this.conversationContext = ev.context;
        this.isReadyToPropose.set(!!ev.isReadyToPropose);
        // กางการ์ดเองเฉพาะเมื่อ "เสนอแพคเกจรอบใหม่" — เพิ่งเข้า ready หรือแผนเปลี่ยน
        {
          const sig = this.proposalSig(ev.context);
          if (ev.isReadyToPropose && (!this.prevReady || sig !== this.prevProposalSig))
            this.proposalCollapsed.set(false);
          this.prevReady = !!ev.isReadyToPropose;
          this.prevProposalSig = sig;
        }
        this.commitBubble(this.streamingText());
        this.endStreaming();
        break;
      case 'error':
        if (!this.streamingText()) {
          this.simulateClientReply(this.lastSentMessage || 'สวัสดี');
        } else {
          this.commitBubble(this.streamingText() + '\n\n⚠️ ' + (ev.text ?? 'เกิดข้อผิดพลาด'));
          this.endStreaming();
        }
        break;
    }
  }

  /**
   * จำลองการตอบของที่ปรึกษา AI ฝั่ง Client-Side พร้อม Typing Animation
   * สำหรับ GitHub Pages / โหมดออฟไลน์ หรือเมื่อเซิร์ฟเวอร์ภายนอกถูกบล็อก
   */
  private simulateClientReply(userText: string): void {
    const brand = this.conversationContext?.brand;
    const model = this.conversationContext?.model;
    const year = this.conversationContext?.year;
    const result = getSmartInsuranceReply(userText, brand, model, year);

    this.statusText.set('กำลังพิมพ์คำตอบ…');
    this.streamingActive.set(true);
    this.streamingText.set('');

    const fullText = result.reply;
    let idx = 0;
    const chunkSize = 4;
    const timer = setInterval(() => {
      idx += chunkSize;
      if (idx >= fullText.length) {
        clearInterval(timer);
        this.statusText.set('');
        this.streamingText.set(fullText);
        this.commitBubble(fullText);
        this.suggestions.set(result.suggestions);
        this.endStreaming();
      } else {
        this.streamingText.set(fullText.slice(0, idx));
        this.scrollChatToBottom();
      }
    }, 25);
  }

  /** แตกคำตอบตาม sentinel [BUBBLE] (บรรทัดเดี่ยว) → กล่องละชิ้น, ตัดชิ้นว่างทิ้ง (ADR 0006).
   *  ตัว sentinel ถูกกลืนหายไปกับการ split — ไม่มีทางโผล่ให้ผู้ใช้เห็น */
  private static splitBubbles(text: string): string[] {
    return (text ?? '')
      .split(/^[ \t]*\[BUBBLE\][ \t]*$/m)
      .map((s) => s.trim())
      .filter((s) => s.length > 0);
  }

  /** ย้ายข้อความที่สตรีมจบ → กลายเป็น turn ถาวรใน messages[] (สลับไร้รอยต่อ — split เดียวกับตอนสตรีม) */
  private commitBubble(text: string): void {
    for (const part of Chatbot.splitBubbles(text))
      this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: part });
  }

  private endStreaming(): void {
    this.streamingActive.set(false);
    this.streamingText.set('');
    this.statusText.set('');
    this.loading.set(false);
    this.scrollChatToBottom();   // คำตอบจบ (commit แล้ว) → เลื่อนตามถ้ายังเกาะล่าง
  }

  /** กดปุ่มถามด่วน = พิมพ์วลีนั้นแล้วส่งทันที (chip = สตริงเดียว โชว์=ส่ง — ADR 0008) */
  sendSuggestion(text: string): void {
    if (this.loading()) return;
    this.userInput = text;
    this.sendMessage();
  }

  submitProposal(): void {
    if (this.loading()) return;
    this.loading.set(true);
    this.proposalResult.set('');

    this.http
      .post<ChatbotProposalResponse>(`${this.apiUrl}/Propose`, {
        conversationContext: this.conversationContext
      })
      .subscribe({
        next: (res) => {
          this.loading.set(false);
          if (res.success) {
            this.proposalResult.set(`✅ ${res.message}`);
            this.isReadyToPropose.set(false);
          } else {
            this.proposalResult.set(`❌ ${res.message}`);
          }
        },
        error: (err) => {
          this.loading.set(false);
          this.proposalResult.set(`❌ ${err?.error?.message ?? 'ส่งคำสั่งซื้อไม่สำเร็จ'}`);
        }
      });
  }

  onKeyPress(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  /** ผู้ใช้เลื่อนเอง → อัปเดตว่าควร "เกาะล่าง" ต่อไหม (ใกล้ล่าง = เกาะ) */
  onChatScroll(): void {
    const el = this.chatBodyRef?.nativeElement;
    if (el) this.stick = el.scrollHeight - el.scrollTop - el.clientHeight < 140;
  }

  /** เลื่อนแชตลงล่างสุด — force=true เลื่อนเสมอ (เช่นพอเพิ่งส่งข้อความ), ไม่งั้นเลื่อนเฉพาะตอนเกาะล่างอยู่ */
  private scrollChatToBottom(force = false): void {
    setTimeout(() => {
      const el = this.chatBodyRef?.nativeElement;
      if (el && (force || this.stick)) el.scrollTop = el.scrollHeight;
    }, 0);
  }

  /** แปลง markdown (ตาราง/ตัวหนา/ลิสต์) → HTML; Angular sanitize ให้อัตโนมัติเมื่อ bind [innerHTML] */
  renderMarkdown(text: string): string {
    return marked.parse(text ?? '', { async: false }) as string;
  }

  /** แพคเกจที่เลือก (ข้อมูลจริงจาก backend) — ใช้แสดงการ์ดยืนยัน ไม่พึ่ง prose ของ LLM */
  get selectedProposal(): any {
    const p = this.conversationContext?.proposals;
    return p && p.length ? p[0] : null;
  }

  /** ลายเซ็นแผนที่เลือก (ใช้ดูว่า "เปลี่ยนแผน" เพื่อสั่งกางการ์ดอัตโนมัติ) */
  private proposalSig(ctx: any): string {
    const p = ctx?.proposals;
    const c = p && p.length ? p[0] : null;
    return c ? `${c.insurerName}|${c.insuranceClass}|${c.total}` : '';
  }
}
