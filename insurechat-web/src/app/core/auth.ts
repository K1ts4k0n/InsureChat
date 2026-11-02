import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

export interface LoginResponse {
  token: string;
  role: string;
  name: string;
  email: string;
  custCode: string;
}

const TOKEN_KEY = 'insurechat.token';
const PROFILE_KEY = 'insurechat.profile';

@Injectable({ providedIn: 'root' })
export class Auth {
  private readonly _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  private readonly _profile = signal<LoginResponse | null>(this.readProfile());

  readonly isLoggedIn = computed(() => !!this._token());
  readonly profile = computed(() => this._profile());
  readonly role = computed(() => this._profile()?.role ?? '');

  constructor(private http: HttpClient) {}

  login(username: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${environment.apiUrl}/Auth/Login`, { username, password })
      .pipe(tap((res) => this.setSession(res)));
  }

  // register ถูกตัดออก (ADR 0011) — chatbot เป็น public ไม่มีบัญชีลูกค้า; admin seed เท่านั้น

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(PROFILE_KEY);
    this._token.set(null);
    this._profile.set(null);
  }

  get token(): string | null {
    return this._token();
  }

  private setSession(res: LoginResponse): void {
    localStorage.setItem(TOKEN_KEY, res.token);
    localStorage.setItem(PROFILE_KEY, JSON.stringify(res));
    this._token.set(res.token);
    this._profile.set(res);
  }

  private readProfile(): LoginResponse | null {
    const raw = localStorage.getItem(PROFILE_KEY);
    return raw ? (JSON.parse(raw) as LoginResponse) : null;
  }
}
