import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { marked } from 'marked';
import { environment } from '../../../environments/environment';
import { Auth } from '../../core/auth';
import { BrandTheme } from '../../core/brand-theme';
import { ThemeToggle } from '../../core/theme-toggle';

interface PromptSection {
  key: string; title: string; hint: string;
  value: string; default: string; isCustom: boolean; sensitive: boolean;
}
interface PromptResp { sections: PromptSection[]; lockedPreview: string; }
interface LlmResp {
  enabled: boolean; baseUrl: string; model: string;
  temperature: number; maxTokens: number; timeoutSeconds: number;
  apiKeyMasked: string; keySource: string;
}
interface ThemeResp { brand: string; }
interface EmailResp {
  senderEmail: string; recipient: string;
  passwordMasked: string; passwordSource: string;
}

// ── Conversations ─────────────────────────────────────────
interface ConversationSummary {
  id: number;
  sessionKey: string;
  carBrand: string;
  carModel: string;
  carYear: number | null;
  prospectName: string;
  prospectPhone: string;
  outcomeStatus: string;
  turnCount: number;
  lastUpdatedAt: string;
}

interface ConversationTurn {
  seq: number;
  role: 'user' | 'assistant';
  text: string;
  timestamp: string;
}

interface ConversationDetail extends ConversationSummary {
  productType: string;
  insuranceClass: string;
  sumInsure: number | null;
  prospectEmail: string;
  createdAt: string;
  needs: string[];
  turns: ConversationTurn[];
}

// ── Orders ────────────────────────────────────────────────
interface OrderListItem {
  id: number;
  orderNo: string;
  custName: string;
  custPhone: string;
  brand: string;
  model: string;
  insurerName: string;
  total: number;
  status: string;
  emailStatus: string;
  createdAt: string;
}

// ── Knowledge ─────────────────────────────────────────────
interface KnowledgeItem {
  slug: string;
  title: string;
  type: string;
  isInjected: boolean;
  updatedAt: string;
}

interface KnowledgeArticle extends KnowledgeItem {
  content: string;
  tags: string[];
  aliases: string[];
}

type Tab = 'prompt' | 'llm' | 'theme' | 'email' | 'conversations' | 'orders' | 'knowledge';

@Component({
  selector: 'app-admin',
  imports: [CommonModule, FormsModule, ThemeToggle],
  templateUrl: './admin.html',
  styleUrl: './admin.css'
})
export class Admin implements OnInit {
  private readonly api = `${environment.apiUrl}/Admin`;

  tab = signal<Tab>('prompt');

  // ── system prompt (4 ช่องแก้ได้รายช่อง + locked preview, ADR 0014) ──────────
  promptSections = signal<PromptSection[]>([]);
  lockedPreview = signal('');
  showLocked = signal(false);
  promptSavingKey = signal<string | null>(null);
  promptMsg = signal<Record<string, string>>({});

  // ── LLM ───────────────────────────────────────────────────
  llmEnabled = true;
  llmBaseUrl = '';
  llmModel = '';
  llmTemp = 0.7;
  llmMaxTokens = 1024;
  llmTimeout = 60;
  newApiKey = '';
  apiKeyMasked = signal('');
  keySource = signal('none');
  llmSaving = signal(false);
  llmMsg = signal('');

  // ── theme ─────────────────────────────────────────────────
  brand = '#2563eb';
  readonly presets = ['#2563eb', '#0ea5e9', '#16a34a', '#7c3aed', '#db2777', '#ea580c'];
  themeSaving = signal(false);
  themeMsg = signal('');

  // ── email (การส่งคำสั่งซื้อ) ───────────────────────────────
  emailSender = '';
  emailRecipient = '';
  newEmailPassword = '';
  emailPasswordMasked = signal('');
  emailPasswordSource = signal('none');
  emailSaving = signal(false);
  emailTesting = signal(false);
  emailMsg = signal('');
  emailMsgErr = signal(false);

  // ── conversations ─────────────────────────────────────────
  conversations = signal<ConversationSummary[]>([]);
  convoLoading = signal(false);
  convoLoaded = signal(false);
  convoError = signal('');
  convoSearch = '';
  selectedConvo = signal<ConversationDetail | null>(null);
  convoDetailLoading = signal(false);

  // ── orders ────────────────────────────────────────────────
  orders = signal<OrderListItem[]>([]);
  ordersLoading = signal(false);
  ordersLoaded = signal(false);
  ordersError = signal('');

  // ── knowledge ─────────────────────────────────────────────
  knowledgeItems = signal<KnowledgeItem[]>([]);
  knowledgeLoading = signal(false);
  knowledgeLoaded = signal(false);
  knowledgeError = signal('');
  selectedArticle = signal<KnowledgeArticle | null>(null);
  articleLoading = signal(false);
  syncMsg = signal('');
  syncMsgErr = signal(false);
  syncing = signal(false);

  constructor(
    private http: HttpClient,
    private auth: Auth,
    private router: Router,
    private brandTheme: BrandTheme
  ) {}

  ngOnInit(): void {
    this.loadPrompt();
    this.loadLlm();
    this.loadTheme();
    this.loadEmail();
  }

  get adminName(): string { return this.auth.profile()?.name ?? 'admin'; }

  // ── system prompt (4 ช่อง, ADR 0014) ──────────────────────
  loadPrompt(): void {
    this.http.get<PromptResp>(`${this.api}/Prompt`).subscribe((r) => {
      this.promptSections.set(r.sections);
      this.lockedPreview.set(r.lockedPreview);
    });
  }

  msgFor(key: string): string { return this.promptMsg()[key] ?? ''; }
  private setMsg(key: string, msg: string): void {
    this.promptMsg.update((m) => ({ ...m, [key]: msg }));
  }

  // อัปเดตเฉพาะช่องที่เพิ่งบันทึก/รีเซ็ต — กันทับ textarea ช่องอื่นที่ admin แก้ค้างไว้
  private applySection(r: PromptResp, key: string): void {
    const incoming = r.sections.find((s) => s.key === key);
    if (!incoming) return;
    this.promptSections.update((list) => list.map((s) => (s.key === key ? { ...incoming } : s)));
    this.lockedPreview.set(r.lockedPreview);
  }

  savePromptSection(sec: PromptSection): void {
    if (sec.sensitive && !confirm(
      'ช่องนี้เป็นกฎ compliance — แก้แล้วกระทบจรรยาบรรณ คปภ. ' +
      '(ถ้าลบกฎ "ห้ามแต่งเบี้ย" บอทอาจสร้างเลขเบี้ยขึ้นเอง). ยืนยันบันทึก?')) return;
    this.promptSavingKey.set(sec.key);
    this.setMsg(sec.key, '');
    this.http.put<PromptResp>(`${this.api}/Prompt`, { key: sec.key, value: sec.value }).subscribe({
      next: (r) => {
        this.applySection(r, sec.key);
        this.promptSavingKey.set(null);
        this.setMsg(sec.key, 'บันทึกแล้ว — มีผลในแชตรอบถัดไป');
      },
      error: (e) => { this.promptSavingKey.set(null); this.setMsg(sec.key, this.err(e)); }
    });
  }

  resetPromptSection(sec: PromptSection): void {
    if (!confirm(`รีเซ็ตช่อง "${sec.title}" กลับเป็นค่าตั้งต้น?`)) return;
    this.http.put<PromptResp>(`${this.api}/Prompt`, { key: sec.key, value: null }).subscribe((r) => {
      this.applySection(r, sec.key);
      this.setMsg(sec.key, 'รีเซ็ตเป็นค่าตั้งต้นแล้ว');
    });
  }

  // ── LLM ───────────────────────────────────────────────────
  loadLlm(): void {
    this.http.get<LlmResp>(`${this.api}/Llm`).subscribe((r) => this.fillLlm(r));
  }

  private fillLlm(r: LlmResp): void {
    this.llmEnabled = r.enabled;
    this.llmBaseUrl = r.baseUrl;
    this.llmModel = r.model;
    this.llmTemp = r.temperature;
    this.llmMaxTokens = r.maxTokens;
    this.llmTimeout = r.timeoutSeconds;
    this.apiKeyMasked.set(r.apiKeyMasked);
    this.keySource.set(r.keySource);
    this.newApiKey = '';
  }

  saveLlm(): void {
    this.llmSaving.set(true);
    this.llmMsg.set('');
    const body: Record<string, unknown> = {
      enabled: this.llmEnabled,
      baseUrl: this.llmBaseUrl,
      model: this.llmModel,
      temperature: this.llmTemp,
      maxTokens: this.llmMaxTokens,
      timeoutSeconds: this.llmTimeout
    };
    if (this.newApiKey.trim()) body['apiKey'] = this.newApiKey.trim();
    this.http.put<LlmResp>(`${this.api}/Llm`, body).subscribe({
      next: (r) => { this.fillLlm(r); this.llmSaving.set(false); this.llmMsg.set('บันทึกแล้ว — มีผลในแชตรอบถัดไป'); },
      error: (e) => { this.llmSaving.set(false); this.llmMsg.set(this.err(e)); }
    });
  }

  clearKey(): void {
    if (!confirm('ล้าง API key ที่ตั้งไว้? (จะกลับไปใช้ค่าจาก environment ถ้ามี)')) return;
    this.http.put<LlmResp>(`${this.api}/Llm`, { clearApiKey: true }).subscribe((r) => {
      this.fillLlm(r);
      this.llmMsg.set('ล้าง key แล้ว');
    });
  }

  keySourceLabel(): string {
    switch (this.keySource()) {
      case 'admin': return 'ตั้งผ่านหน้านี้';
      case 'env': return 'จาก environment (OPENROUTER_API_KEY)';
      default: return 'ยังไม่ได้ตั้ง';
    }
  }

  // ── theme ─────────────────────────────────────────────────
  loadTheme(): void {
    this.http.get<ThemeResp>(`${this.api}/Theme`).subscribe((r) => { this.brand = r.brand; this.brandTheme.apply(r.brand); });
  }

  pickPreset(hex: string): void { this.brand = hex; this.brandTheme.apply(hex); }
  onBrandInput(): void { if (/^#[0-9a-fA-F]{6}$/.test(this.brand)) this.brandTheme.apply(this.brand); }

  saveTheme(): void {
    this.themeSaving.set(true);
    this.themeMsg.set('');
    this.http.put<ThemeResp>(`${this.api}/Theme`, { brand: this.brand }).subscribe({
      next: (r) => { this.brand = r.brand; this.brandTheme.apply(r.brand); this.themeSaving.set(false); this.themeMsg.set('บันทึกสีแบรนด์แล้ว'); },
      error: (e) => { this.themeSaving.set(false); this.themeMsg.set(this.err(e)); }
    });
  }

  // ── email (การส่งคำสั่งซื้อ) ───────────────────────────────
  loadEmail(): void {
    this.http.get<EmailResp>(`${this.api}/Email`).subscribe((r) => this.fillEmail(r));
  }

  private fillEmail(r: EmailResp): void {
    this.emailSender = r.senderEmail;
    this.emailRecipient = r.recipient;
    this.emailPasswordMasked.set(r.passwordMasked);
    this.emailPasswordSource.set(r.passwordSource);
    this.newEmailPassword = '';
  }

  saveEmail(): void {
    this.emailSaving.set(true);
    this.emailMsg.set('');
    const body: Record<string, unknown> = {
      senderEmail: this.emailSender,
      recipient: this.emailRecipient
    };
    if (this.newEmailPassword.trim()) body['password'] = this.newEmailPassword.trim();
    this.http.put<EmailResp>(`${this.api}/Email`, body).subscribe({
      next: (r) => { this.fillEmail(r); this.emailSaving.set(false); this.emailMsgErr.set(false); this.emailMsg.set('บันทึกแล้ว — มีผลกับคำสั่งซื้อรอบถัดไป'); },
      error: (e) => { this.emailSaving.set(false); this.emailMsgErr.set(true); this.emailMsg.set(this.err(e)); }
    });
  }

  testEmail(): void {
    this.emailTesting.set(true);
    this.emailMsg.set('');
    this.http.post<{ message: string }>(`${this.api}/Email/Test`, {}).subscribe({
      next: (r) => { this.emailTesting.set(false); this.emailMsgErr.set(false); this.emailMsg.set(r.message); },
      error: (e) => { this.emailTesting.set(false); this.emailMsgErr.set(true); this.emailMsg.set(this.err(e)); }
    });
  }

  clearEmailPassword(): void {
    if (!confirm('ล้าง App Password ที่ตั้งไว้? (จะกลับไปใช้ค่าจาก environment ถ้ามี)')) return;
    this.http.put<EmailResp>(`${this.api}/Email`, { clearPassword: true }).subscribe((r) => {
      this.fillEmail(r);
      this.emailMsgErr.set(false);
      this.emailMsg.set('ล้าง App Password แล้ว');
    });
  }

  emailPasswordSourceLabel(): string {
    switch (this.emailPasswordSource()) {
      case 'admin': return 'ตั้งผ่านหน้านี้';
      case 'env': return 'จาก environment (SMTP_PASSWORD)';
      default: return 'ยังไม่ได้ตั้ง';
    }
  }

  // ── tab switching ─────────────────────────────────────────
  setTab(t: Tab): void {
    this.tab.set(t);
    if (t === 'conversations' && !this.convoLoaded()) this.loadConversations();
    if (t === 'orders' && !this.ordersLoaded()) this.loadOrders();
    if (t === 'knowledge' && !this.knowledgeLoaded()) this.loadKnowledge();
  }

  isWideTab(): boolean {
    const t = this.tab();
    return t === 'conversations' || t === 'orders' || t === 'knowledge';
  }

  // ── conversations ─────────────────────────────────────────
  loadConversations(q?: string): void {
    this.convoLoading.set(true);
    this.convoError.set('');
    const params = q ? `?take=50&skip=0&q=${encodeURIComponent(q)}` : '?take=50&skip=0';
    this.http.get<ConversationSummary[]>(`${this.api}/Conversations${params}`).subscribe({
      next: (r) => {
        this.conversations.set(r);
        this.convoLoading.set(false);
        this.convoLoaded.set(true);
      },
      error: (e) => {
        this.convoError.set(this.err(e));
        this.convoLoading.set(false);
        this.convoLoaded.set(true);
      }
    });
  }

  searchConversations(): void {
    this.loadConversations(this.convoSearch.trim() || undefined);
  }

  openConversation(id: number): void {
    this.convoDetailLoading.set(true);
    this.selectedConvo.set(null);
    this.http.get<ConversationDetail>(`${this.api}/Conversations/${id}`).subscribe({
      next: (r) => { this.selectedConvo.set(r); this.convoDetailLoading.set(false); },
      error: () => { this.convoDetailLoading.set(false); }
    });
  }

  closeConversation(): void { this.selectedConvo.set(null); }

  // ── orders ────────────────────────────────────────────────
  loadOrders(): void {
    this.ordersLoading.set(true);
    this.ordersError.set('');
    this.http.get<OrderListItem[]>(`${this.api}/Orders?take=50&skip=0`).subscribe({
      next: (r) => {
        this.orders.set(r);
        this.ordersLoading.set(false);
        this.ordersLoaded.set(true);
      },
      error: (e) => {
        this.ordersError.set(this.err(e));
        this.ordersLoading.set(false);
        this.ordersLoaded.set(true);
      }
    });
  }

  // ── knowledge ─────────────────────────────────────────────
  loadKnowledge(): void {
    this.knowledgeLoading.set(true);
    this.knowledgeError.set('');
    this.http.get<KnowledgeItem[]>(`${this.api}/Knowledge`).subscribe({
      next: (r) => {
        this.knowledgeItems.set(r);
        this.knowledgeLoading.set(false);
        this.knowledgeLoaded.set(true);
      },
      error: (e) => {
        this.knowledgeError.set(this.err(e));
        this.knowledgeLoading.set(false);
        this.knowledgeLoaded.set(true);
      }
    });
  }

  openArticle(slug: string): void {
    this.articleLoading.set(true);
    this.selectedArticle.set(null);
    this.http.get<KnowledgeArticle>(`${this.api}/Knowledge/Article?slug=${encodeURIComponent(slug)}`).subscribe({
      next: (r) => { this.selectedArticle.set(r); this.articleLoading.set(false); },
      error: () => { this.articleLoading.set(false); }
    });
  }

  closeArticle(): void { this.selectedArticle.set(null); }

  syncKnowledge(): void {
    this.syncing.set(true);
    this.syncMsg.set('');
    this.syncMsgErr.set(false);
    this.http.post<{ synced: number }>(`${this.api}/Knowledge/Sync`, {}).subscribe({
      next: (r) => {
        this.syncing.set(false);
        this.syncMsg.set(`ซิงก์แล้ว ${r.synced} บทความ`);
        // reload list
        this.knowledgeLoaded.set(false);
        this.loadKnowledge();
      },
      error: (e) => {
        this.syncing.set(false);
        this.syncMsgErr.set(true);
        this.syncMsg.set(this.err(e));
      }
    });
  }

  // ── format helpers ────────────────────────────────────────
  renderMarkdown(text: string): string {
    return marked.parse(text ?? '', { async: false }) as string;
  }

  formatMoney(n: number): string {
    return n == null ? '—' : n.toLocaleString('th-TH');
  }

  formatDate(s: string): string {
    if (!s) return '—';
    const d = new Date(s);
    const y = d.getFullYear();
    const mo = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    const h = String(d.getHours()).padStart(2, '0');
    const m = String(d.getMinutes()).padStart(2, '0');
    return `${y}-${mo}-${day} ${h}:${m}`;
  }

  // ── misc ──────────────────────────────────────────────────
  logout(): void { this.auth.logout(); this.router.navigate(['/login']); }

  private err(e: any): string { return e?.error?.message ?? 'บันทึกไม่สำเร็จ'; }
}
