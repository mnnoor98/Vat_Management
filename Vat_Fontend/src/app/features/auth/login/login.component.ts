import { Component, inject, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

interface DemoAccount {
  username: string;
  roleTitle: string;
  roleBn: string;
  description: string;
  color: string;
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-wrapper">
      <div class="login-container">
        <!-- Brand Header -->
        <div class="login-header">
          <div class="logo-badge">
            <span class="vat-tag">VAT BD</span>
          </div>
          <h1>Enterprise VAT Management</h1>
          <p class="subtitle">গণপ্রজাতন্ত্রী বাংলাদেশ সরকার — জাতীয় রাজস্ব বোর্ড (NBR)</p>
          <span class="badge-tag">ভ্যাট আইন ও বিধিমালা অনুযায়ী পরিচালিত</span>
        </div>

        <!-- Quick Demo Account Chips -->
        <div class="demo-section">
          <p class="section-label">দ্রুত লগইনের জন্য অ্যাকাউন্ট নির্বাচন করুন:</p>
          <div class="demo-chips">
            @for (acc of demoAccounts; track acc.username) {
              <button
                type="button"
                class="demo-chip"
                [class.active]="username === acc.username"
                (click)="selectAccount(acc.username)"
              >
                <span class="chip-dot" [style.background-color]="acc.color"></span>
                <span class="chip-name">{{ acc.roleTitle }}</span>
                <span class="chip-role">({{ acc.roleBn }})</span>
              </button>
            }
          </div>
        </div>

        <!-- Login Form -->
        <form (ngSubmit)="onSubmit()" class="login-form">
          @if (errorMessage) {
            <div class="error-banner" role="alert">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <circle cx="12" cy="12" r="10"></circle>
                <line x1="12" y1="8" x2="12" y2="12"></line>
                <line x1="12" y1="16" x2="12.01" y2="16"></line>
              </svg>
              <span>{{ errorMessage }}</span>
            </div>
          }

          <div class="form-group">
            <label for="username">ব্যবহারকারীর নাম / Username</label>
            <input
              id="username"
              type="text"
              name="username"
              [(ngModel)]="username"
              placeholder="e.g. admin, maker, checker, auditor"
              [disabled]="isLoading"
              required
              autocomplete="username"
            />
          </div>

          <div class="form-group">
            <label for="password">পাসওয়ার্ড / Password</label>
            <input
              id="password"
              type="password"
              name="password"
              [(ngModel)]="password"
              placeholder="••••••••••••"
              [disabled]="isLoading"
              required
              autocomplete="current-password"
            />
          </div>

          <button type="submit" class="submit-btn" [disabled]="isLoading || !username || !password">
            @if (isLoading) {
              <span class="spinner"></span>
              <span>যাচাই করা হচ্ছে… (Signing in)</span>
            } @else {
              <span>প্রবেশ করুন / Sign In</span>
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M5 12h14M12 5l7 7-7 7"/>
              </svg>
            }
          </button>
        </form>

        <!-- Footer Info -->
        <div class="login-footer">
          <p class="credential-info">
            লগইন পাসওয়ার্ড: <code>Admin&#64;123456</code> (Imran) / <code>Maker&#64;123456</code> / <code>Checker&#64;123456</code>
          </p>
          <p class="system-meta">ACI Limited (Advanced Chemical Industries Ltd) — BIN: 000124578-0101</p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-wrapper {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: radial-gradient(circle at 50% 20%, #0f172a 0%, #020617 100%);
      padding: 24px;
      font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
      color: #f8fafc;
    }

    .login-container {
      width: 100%;
      max-width: 480px;
      background: rgba(15, 23, 42, 0.85);
      border: 1px solid rgba(51, 65, 85, 0.6);
      backdrop-filter: blur(16px);
      border-radius: 20px;
      padding: 40px;
      box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.6), 0 0 0 1px rgba(255, 255, 255, 0.05);
    }

    .login-header {
      text-align: center;
      margin-bottom: 28px;
    }

    .logo-badge {
      display: inline-flex;
      margin-bottom: 12px;
    }

    .vat-tag {
      background: linear-gradient(135deg, #10b981 0%, #059669 100%);
      color: white;
      font-weight: 700;
      font-size: 13px;
      padding: 4px 12px;
      border-radius: 999px;
      letter-spacing: 0.05em;
      box-shadow: 0 4px 12px rgba(16, 185, 129, 0.3);
    }

    .login-header h1 {
      font-size: 24px;
      font-weight: 700;
      margin: 0 0 6px 0;
      color: #f1f5f9;
      letter-spacing: -0.02em;
    }

    .subtitle {
      font-size: 13px;
      color: #94a3b8;
      margin: 0 0 10px 0;
    }

    .badge-tag {
      display: inline-block;
      font-size: 11px;
      color: #38bdf8;
      background: rgba(56, 189, 248, 0.1);
      border: 1px solid rgba(56, 189, 248, 0.2);
      padding: 2px 10px;
      border-radius: 6px;
    }

    .demo-section {
      background: rgba(30, 41, 59, 0.5);
      border: 1px solid rgba(51, 65, 85, 0.4);
      border-radius: 12px;
      padding: 14px;
      margin-bottom: 24px;
    }

    .section-label {
      font-size: 12px;
      color: #94a3b8;
      margin: 0 0 10px 0;
      font-weight: 500;
    }

    .demo-chips {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 8px;
    }

    .demo-chip {
      display: flex;
      align-items: center;
      gap: 6px;
      background: rgba(15, 23, 42, 0.7);
      border: 1px solid rgba(71, 85, 105, 0.4);
      border-radius: 8px;
      padding: 8px 10px;
      color: #cbd5e1;
      font-size: 12px;
      cursor: pointer;
      transition: all 0.2s ease;
      text-align: left;
    }

    .demo-chip:hover {
      background: rgba(51, 65, 85, 0.6);
      border-color: #64748b;
      transform: translateY(-1px);
    }

    .demo-chip.active {
      background: rgba(16, 185, 129, 0.15);
      border-color: #10b981;
      color: #34d399;
      font-weight: 600;
    }

    .chip-dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      flex-shrink: 0;
    }

    .chip-role {
      color: #64748b;
      font-size: 10px;
    }

    .login-form {
      display: flex;
      flex-direction: column;
      gap: 18px;
    }

    .error-banner {
      display: flex;
      align-items: center;
      gap: 10px;
      background: rgba(239, 68, 68, 0.1);
      border: 1px solid rgba(239, 68, 68, 0.3);
      color: #fca5a5;
      font-size: 13px;
      padding: 12px 14px;
      border-radius: 10px;
    }

    .form-group {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .form-group label {
      font-size: 13px;
      font-weight: 500;
      color: #cbd5e1;
    }

    .form-group input {
      background: rgba(30, 41, 59, 0.8);
      border: 1px solid rgba(71, 85, 105, 0.5);
      border-radius: 10px;
      padding: 12px 14px;
      font-size: 14px;
      color: #f8fafc;
      transition: border-color 0.2s;
    }

    .form-group input:focus {
      outline: none;
      border-color: #10b981;
      box-shadow: 0 0 0 2px rgba(16, 185, 129, 0.2);
    }

    .submit-btn {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      background: linear-gradient(135deg, #10b981 0%, #059669 100%);
      color: white;
      border: none;
      border-radius: 10px;
      padding: 14px;
      font-size: 15px;
      font-weight: 600;
      cursor: pointer;
      margin-top: 8px;
      transition: all 0.2s;
      box-shadow: 0 4px 14px rgba(16, 185, 129, 0.35);
    }

    .submit-btn:hover:not(:disabled) {
      background: linear-gradient(135deg, #059669 0%, #047857 100%);
      transform: translateY(-1px);
    }

    .submit-btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .spinner {
      width: 16px;
      height: 16px;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-top-color: white;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    .login-footer {
      margin-top: 28px;
      text-align: center;
      border-top: 1px solid rgba(51, 65, 85, 0.5);
      padding-top: 18px;
    }

    .credential-info {
      font-size: 12px;
      color: #94a3b8;
      margin: 0 0 6px 0;
    }

    .credential-info code {
      background: rgba(51, 65, 85, 0.5);
      padding: 2px 6px;
      border-radius: 4px;
      color: #38bdf8;
      font-family: monospace;
      font-size: 11px;
    }

    .system-meta {
      font-size: 11px;
      color: #64748b;
      margin: 0;
    }
  `]
})
export class LoginComponent implements OnInit {
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  username = 'imran';
  password = 'Admin@123456';
  isLoading = false;
  errorMessage = '';

  readonly demoAccounts: DemoAccount[] = [
    { username: 'imran', roleTitle: 'Imran (Admin)', roleBn: 'প্রশাসক - ভ্যাট প্রধান', description: 'Full Control', color: '#10b981' },
    { username: 'maker', roleTitle: 'Tanvir (Maker)', roleBn: 'প্রস্তুতকারী', description: 'Invoices & Entries', color: '#38bdf8' },
    { username: 'checker', roleTitle: 'Farhana (Checker)', roleBn: 'যাচাইকারী', description: 'Approve & File', color: '#f59e0b' },
    { username: 'auditor', roleTitle: 'Shafiqul (Auditor)', roleBn: 'নিরীক্ষক', description: 'Read & Reports', color: '#a855f7' }
  ];

  async ngOnInit(): Promise<void> {
    if (location.pathname === '/auth/callback') {
      this.isLoading = true;
      try {
        await this.router.navigateByUrl(await this.auth.completeLogin(), { replaceUrl: true });
      } catch (error) {
        this.errorMessage = error instanceof Error ? error.message : 'Sign-in failed.';
      } finally {
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    }
  }

  selectAccount(uname: string): void {
    this.username = uname;
    const pwdMap: Record<string, string> = {
      imran: 'Admin@123456',
      admin: 'Admin@123456',
      maker: 'Maker@123456',
      checker: 'Checker@123456',
      auditor: 'Auditor@123456'
    };
    this.password = pwdMap[uname] || '123456';
    this.errorMessage = '';
  }

  onSubmit(): void {
    if (!this.username || !this.password) return;
    this.isLoading = true;
    this.errorMessage = '';

    this.auth.login({ username: this.username, password: this.password }).subscribe({
      next: () => {
        const requestedUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        const returnUrl = requestedUrl?.startsWith('/app/') && !requestedUrl.includes('\\') ? requestedUrl : '/app/portal';
        void this.router.navigateByUrl(returnUrl, { replaceUrl: true });
      },
      error: (err: Error) => {
        this.errorMessage = err.message || 'Login failed. Please check credentials.';
        this.isLoading = false;
        this.cdr.markForCheck();
      }
    });
  }
}
