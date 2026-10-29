import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { CompanyContextService } from '../../../core/services/company-context.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="auth-container">
      <!-- Background Ambient Glow & Grid -->
      <div class="ambient-glow glow-1"></div>
      <div class="ambient-glow glow-2"></div>
      <div class="grid-overlay"></div>

      <!-- Header Bar with Language Switcher -->
      <header class="auth-top-nav">
        <div class="govt-brand">
          <div class="bd-seal-circle">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none">
              <circle cx="12" cy="12" r="10" fill="#006A4E" stroke="#F42A41" stroke-width="2"/>
              <circle cx="10" cy="12" r="4" fill="#F42A41"/>
            </svg>
          </div>
          <div>
            <span class="govt-name">গণপ্রজাতন্ত্রী বাংলাদেশ সরকার</span>
            <span class="nbr-sub">জাতীয় রাজস্ব বোর্ড (National Board of Revenue)</span>
          </div>
        </div>

        <button class="lang-pill-btn" (click)="context.toggleLocale()">
          <span>{{ context.locale() === 'bn' ? 'English' : 'বাংলা' }}</span>
        </button>
      </header>

      <!-- Main Login Card Wrapper -->
      <main class="auth-content">
        <div class="login-card animate-fade-in">
          
          <!-- Card Header -->
          <div class="card-header-badge">
            <div class="system-icon-box">
              <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
                <path d="m9 12 2 2 4-4"/>
              </svg>
            </div>
            <h1 class="portal-title">
              {{ context.locale() === 'bn' ? 'ভ্যাট ব্যবস্থাপনা ও অটোমেশন পোর্টাল' : 'Enterprise VAT Management Portal' }}
            </h1>
            <p class="portal-subtitle">
              {{ context.locale() === 'bn' ? 'মূসক আইন ২০১২ ও বিধিমালা ২০১৬ অনুযায়ী অনুমোদিত কর ব্যবস্থা' : 'NBR Statutory VAT & Supplementary Duty Act 2012 Compliance' }}
            </p>
          </div>

          <!-- Error Alert Banner -->
          <div *ngIf="errorMessage" class="error-banner animate-shake">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="10"></circle>
              <line x1="12" y1="8" x2="12" y2="12"></line>
              <line x1="12" y1="16" x2="12.01" y2="16"></line>
            </svg>
            <span>{{ errorMessage }}</span>
          </div>

          <!-- Login Form -->
          <form (ngSubmit)="onSubmit()" class="login-form">
            <!-- Username Input -->
            <div class="form-field">
              <label for="username" class="field-label">
                {{ context.locale() === 'bn' ? 'ব্যবহারকারী নাম বা ইমেইল (Username / Email)' : 'Username or Email' }}
              </label>
              <div class="input-wrapper">
                <span class="input-icon">
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                    <circle cx="12" cy="7" r="4"></circle>
                  </svg>
                </span>
                <input 
                  type="text" 
                  id="username" 
                  name="username" 
                  [(ngModel)]="username" 
                  required 
                  class="auth-input" 
                  [placeholder]="context.locale() === 'bn' ? 'যেমন: admin বা maker' : 'e.g. admin or maker'"
                  autocomplete="username"
                />
              </div>
            </div>

            <!-- Password Input -->
            <div class="form-field">
              <div class="label-row">
                <label for="password" class="field-label">
                  {{ context.locale() === 'bn' ? 'গোপন পাসওয়ার্ড (Password)' : 'Password' }}
                </label>
              </div>
              <div class="input-wrapper">
                <span class="input-icon">
                  <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                    <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                  </svg>
                </span>
                <input 
                  [type]="showPassword ? 'text' : 'password'" 
                  id="password" 
                  name="password" 
                  [(ngModel)]="password" 
                  required 
                  class="auth-input" 
                  [placeholder]="context.locale() === 'bn' ? 'পাসওয়ার্ড লিখুন' : 'Enter your password'"
                  autocomplete="current-password"
                />
                <button 
                  type="button" 
                  class="toggle-pwd-btn" 
                  (click)="showPassword = !showPassword"
                  [title]="showPassword ? 'Hide password' : 'Show password'"
                >
                  <svg *ngIf="!showPassword" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>
                    <circle cx="12" cy="12" r="3"></circle>
                  </svg>
                  <svg *ngIf="showPassword" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"></path>
                    <line x1="1" y1="1" x2="23" y2="23"></line>
                  </svg>
                </button>
              </div>
            </div>

            <!-- Options Row -->
            <div class="form-options">
              <label class="remember-box">
                <input type="checkbox" [(ngModel)]="rememberMe" name="rememberMe" />
                <span>{{ context.locale() === 'bn' ? 'পাসওয়ার্ড মনে রাখুন' : 'Remember me' }}</span>
              </label>
              <span class="auth-hint">{{ context.locale() === 'bn' ? 'এনবিআর এনক্রিপ্টেড' : 'NBR Verified' }}</span>
            </div>

            <!-- Submit Button -->
            <button type="submit" class="submit-btn" [disabled]="isLoading">
              <span *ngIf="!isLoading">
                {{ context.locale() === 'bn' ? 'লগইন করুন (Secure Sign In)' : 'Sign In to Portal' }}
              </span>
              <span *ngIf="isLoading" class="btn-spinner">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M21 12a9 9 0 1 1-6.219-8.56"></path>
                </svg>
                {{ context.locale() === 'bn' ? 'যাচাই করা হচ্ছে...' : 'Authenticating...' }}
              </span>
            </button>
          </form>

          <!-- Quick Demo Login Personas -->
          <div class="demo-personas-box">
            <div class="demo-header">
              <span class="demo-title font-bengali">
                {{ context.locale() === 'bn' ? 'এক ক্লিকে ডেমো লগইন নির্বাচন:' : 'Quick Persona Demo Login:' }}
              </span>
            </div>
            <div class="persona-chips">
              <button type="button" class="chip chip-admin" (click)="fillDemo('admin', 'admin123')">
                <span class="chip-role">👑 SuperAdmin</span>
                <span class="chip-user">All Branches</span>
              </button>
              <button type="button" class="chip chip-maker" (click)="fillDemo('maker', 'maker123')">
                <span class="chip-role">📝 Maker</span>
                <span class="chip-user">Dhaka Plant</span>
              </button>
              <button type="button" class="chip chip-maker" (click)="fillDemo('ctg_maker', 'maker123')">
                <span class="chip-role">⚓ Maker</span>
                <span class="chip-user">CTG Depot</span>
              </button>
              <button type="button" class="chip chip-checker" (click)="fillDemo('checker', 'checker123')">
                <span class="chip-role">✅ Checker</span>
                <span class="chip-user">Dhaka Plant</span>
              </button>
              <button type="button" class="chip chip-manager" (click)="fillDemo('taxmanager', 'tax123')">
                <span class="chip-role">⚖️ Tax Mgr</span>
                <span class="chip-user">Central HQ</span>
              </button>
            </div>
          </div>

          <!-- Register Link -->
          <div class="register-footer">
            <span>{{ context.locale() === 'bn' ? 'নতুন ব্যবহারকারী?' : "Don't have an account?" }}</span>
            <a routerLink="/register" class="register-link">
              {{ context.locale() === 'bn' ? 'নতুন অ্যাকাউন্ট নিবন্ধন করুন' : 'Create New Account' }}
            </a>
          </div>

        </div>

        <!-- Security Seal Footer -->
        <footer class="compliance-footer">
          <p>
            {{ context.locale() === 'bn' 
              ? 'নিরাপত্তা বার্তা: এই সিস্টেমটি বাংলাদেশ জাতীয় রাজস্ব বোর্ড (NBR) এর ভ্যাট ও সম্পূরক শুল্ক আইন ২০১২ অনুযায়ী অডিট ট্রেইল দ্বারা সুরক্ষিত।'
              : 'Security Notice: This portal is protected with end-to-end audit trails compliant with NBR Rule 40 & 41.' }}
          </p>
        </footer>
      </main>
    </div>
  `,
  styles: [`
    :host {
      display: block;
      min-height: 100vh;
      width: 100%;
      background: #090d16;
      font-family: var(--font-main, 'Inter', sans-serif);
      color: #f1f5f9;
    }

    .auth-container {
      position: relative;
      min-height: 100vh;
      display: flex;
      flex-direction: column;
      overflow-x: hidden;
      background: radial-gradient(circle at 50% 20%, #0d1b2a 0%, #080d1a 60%, #040810 100%);
    }

    /* Ambient Lighting */
    .ambient-glow {
      position: absolute;
      border-radius: 50%;
      filter: blur(120px);
      pointer-events: none;
      z-index: 1;
    }
    .glow-1 {
      top: 10%;
      left: 20%;
      width: 350px;
      height: 350px;
      background: rgba(5, 150, 105, 0.15); /* Emerald */
    }
    .glow-2 {
      bottom: 10%;
      right: 20%;
      width: 400px;
      height: 400px;
      background: rgba(30, 58, 138, 0.2); /* Deep Blue */
    }

    .grid-overlay {
      position: absolute;
      inset: 0;
      background-image: radial-gradient(rgba(255, 255, 255, 0.05) 1px, transparent 1px);
      background-size: 32px 32px;
      pointer-events: none;
      z-index: 1;
    }

    /* Top Nav */
    .auth-top-nav {
      position: relative;
      z-index: 10;
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 20px 36px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.06);
      background: rgba(13, 21, 38, 0.4);
      backdrop-filter: blur(10px);
    }

    .govt-brand {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .bd-seal-circle {
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .govt-name {
      display: block;
      font-size: 0.95rem;
      font-weight: 700;
      color: #10b981;
      letter-spacing: 0.3px;
    }

    .nbr-sub {
      display: block;
      font-size: 0.75rem;
      color: #94a3b8;
    }

    .lang-pill-btn {
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.15);
      color: #f1f5f9;
      padding: 6px 14px;
      border-radius: 20px;
      font-size: 0.85rem;
      cursor: pointer;
      font-weight: 600;
      transition: all 0.2s ease;
    }
    .lang-pill-btn:hover {
      background: rgba(16, 185, 129, 0.2);
      border-color: #10b981;
    }

    /* Main Content */
    .auth-content {
      position: relative;
      z-index: 10;
      flex: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 40px 20px;
    }

    /* Login Card */
    .login-card {
      width: 100%;
      max-width: 480px;
      background: rgba(15, 23, 42, 0.85);
      border: 1px solid rgba(255, 255, 255, 0.1);
      box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.7), 0 0 0 1px rgba(16, 185, 129, 0.15);
      border-radius: 18px;
      padding: 36px;
      backdrop-filter: blur(16px);
    }

    .card-header-badge {
      text-align: center;
      margin-bottom: 24px;
      display: flex;
      flex-direction: column;
      align-items: center;
    }

    .system-icon-box {
      width: 52px;
      height: 52px;
      border-radius: 14px;
      background: linear-gradient(135deg, rgba(16, 185, 129, 0.25), rgba(5, 150, 105, 0.1));
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: 14px;
    }

    .portal-title {
      font-size: 1.35rem;
      font-weight: 800;
      color: #ffffff;
      margin-bottom: 6px;
      letter-spacing: -0.3px;
    }

    .portal-subtitle {
      font-size: 0.8rem;
      color: #94a3b8;
      line-height: 1.4;
      max-width: 380px;
    }

    /* Error Banner */
    .error-banner {
      display: flex;
      align-items: center;
      gap: 10px;
      background: rgba(239, 68, 68, 0.12);
      border: 1px solid rgba(239, 68, 68, 0.35);
      color: #fca5a5;
      padding: 10px 14px;
      border-radius: 10px;
      font-size: 0.82rem;
      margin-bottom: 20px;
    }

    /* Form Elements */
    .login-form {
      display: flex;
      flex-direction: column;
      gap: 18px;
    }

    .form-field {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .field-label {
      font-size: 0.82rem;
      font-weight: 600;
      color: #cbd5e1;
    }

    .input-wrapper {
      position: relative;
      display: flex;
      align-items: center;
    }

    .input-icon {
      position: absolute;
      left: 14px;
      color: #64748b;
      display: flex;
      align-items: center;
      pointer-events: none;
    }

    .auth-input {
      width: 100%;
      background: rgba(2, 6, 23, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 10px;
      padding: 12px 14px 12px 42px;
      color: #f8fafc;
      font-size: 0.92rem;
      transition: all 0.2s ease;
      outline: none;
    }

    .auth-input:focus {
      border-color: #10b981;
      box-shadow: 0 0 0 3px rgba(16, 185, 129, 0.2);
      background: rgba(2, 6, 23, 0.9);
    }

    .toggle-pwd-btn {
      position: absolute;
      right: 12px;
      background: none;
      border: none;
      color: #64748b;
      cursor: pointer;
      display: flex;
      align-items: center;
      padding: 4px;
      border-radius: 6px;
      transition: color 0.15s;
    }
    .toggle-pwd-btn:hover { color: #cbd5e1; }

    .form-options {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.8rem;
    }

    .remember-box {
      display: flex;
      align-items: center;
      gap: 8px;
      cursor: pointer;
      color: #94a3b8;
    }
    .remember-box input { accent-color: #10b981; }

    .auth-hint {
      color: #10b981;
      font-size: 0.75rem;
      font-weight: 600;
      background: rgba(16, 185, 129, 0.1);
      padding: 2px 8px;
      border-radius: 6px;
    }

    /* Submit Button */
    .submit-btn {
      background: linear-gradient(135deg, #059669 0%, #047857 100%);
      color: #ffffff;
      border: none;
      padding: 13px;
      border-radius: 10px;
      font-size: 0.95rem;
      font-weight: 700;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 4px 14px rgba(5, 150, 105, 0.4);
      transition: all 0.2s ease;
      margin-top: 4px;
    }
    .submit-btn:hover:not(:disabled) {
      background: linear-gradient(135deg, #10b981 0%, #059669 100%);
      box-shadow: 0 6px 20px rgba(16, 185, 129, 0.5);
      transform: translateY(-1px);
    }
    .submit-btn:disabled {
      opacity: 0.7;
      cursor: not-allowed;
    }

    .btn-spinner {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .btn-spinner svg {
      animation: spin 1s linear infinite;
    }

    @keyframes spin {
      100% { transform: rotate(360deg); }
    }

    /* Demo Personas Chips */
    .demo-personas-box {
      margin-top: 24px;
      padding-top: 20px;
      border-top: 1px dashed rgba(255, 255, 255, 0.1);
    }

    .demo-header {
      margin-bottom: 10px;
      text-align: center;
    }

    .demo-title {
      font-size: 0.75rem;
      color: #64748b;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      font-weight: 700;
    }

    .persona-chips {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 8px;
    }

    .chip {
      background: rgba(255, 255, 255, 0.04);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 8px;
      padding: 7px 4px;
      display: flex;
      flex-direction: column;
      align-items: center;
      cursor: pointer;
      transition: all 0.15s ease;
    }
    .chip:hover {
      background: rgba(255, 255, 255, 0.09);
      border-color: rgba(255, 255, 255, 0.2);
      transform: translateY(-2px);
    }

    .chip-role {
      font-size: 0.72rem;
      font-weight: 700;
    }
    .chip-user {
      font-size: 0.65rem;
      color: #64748b;
      font-family: monospace;
    }

    .chip-admin .chip-role { color: #f43f5e; }
    .chip-maker .chip-role { color: #38bdf8; }
    .chip-checker .chip-role { color: #fbbf24; }
    .chip-manager .chip-role { color: #34d399; }

    /* Register Footer */
    .register-footer {
      margin-top: 20px;
      text-align: center;
      font-size: 0.85rem;
      color: #94a3b8;
      display: flex;
      justify-content: center;
      gap: 6px;
    }

    .register-link {
      color: #34d399;
      font-weight: 600;
      text-decoration: none;
      transition: color 0.15s;
    }
    .register-link:hover {
      color: #6ee7b7;
      text-decoration: underline;
    }

    /* Security Compliance Footer */
    .compliance-footer {
      margin-top: 30px;
      text-align: center;
      max-width: 600px;
    }
    .compliance-footer p {
      font-size: 0.72rem;
      color: #475569;
      line-height: 1.5;
    }

    @keyframes shake {
      0%, 100% { transform: translateX(0); }
      20%, 60% { transform: translateX(-6px); }
      40%, 80% { transform: translateX(6px); }
    }
    .animate-shake {
      animation: shake 0.4s ease-in-out;
    }
  `]
})
export class LoginComponent implements OnInit {
  public authService = inject(AuthService);
  public context = inject(CompanyContextService);
  private toast = inject(ToastService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  public username: string = '';
  public password: string = '';
  public rememberMe: boolean = true;
  public showPassword: boolean = false;
  public isLoading: boolean = false;
  public errorMessage: string = '';

  private returnUrl: string = '/app/portal';

  ngOnInit(): void {
    this.returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/app/portal';
  }

  public fillDemo(u: string, p: string): void {
    this.username = u;
    this.password = p;
    this.errorMessage = '';
    // Auto-submit on chip click for effortless user testing
    this.onSubmit();
  }

  public onSubmit(): void {
    if (!this.username || !this.password) {
      this.errorMessage = this.context.locale() === 'bn' 
        ? 'ব্যবহারকারীর নাম ও পাসওয়ার্ড প্রদান করুন।' 
        : 'Please enter both username and password.';
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';

    this.authService.login({ username: this.username, password: this.password }, this.rememberMe).subscribe({
      next: (user) => {
        this.isLoading = false;
        this.toast.showSuccess(
          this.context.locale() === 'bn'
            ? `স্বাগতম ${user.fullNameBn}! পোর্টাল সফলভাবে আনলক হয়েছে।`
            : `Welcome back, ${user.fullName}! Logged in as ${user.role}.`
        );
        this.router.navigateByUrl(this.returnUrl);
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = err.message || 'Login failed. Please check your credentials.';
      }
    });
  }
}
