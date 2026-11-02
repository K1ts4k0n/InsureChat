import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Auth } from '../../core/auth';
import { ThemeToggle } from '../../core/theme-toggle';

@Component({
  selector: 'app-login',
  imports: [FormsModule, ThemeToggle],
  templateUrl: './login.html',
  styleUrl: './login.css'
})
export class Login {
  username = '';
  password = '';
  loading = signal(false);
  error = signal('');

  constructor(private auth: Auth, private router: Router) {}

  submit(): void {
    if (!this.username || !this.password) {
      this.error.set('กรุณากรอก username และ password');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.auth.login(this.username.trim(), this.password).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.role !== 'admin') {
          this.auth.logout();
          this.error.set('บัญชีนี้ไม่มีสิทธิ์เข้าหลังบ้าน');
          return;
        }
        this.router.navigate(['/admin']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.message ?? 'เข้าสู่ระบบไม่สำเร็จ');
      }
    });
  }
}
