import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { CompanyContextService } from '../../core/services/company-context.service';
import { MockDataService } from '../../core/services/mock-data.service';
import { NavigationService } from '../../core/services/navigation.service';
import { ToastService } from '../../core/services/toast.service';
import { AuthService } from '../../core/services/auth.service';
import { BackendHealthService } from '../../core/services/backend-health.service';

@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <header class="topbar">
      <!-- Left: Mobile toggle & Breadcrumb/Context Indicator -->
      <div class="topbar-left">
        <button *ngIf="!isPortalPage()" class="icon-btn toggle-btn" (click)="navService.toggleSidebar()" title="Toggle Sidebar">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="3" y1="12" x2="21" y2="12"></line>
            <line x1="3" y1="6" x2="21" y2="6"></line>
            <line x1="3" y1="18" x2="21" y2="18"></line>
          </svg>
        </button>

        <a *ngIf="!isPortalPage()" routerLink="/app/portal" class="portal-launcher-btn" [title]="contextService.locale() === 'bn' ? 'সকল মডিউল লঞ্চপ্যাড' : 'Module Launchpad'">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="3" y="3" width="7" height="7"></rect>
            <rect x="14" y="3" width="7" height="7"></rect>
            <rect x="14" y="14" width="7" height="7"></rect>
            <rect x="3" y="14" width="7" height="7"></rect>
          </svg>
          <span class="portal-launcher-text">{{ contextService.locale() === 'bn' ? 'সকল মডিউল' : 'All Modules' }}</span>
        </a>

        <div class="company-badge-box">
          <div class="company-label">
            <span class="active-dot"></span>
            {{ contextService.locale() === 'bn' ? 'বর্তমান কোম্পানি ও ভ্যাট বিআইএন' : 'ACTIVE ENTITY & BIN' }}
          </div>
          <div class="company-name-row">
            <select *ngIf="contextService.isSuperAdmin()" class="context-select company-select" [value]="contextService.activeCompany().id" (change)="onCompanyChange($event)">
              <option *ngFor="let c of mockData.companies" [value]="c.id">
                {{ contextService.locale() === 'bn' ? c.nameBn : c.name }}
              </option>
            </select>
            <span *ngIf="!contextService.isSuperAdmin()" class="locked-company-text">
              {{ contextService.companyName() }}
            </span>
            <span class="bin-chip">BIN: {{ contextService.activeCompany().bin }}</span>
          </div>
        </div>
      </div>

      <!-- Right: Branch, Fiscal Period, Language, Approvals, Profile -->
      <div class="topbar-right">
        <!-- Live Backend Environment Indicator -->
        <button class="backend-health-pill" 
                (click)="onRefreshBackendHealth()" 
                [title]="getBackendTooltip()"
                [ngClass]="'status-' + healthService.status()">
          <span class="status-pulse-dot" [ngClass]="healthService.status()"></span>
          <span class="backend-info-text">
            <span class="backend-tag">API</span>
            <span class="backend-status-name">
              {{ healthService.status() === 'online' ? 'Live' : (healthService.status() === 'checking' ? '...' : 'Offline') }}
            </span>
          </span>
          <span class="backend-meta-pill" *ngIf="healthService.status() === 'online'">
            :5080
          </span>
        </button>

        <!-- Branch Selector -->
        <div class="context-pill" [class.locked-pill]="!contextService.isSuperAdmin()">
          <span class="pill-label">{{ contextService.locale() === 'bn' ? 'শাখা:' : 'Branch:' }}</span>
          <select *ngIf="contextService.isSuperAdmin()" class="context-select branch-select" [value]="contextService.activeBranch().id" (change)="onBranchChange($event)">
            <option *ngFor="let b of availableBranches" [value]="b.id">
              {{ b.code }} ({{ b.binExtension }})
            </option>
          </select>
          <div *ngIf="!contextService.isSuperAdmin()" class="locked-branch-badge" [title]="contextService.locale() === 'bn' ? 'শাখা লক করা (শুধুমাত্র সুপার অ্যাডমিন পরিবর্তন করতে পারে)' : 'Branch restricted to your assigned post'">
            <span class="lock-icon">🔒</span>
            <span class="branch-name-text">{{ contextService.activeBranch().code || 'ACI-HQ' }} ({{ contextService.activeBranch().binExtension || '0101' }})</span>
          </div>
        </div>

        <!-- Fiscal Period Selector -->
        <div class="context-pill period-pill">
          <span class="pill-label">{{ contextService.locale() === 'bn' ? 'কর মেয়াদ:' : 'Tax Period:' }}</span>
          <select class="context-select period-select" [value]="contextService.activeFiscalPeriod().id" (change)="onPeriodChange($event)">
            <option *ngFor="let p of mockData.fiscalPeriods" [value]="p.id">
              {{ contextService.locale() === 'bn' ? p.monthBn : p.month + ' ' + p.fiscalYear }}
            </option>
          </select>
        </div>

        <!-- Language Switcher (EN / BN) -->
        <button class="lang-toggle-btn" (click)="contextService.toggleLocale()">
          <span [class.active-lang]="contextService.locale() === 'en'">EN</span>
          <span class="lang-sep">|</span>
          <span [class.active-lang]="contextService.locale() === 'bn'">বাংলা</span>
        </button>

        <!-- Approval Inbox Quick Shortcut with Badge -->
        <a routerLink="/app/workflow/my-tasks" class="icon-btn notification-btn" title="Pending Approvals">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
            <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
          </svg>
          <span class="badge-counter" *ngIf="pendingTaskCount > 0">{{ pendingTaskCount }}</span>
        </a>

        <!-- Persona Simulator (RBAC Switcher) -->
        <div class="persona-switcher-pill" [attr.data-role]="contextService.userRole()">
          <div class="avatar-circle" [ngClass]="getRoleClass(contextService.userRole())">
            {{ getUserInitials(contextService.currentUser().fullName) }}
          </div>
          <div class="user-meta">
            <div class="user-title-row">
              <span class="user-role-badge" [ngClass]="getRoleClass(contextService.userRole())">
                {{ contextService.userRole() }}
              </span>
              <span class="sim-badge" title="Live RBAC Persona Simulator">RBAC</span>
            </div>
            <select class="persona-select" [value]="contextService.currentUser().id" (change)="onUserChange($event)" title="সক্রিয় পারসোনা পরিবর্তন করুন">
              <option *ngFor="let u of mockData.availableUsers" [value]="u.id">
                {{ contextService.locale() === 'bn' ? u.fullNameBn : u.fullName }} ({{ u.role }})
              </option>
            </select>
          </div>
        </div>

        <!-- Logout Button -->
        <button class="logout-pill-btn" (click)="onLogout()" [title]="contextService.locale() === 'bn' ? 'সিস্টেম থেকে লগআউট করুন' : 'Sign Out'">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path>
            <polyline points="16 17 21 12 16 7"></polyline>
            <line x1="21" y1="12" x2="9" y2="12"></line>
          </svg>
          <span class="logout-text">{{ contextService.locale() === 'bn' ? 'লগআউট' : 'Logout' }}</span>
        </button>
      </div>
    </header>
  `,
  styles: [`
    .topbar {
      height: var(--topbar-height);
      background: var(--bg-topbar);
      backdrop-filter: blur(12px);
      border-bottom: 1px solid var(--border-card);
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 0 24px;
      position: sticky;
      top: 0;
      z-index: 40;
    }

    .topbar-left {
      display: flex;
      align-items: center;
      gap: 18px;
    }

    .icon-btn {
      background: var(--bg-card);
      border: 1px solid var(--border-card);
      color: var(--text-muted);
      width: 40px;
      height: 40px;
      border-radius: var(--radius-md);
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      position: relative;
      transition: all 0.2s ease;
    }
    .icon-btn:hover {
      background: var(--bg-card-hover);
      color: var(--text-main);
      border-color: var(--border-subtle);
    }

    .company-badge-box {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .company-label {
      font-size: 0.68rem;
      font-weight: 700;
      letter-spacing: 0.06em;
      color: var(--vat-teal);
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .active-dot {
      width: 6px;
      height: 6px;
      background: var(--vat-emerald);
      border-radius: 50%;
      box-shadow: 0 0 8px var(--vat-emerald);
    }

    .company-name-row {
      display: flex;
      align-items: center;
      gap: 10px;
    }

    .context-select {
      background: transparent;
      border: none;
      color: var(--text-main);
      font-weight: 700;
      font-size: 0.95rem;
      cursor: pointer;
      outline: none;
      font-family: var(--font-heading);
    }
    .context-select option {
      background: var(--bg-card);
      color: var(--text-main);
    }

    .bin-chip {
      background: rgba(16, 185, 129, 0.12);
      color: var(--vat-emerald);
      border: 1px solid rgba(16, 185, 129, 0.3);
      padding: 2px 8px;
      border-radius: var(--radius-sm);
      font-size: 0.72rem;
      font-family: var(--font-mono);
      font-weight: 600;
    }

    .portal-launcher-btn {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 6px 12px;
      background: rgba(16, 185, 129, 0.12);
      border: 1px solid rgba(16, 185, 129, 0.3);
      border-radius: var(--radius-md);
      color: var(--vat-emerald);
      text-decoration: none;
      font-size: 0.8rem;
      font-weight: 700;
      transition: all 0.2s ease;
      white-space: nowrap;
    }
    .portal-launcher-btn:hover {
      background: var(--vat-emerald);
      color: #ffffff;
      transform: translateY(-1px);
    }

    .topbar-right {
      display: flex;
      align-items: center;
      gap: 14px;
    }

    .backend-health-pill {
      display: inline-flex;
      align-items: center;
      gap: 7px;
      padding: 5px 10px;
      border-radius: var(--radius-md);
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid var(--border-card);
      cursor: pointer;
      font-family: var(--font-body);
      transition: all 0.2s ease;
    }
    .backend-health-pill:hover {
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(0,0,0,0.2);
    }
    .backend-health-pill.status-online {
      border-color: rgba(16, 185, 129, 0.4);
      background: rgba(16, 185, 129, 0.08);
    }
    .backend-health-pill.status-offline {
      border-color: rgba(239, 68, 68, 0.4);
      background: rgba(239, 68, 68, 0.08);
    }
    .status-pulse-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #94a3b8;
    }
    .status-pulse-dot.online {
      background: #10b981;
      box-shadow: 0 0 8px #10b981;
      animation: pulse-dot 2s infinite;
    }
    .status-pulse-dot.offline {
      background: #ef4444;
      box-shadow: 0 0 8px #ef4444;
    }
    .status-pulse-dot.checking {
      background: #f59e0b;
      animation: pulse-dot 1s infinite;
    }
    @keyframes pulse-dot {
      0% { opacity: 0.6; transform: scale(0.9); }
      50% { opacity: 1; transform: scale(1.15); }
      100% { opacity: 0.6; transform: scale(0.9); }
    }
    .backend-info-text {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 0.78rem;
    }
    .backend-tag {
      font-size: 0.65rem;
      font-weight: 800;
      letter-spacing: 0.04em;
      color: var(--text-muted);
    }
    .backend-status-name {
      font-weight: 700;
      color: var(--text-main);
    }
    .backend-meta-pill {
      font-size: 0.68rem;
      font-family: var(--font-mono);
      font-weight: 600;
      color: #10b981;
      background: rgba(16, 185, 129, 0.15);
      padding: 1px 5px;
      border-radius: 4px;
    }

    .context-pill {
      background: var(--bg-card);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-md);
      padding: 6px 12px;
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .pill-label {
      font-size: 0.72rem;
      font-weight: 600;
      color: var(--text-dim);
      text-transform: uppercase;
    }

    .context-pill select {
      font-size: 0.8rem;
      font-weight: 600;
    }

    .locked-pill {
      background: rgba(16, 185, 129, 0.05);
      border-color: rgba(16, 185, 129, 0.2);
    }

    .locked-company-text {
      font-size: 0.88rem;
      font-weight: 700;
      color: var(--text-main);
    }

    .locked-branch-badge {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      font-size: 0.8rem;
      font-weight: 700;
      color: var(--vat-emerald);
      cursor: default;
    }
    .locked-branch-badge .lock-icon {
      font-size: 0.72rem;
    }

    .lang-toggle-btn {
      background: var(--bg-card);
      border: 1px solid var(--border-card);
      color: var(--text-muted);
      padding: 8px 14px;
      border-radius: var(--radius-md);
      font-size: 0.78rem;
      font-weight: 700;
      cursor: pointer;
      display: flex;
      align-items: center;
      gap: 6px;
      transition: all 0.2s ease;
    }
    .lang-toggle-btn:hover {
      border-color: var(--border-subtle);
    }
    .active-lang {
      color: var(--vat-emerald);
    }
    .lang-sep {
      color: var(--text-dim);
    }

    .badge-counter {
      position: absolute;
      top: -4px;
      right: -4px;
      background: var(--vat-rose);
      color: #ffffff;
      font-size: 0.68rem;
      font-weight: 800;
      width: 18px;
      height: 18px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      border: 2px solid var(--bg-app);
    }

    .persona-switcher-pill {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 4px 12px 4px 4px;
      background: var(--bg-card);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-lg);
      transition: all 0.2s ease;
    }
    .persona-switcher-pill:hover {
      border-color: var(--border-subtle);
    }

    .avatar-circle {
      width: 34px;
      height: 34px;
      font-weight: 700;
      font-size: 0.78rem;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.2);
    }

    .user-meta {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .user-title-row {
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .user-role-badge {
      font-size: 0.65rem;
      font-weight: 700;
      padding: 1px 6px;
      border-radius: var(--radius-sm);
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .sim-badge {
      font-size: 0.58rem;
      font-weight: 800;
      background: rgba(255, 255, 255, 0.08);
      color: var(--text-dim);
      padding: 1px 4px;
      border-radius: 3px;
      letter-spacing: 0.05em;
    }

    .persona-select {
      background: transparent;
      border: none;
      color: var(--text-main);
      font-weight: 700;
      font-size: 0.82rem;
      cursor: pointer;
      outline: none;
      padding: 0;
      margin: 0;
      max-width: 175px;
    }
    .persona-select option {
      background: var(--bg-card);
      color: var(--text-main);
      padding: 4px 8px;
    }

    .role-taxmanager {
      background: rgba(16, 185, 129, 0.18);
      color: var(--vat-emerald);
      border: 1px solid rgba(16, 185, 129, 0.4);
    }
    .role-maker {
      background: rgba(245, 158, 11, 0.18);
      color: var(--vat-amber);
      border: 1px solid rgba(245, 158, 11, 0.4);
    }
    .role-checker {
      background: rgba(59, 130, 246, 0.18);
      color: #60a5fa;
      border: 1px solid rgba(59, 130, 246, 0.4);
    }
    .role-approver {
      background: rgba(168, 85, 247, 0.18);
      color: #c084fc;
      border: 1px solid rgba(168, 85, 247, 0.4);
    }
    .role-auditor {
      background: rgba(244, 63, 94, 0.18);
      color: var(--vat-rose);
      border: 1px solid rgba(244, 63, 94, 0.4);
    }

    .logout-pill-btn {
      display: flex;
      align-items: center;
      gap: 6px;
      background: rgba(239, 68, 68, 0.08);
      border: 1px solid rgba(239, 68, 68, 0.25);
      color: #f87171;
      padding: 6px 12px;
      border-radius: var(--radius-md);
      font-size: 0.78rem;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .logout-pill-btn:hover {
      background: rgba(239, 68, 68, 0.18);
      border-color: #ef4444;
      color: #fca5a5;
      transform: translateY(-1px);
    }
    @media (max-width: 640px) {
      .logout-text { display: none; }
    }

    @media (max-width: 900px) {
      .context-pill, .user-meta {
        display: none;
      }
    }
  `]
})
export class TopbarComponent {
  private toast = inject(ToastService);
  public authService = inject(AuthService);
  private router = inject(Router);

  public isPortalPage(): boolean {
    return this.router.url.includes('/app/portal');
  }

  onLogout(): void {
    const isBn = this.contextService.locale() === 'bn';
    if (confirm(isBn ? 'আপনি কি নিশ্চিত যে আপনি লগআউট করতে চান?' : 'Are you sure you want to sign out?')) {
      this.authService.logout();
    }
  }

  public healthService = inject(BackendHealthService);

  onRefreshBackendHealth(): void {
    this.healthService.checkHealth();
    const isBn = this.contextService.locale() === 'bn';
    setTimeout(() => {
      if (this.healthService.status() === 'online') {
        this.toast.success(
          isBn ? `ব্যাকএন্ড সচল (লেটেন্সি: ${this.healthService.latencyMs()}ms, ডাটাবেজ রেডি)` : `Backend API is healthy (Latency: ${this.healthService.latencyMs()}ms, DB ready)`,
          isBn ? 'ব্যাকএন্ড এনভায়রনমেন্ট' : 'Backend Environment'
        );
      } else {
        this.toast.warning(
          isBn ? 'ব্যাকএন্ড সংযোগ বিচ্ছিন্ন। মক ডাটা ব্যবহার হচ্ছে।' : 'Backend disconnected. Operating on fallback mode.',
          isBn ? 'ব্যাকএন্ড সতর্কতা' : 'Backend Warning'
        );
      }
    }, 600);
  }

  getBackendTooltip(): string {
    const isBn = this.contextService.locale() === 'bn';
    if (this.healthService.status() === 'online') {
      return isBn 
        ? `ASP.NET Core 10 ব্যাকএন্ড পোর্ট ৫০৮০-এ সক্রিয়। ডাটাবেজ: PostgreSQL 18 (কানেক্টেড)। ক্লিক করে পুনরায় টেস্ট করুন।`
        : `ASP.NET Core 10 backend active on :5080. Database: PostgreSQL 18 (Connected). Latency: ${this.healthService.latencyMs()}ms. Click to re-check.`;
    }
    return isBn 
      ? `ব্যাকএন্ড অফলাইন। ক্লিক করে পুনরায় কানেক্ট করার চেষ্টা করুন।`
      : `Backend offline. Click to retry connection.`;
  }

  constructor(
    public contextService: CompanyContextService,
    public mockData: MockDataService,
    public navService: NavigationService
  ) {}

  get availableBranches() {
    const compId = this.contextService.activeCompany().id;
    return this.mockData.branches.filter(b => b.companyId === compId);
  }

  get pendingTaskCount(): number {
    return this.mockData.approvalTasks.filter(t => t.status === 'Pending').length;
  }

  onCompanyChange(event: Event): void {
    if (!this.contextService.isSuperAdmin()) return;
    const val = (event.target as HTMLSelectElement).value;
    this.contextService.switchCompany(val);
    const comp = this.contextService.activeCompany();
    this.toast.info(
      this.contextService.locale() === 'bn' ? `কোম্পানি পরিবর্তিত: ${comp.nameBn}` : `Active entity switched to ${comp.name}`,
      'কোম্পানি পরিবর্তন (Entity Switched)'
    );
  }

  onBranchChange(event: Event): void {
    if (!this.contextService.isSuperAdmin()) return;
    const val = (event.target as HTMLSelectElement).value;
    this.contextService.switchBranch(val);
    const branch = this.contextService.activeBranch();
    this.toast.info(
      this.contextService.locale() === 'bn' ? `শাখা পরিবর্তিত: ${branch.nameBn}` : `Active branch switched to ${branch.name}`,
      'শাখা পরিবর্তন (Branch Switched)'
    );
  }

  onPeriodChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.contextService.switchFiscalPeriod(val);
    const period = this.contextService.activeFiscalPeriod();
    this.toast.info(
      this.contextService.locale() === 'bn' ? `কর মেয়াদ: ${period.monthBn}` : `Tax Period switched to ${period.month} ${period.fiscalYear}`,
      'কর মেয়াদ (Tax Period)'
    );
  }

  onUserChange(event: Event): void {
    const val = (event.target as HTMLSelectElement).value;
    this.contextService.switchUser(val);
    const user = this.contextService.currentUser();
    const isBn = this.contextService.locale() === 'bn';
    this.toast.success(
      isBn ? `${user.fullNameBn} (${user.role}) হিসেবে ভূমিকা কার্যকর করা হয়েছে।` : `Role persona switched to ${user.fullName} (${user.role}).`,
      isBn ? 'সক্রিয় ব্যবহারকারী ও আরবেসি (RBAC Switch)' : 'RBAC Persona Switched'
    );
  }

  getUserInitials(name: string): string {
    if (!name) return 'U';
    const parts = name.replace(/^(Mr\.|Ms\.|Engr\.|Dr\.)\s*/i, '').trim().split(/\s+/);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[1][0]).toUpperCase();
    }
    return parts[0].slice(0, 2).toUpperCase();
  }

  getRoleClass(role: string): string {
    const lower = (role || '').toLowerCase();
    if (lower.includes('superadmin')) return 'role-superadmin';
    if (lower.includes('taxmanager')) return 'role-taxmanager';
    if (lower.includes('checker')) return 'role-checker';
    if (lower.includes('approver')) return 'role-approver';
    if (lower.includes('auditor')) return 'role-auditor';
    if (lower.includes('maker')) return 'role-maker';
    return 'role-taxmanager';
  }
}
