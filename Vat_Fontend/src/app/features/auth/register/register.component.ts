import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService, RegisterUserRequest } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { CompanyContextService } from '../../../core/services/company-context.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="auth-container">
      <div class="ambient-glow glow-1"></div>
      <div class="ambient-glow glow-2"></div>
      <div class="grid-overlay"></div>

      <!-- Header Bar -->
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

      <!-- Main Registration Content -->
      <main class="auth-content">
        <div class="register-card animate-fade-in">
          
          <div class="card-header">
            <div class="icon-circle">
              <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"></path>
                <circle cx="9" cy="7" r="4"></circle>
                <line x1="19" y1="8" x2="19" y2="14"></line>
                <line x1="22" y1="11" x2="16" y2="11"></line>
              </svg>
            </div>
            <h1 class="page-title">
              {{ context.locale() === 'bn' ? 'নতুন ব্যবহারকারী নিবন্ধন' : 'User Registration & Access Request' }}
            </h1>
            <p class="page-subtitle">
              {{ context.locale() === 'bn' ? 'ভ্যাট পোর্টালের জন্য নতুন কর্মকর্তা বা কর্মচারীর অ্যাকাউন্ট তৈরি করুন' : 'Create a verified user profile with assigned RBAC scope and branch credentials' }}
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

          <!-- Registration Form -->
          <form (ngSubmit)="onSubmit()" class="register-form">
            <div class="form-grid">
              
              <!-- Full Name (English) -->
              <div class="form-group">
                <label class="field-label">Full Name (English) *</label>
                <input 
                  type="text" 
                  name="fullName" 
                  [(ngModel)]="formData.fullName" 
                  required 
                  class="auth-input" 
                  placeholder="e.g. Kamrul Hasan, ACA"
                />
              </div>

              <!-- Full Name (বাংলা) -->
              <div class="form-group">
                <label class="field-label">নাম (বাংলায়)</label>
                <input 
                  type="text" 
                  name="fullNameBn" 
                  [(ngModel)]="formData.fullNameBn" 
                  class="auth-input font-bengali" 
                  placeholder="যেমন: কামরুল হাসান"
                />
              </div>

              <!-- Username -->
              <div class="form-group">
                <label class="field-label">Username (ইউজারনেম) *</label>
                <input 
                  type="text" 
                  name="username" 
                  [(ngModel)]="formData.username" 
                  required 
                  class="auth-input font-mono" 
                  placeholder="e.g. kamrul.vat"
                />
              </div>

              <!-- Email -->
              <div class="form-group">
                <label class="field-label">Official Email (ইমেইল) *</label>
                <input 
                  type="email" 
                  name="email" 
                  [(ngModel)]="formData.email" 
                  required 
                  class="auth-input" 
                  placeholder="e.g. kamrul@padmagroup.com.bd"
                />
              </div>

              <!-- Mobile Number -->
              <div class="form-group">
                <label class="field-label">Mobile Number (মোবাইল নম্বর)</label>
                <input 
                  type="tel" 
                  name="phone" 
                  [(ngModel)]="formData.phone" 
                  class="auth-input" 
                  placeholder="+880 1711-XXXXXX"
                />
              </div>

              <!-- Role Selector -->
              <div class="form-group">
                <label class="field-label">Assigned Role (দায়িত্ব ও ভূমিকা) *</label>
                <select name="role" [(ngModel)]="formData.role" class="auth-select">
                  <option value="Maker">Maker (বিল ও চালান প্রস্তুতকারী)</option>
                  <option value="Checker">Checker (চালান নিরীক্ষক ও ভেরিফায়ার)</option>
                  <option value="TaxManager">Tax Manager (চূড়ান্ত অনুমোদন ও খতিয়ান পোস্টার)</option>
                  <option value="Auditor">Auditor (ভ্যাট নিরীক্ষক)</option>
                  <option value="SuperAdmin">Super Admin (সিস্টেম অ্যাডমিনিস্ট্রেটর)</option>
                </select>
              </div>

              <!-- Company Assignment -->
              <div class="form-group">
                <label class="field-label">Company (কোম্পানি) *</label>
                <select name="companyId" [(ngModel)]="formData.companyId" class="auth-select">
                  <option value="11111111-1111-1111-1111-111111111111">Dhaka Polymer Industries Ltd (BIN: 001928374-0101)</option>
                  <option value="22222222-2222-2222-2222-222222222222">Meghna Plastic Industries Ltd (BIN: 002837465-0101)</option>
                </select>
              </div>

              <!-- Branch Assignment -->
              <div class="form-group">
                <label class="field-label">Branch (শাখা / ইউনিট) *</label>
                <select name="branchId" [(ngModel)]="formData.branchId" class="auth-select">
                  <option value="33333333-3333-3333-3333-333333333331">DHK-CENTRAL (Dhaka Central Plant)</option>
                  <option value="33333333-3333-3333-3333-333333333332">CTG-DEPOT (Chittagong Port Depot)</option>
                </select>
              </div>

              <!-- Password -->
              <div class="form-group">
                <label class="field-label">Password (পাসওয়ার্ড - কমপক্ষে ৬ অক্ষর) *</label>
                <input 
                  type="password" 
                  name="password" 
                  [(ngModel)]="formData.password" 
                  required 
                  class="auth-input" 
                  placeholder="••••••••"
                />
              </div>

              <!-- Confirm Password -->
              <div class="form-group">
                <label class="field-label">Confirm Password (পুনরায় পাসওয়ার্ড) *</label>
                <input 
                  type="password" 
                  name="confirmPassword" 
                  [(ngModel)]="confirmPassword" 
                  required 
                  class="auth-input" 
                  placeholder="••••••••"
                />
              </div>

            </div>

            <!-- Submit Button -->
            <button type="submit" class="submit-btn" [disabled]="isLoading">
              <span *ngIf="!isLoading">
                {{ context.locale() === 'bn' ? 'ব্যবহারকারী তৈরি করুন (Create Account)' : 'Register & Create Account' }}
              </span>
              <span *ngIf="isLoading" class="btn-spinner">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M21 12a9 9 0 1 1-6.219-8.56"></path>
                </svg>
                নিবন্ধন প্রক্রিয়াধীন...
              </span>
            </button>
          </form>

          <!-- Back to Login Link -->
          <div class="login-footer">
            <span>{{ context.locale() === 'bn' ? 'ইতিমধ্যে অ্যাকাউন্ট আছে?' : 'Already have an account?' }}</span>
            <a routerLink="/login" class="login-link">
              {{ context.locale() === 'bn' ? 'লগইন করুন (Sign In)' : 'Sign In to Portal' }}
            </a>
          </div>

        </div>
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
      background: radial-gradient(circle at 50% 20%, #0d1b2a 0%, #080d1a 60%, #040810 100%);
    }

    .ambient-glow {
      position: absolute;
      border-radius: 50%;
      filter: blur(120px);
      pointer-events: none;
      z-index: 1;
    }
    .glow-1 { top: 10%; left: 15%; width: 380px; height: 380px; background: rgba(5, 150, 105, 0.15); }
    .glow-2 { bottom: 10%; right: 15%; width: 420px; height: 420px; background: rgba(30, 58, 138, 0.2); }

    .grid-overlay {
      position: absolute;
      inset: 0;
      background-image: radial-gradient(rgba(255, 255, 255, 0.05) 1px, transparent 1px);
      background-size: 32px 32px;
      pointer-events: none;
      z-index: 1;
    }

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

    .govt-brand { display: flex; align-items: center; gap: 12px; }
    .govt-name { display: block; font-size: 0.95rem; font-weight: 700; color: #10b981; }
    .nbr-sub { display: block; font-size: 0.75rem; color: #94a3b8; }

    .lang-pill-btn {
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.15);
      color: #f1f5f9;
      padding: 6px 14px;
      border-radius: 20px;
      font-size: 0.85rem;
      cursor: pointer;
      font-weight: 600;
      transition: all 0.2s;
    }
    .lang-pill-btn:hover { background: rgba(16, 185, 129, 0.2); border-color: #10b981; }

    .auth-content {
      position: relative;
      z-index: 10;
      flex: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 30px 20px;
    }

    .register-card {
      width: 100%;
      max-width: 680px;
      background: rgba(15, 23, 42, 0.88);
      border: 1px solid rgba(255, 255, 255, 0.1);
      box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.7), 0 0 0 1px rgba(16, 185, 129, 0.15);
      border-radius: 18px;
      padding: 36px 40px;
      backdrop-filter: blur(16px);
    }

    .card-header {
      text-align: center;
      margin-bottom: 24px;
      display: flex;
      flex-direction: column;
      align-items: center;
    }

    .icon-circle {
      width: 52px;
      height: 52px;
      border-radius: 14px;
      background: linear-gradient(135deg, rgba(16, 185, 129, 0.25), rgba(5, 150, 105, 0.1));
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: 12px;
    }

    .page-title {
      font-size: 1.35rem;
      font-weight: 800;
      color: #ffffff;
      margin-bottom: 6px;
    }

    .page-subtitle {
      font-size: 0.82rem;
      color: #94a3b8;
      max-width: 500px;
    }

    .error-banner {
      display: flex;
      align-items: center;
      gap: 10px;
      background: rgba(239, 68, 68, 0.12);
      border: 1px solid rgba(239, 68, 68, 0.35);
      color: #fca5a5;
      padding: 10px 14px;
      border-radius: 10px;
      font-size: 0.85rem;
      margin-bottom: 20px;
    }

    .register-form {
      display: flex;
      flex-direction: column;
      gap: 20px;
    }

    .form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }

    @media (max-width: 640px) {
      .form-grid { grid-template-columns: 1fr; }
      .register-card { padding: 24px 20px; }
    }

    .form-group {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .field-label {
      font-size: 0.8rem;
      font-weight: 600;
      color: #cbd5e1;
    }

    .auth-input, .auth-select {
      width: 100%;
      background: rgba(2, 6, 23, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 10px;
      padding: 10px 14px;
      color: #f8fafc;
      font-size: 0.88rem;
      outline: none;
      transition: all 0.2s;
    }

    .auth-input:focus, .auth-select:focus {
      border-color: #10b981;
      box-shadow: 0 0 0 3px rgba(16, 185, 129, 0.2);
      background: rgba(2, 6, 23, 0.9);
    }

    .auth-select option {
      background: #0f172a;
      color: #ffffff;
    }

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
      margin-top: 10px;
    }
    .submit-btn:hover:not(:disabled) {
      background: linear-gradient(135deg, #10b981 0%, #059669 100%);
      box-shadow: 0 6px 20px rgba(16, 185, 129, 0.5);
      transform: translateY(-1px);
    }
    .submit-btn:disabled { opacity: 0.7; cursor: not-allowed; }

    .login-footer {
      margin-top: 20px;
      text-align: center;
      font-size: 0.85rem;
      color: #94a3b8;
      display: flex;
      justify-content: center;
      gap: 6px;
    }

    .login-link {
      color: #34d399;
      font-weight: 600;
      text-decoration: none;
    }
    .login-link:hover { text-decoration: underline; }

    @keyframes shake {
      0%, 100% { transform: translateX(0); }
      20%, 60% { transform: translateX(-6px); }
      40%, 80% { transform: translateX(6px); }
    }
    .animate-shake { animation: shake 0.4s ease-in-out; }
  `]
})
export class RegisterComponent {
  public authService = inject(AuthService);
  public context = inject(CompanyContextService);
  private toast = inject(ToastService);
  private router = inject(Router);

  public formData: RegisterUserRequest = {
    username: '',
    fullName: '',
    fullNameBn: '',
    email: '',
    phone: '',
    password: '',
    role: 'Maker',
    companyId: '11111111-1111-1111-1111-111111111111',
    branchId: '33333333-3333-3333-3333-333333333331'
  };

  public confirmPassword: string = '';
  public isLoading: boolean = false;
  public errorMessage: string = '';

  public onSubmit(): void {
    this.errorMessage = '';

    if (!this.formData.fullName || !this.formData.username || !this.formData.email || !this.formData.password) {
      this.errorMessage = 'অনুগ্রহ করে সকল আবশ্যকীয় তথ্য (Required Fields) পূরণ করুন।';
      return;
    }

    if (this.formData.password !== this.confirmPassword) {
      this.errorMessage = 'পাসওয়ার্ড এবং কনফার্ম পাসওয়ার্ড মিলছে না (Passwords do not match)।';
      return;
    }

    if (this.formData.password.length < 6) {
      this.errorMessage = 'পাসওয়ার্ড কমপক্ষে ৬ অক্ষরের হতে হবে।';
      return;
    }

    this.isLoading = true;

    this.authService.register(this.formData).subscribe({
      next: (newUser) => {
        this.isLoading = false;
        this.toast.showSuccess(
          `অভিনন্দন! '${newUser.fullName}' ব্যবহারকারী অ্যাকাউন্টটি সফলভাবে তৈরি হয়েছে।`
        );
        // Auto-login with the newly created account
        this.authService.login({ username: newUser.username, password: this.formData.password }).subscribe({
          next: () => this.router.navigate(['/app/portal']),
          error: () => this.router.navigate(['/login'])
        });
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = err.message || 'নিবন্ধন ব্যর্থ হয়েছে। পুনরায় চেষ্টা করুন।';
      }
    });
  }
}
