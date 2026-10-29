import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import { PortalService, ModuleCard, ModuleSubmenu } from '../../core/services/portal.service';
import { CompanyContextService } from '../../core/services/company-context.service';

@Component({
  selector: 'app-module-view',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="module-view-container animate-fade-in" *ngIf="moduleData">
      
      <!-- Back to Portal Breadcrumb -->
      <div class="breadcrumb-bar">
        <a routerLink="/app/portal" class="back-link">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
            <line x1="19" y1="12" x2="5" y2="12"></line>
            <polyline points="12 19 5 12 12 5"></polyline>
          </svg>
          {{ context.locale() === 'bn' ? 'সকল মডিউলে ফিরে যান (All Modules)' : 'Back to All Modules' }}
        </a>
        <span class="sep">/</span>
        <span class="current-module-crumb">{{ context.locale() === 'bn' ? moduleData.titleBn : moduleData.titleEn }}</span>
      </div>

      <!-- Module Hero Header Card -->
      <div class="module-hero-card" [ngClass]="moduleData.colorClass">
        <div class="hero-left">
          <div class="hero-icon-box">
            <!-- Icon mapped from module -->
            <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path *ngIf="moduleData.icon === 'shopping_cart'" d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path>
              <rect *ngIf="moduleData.icon === 'point_of_sale'" x="2" y="5" width="20" height="14" rx="2"></rect>
              <path *ngIf="moduleData.icon === 'factory'" d="M2 20a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V8l-7 5V8l-7 5V4a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z"></path>
              <polyline *ngIf="moduleData.icon === 'inventory_2'" points="21 8 21 21 3 21 3 8"></polyline>
              <path *ngIf="moduleData.icon === 'verified_user'" d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
              <path *ngIf="moduleData.icon === 'task_alt'" d="M12 22c5.523 0 10-4.477 10-10S17.523 2 12 2 2 6.477 2 12s4.477 10 10 10z"></path>
              <ellipse *ngIf="moduleData.icon === 'dataset'" cx="12" cy="5" rx="9" ry="3"></ellipse>
              <line *ngIf="moduleData.icon === 'bar_chart'" x1="12" y1="20" x2="12" y2="10"></line>
              <circle *ngIf="moduleData.icon === 'admin_panel_settings'" cx="12" cy="12" r="3"></circle>
            </svg>
          </div>
          <div>
            <div class="statutory-pill">{{ moduleData.statutoryRef }}</div>
            <h1 class="module-title">
              {{ context.locale() === 'bn' ? moduleData.titleBn : moduleData.titleEn }}
            </h1>
            <p class="module-subtitle">
              {{ context.locale() === 'bn' ? moduleData.descriptionBn : moduleData.descriptionEn }}
            </p>
          </div>
        </div>

        <div class="hero-right">
          <div class="user-access-box">
            <span class="access-label">{{ context.locale() === 'bn' ? 'সক্রিয় ব্যবহারকারী ও ভূমিকা:' : 'Active User & Scope:' }}</span>
            <strong class="access-user">{{ context.userName() }} ({{ context.userRole() }})</strong>
            <span class="access-branch font-mono">{{ context.activeBranch().name }}</span>
          </div>
        </div>
      </div>

      <!-- Section Title -->
      <div class="section-header-row">
        <div>
          <h2 class="section-title">
            {{ context.locale() === 'bn' ? 'অনুমোদিত মেনু ও কার্যক্রম তালিকা' : 'Authorized Menus & Functional Operations' }}
          </h2>
          <p class="section-subtitle">
            {{ context.locale() === 'bn' 
              ? 'আপনার ভূমিকা (' + context.userRole() + ') অনুযায়ী এই মডিউলের যেসকল মেনুতে আপনার প্রবেশাধিকার রয়েছে:' 
              : 'Submenus and tools accessible based on your role privileges (' + context.userRole() + '):' }}
          </p>
        </div>
        <span class="authorized-count">
          {{ authorizedSubmenus.length }} {{ context.locale() === 'bn' ? 'টি মেনু সক্রিয়' : 'Menus Available' }}
        </span>
      </div>

      <!-- Submenu Cards Grid (Card-in-Card Architecture) -->
      <div class="submenu-cards-grid">
        <div 
          *ngFor="let sub of authorizedSubmenus; let i = index" 
          class="submenu-card animate-fade-in"
          (click)="navigateToSubmenu(sub)"
        >
          <div class="sub-card-header">
            <div class="sub-number font-mono">0{{ i + 1 }}</div>
            <div class="sub-icon-badge">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <rect *ngIf="sub.icon === 'receipt'" x="3" y="3" width="18" height="18" rx="2"></rect>
                <circle *ngIf="sub.icon === 'add_circle'" cx="12" cy="12" r="10"></circle>
                <path *ngIf="sub.icon === 'menu_book'" d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"></path>
                <rect *ngIf="sub.icon === 'format_list_bulleted'" x="2" y="5" width="20" height="14" rx="2"></rect>
                <polyline *ngIf="sub.icon === 'print'" points="6 9 6 2 18 2 18 9"></polyline>
                <path *ngIf="sub.icon === 'auto_stories'" d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"></path>
                <rect *ngIf="sub.icon === 'calculate'" x="4" y="2" width="16" height="20" rx="2"></rect>
                <polyline *ngIf="sub.icon === 'analytics'" points="22 12 18 12 15 21 9 3 6 12 2 12"></polyline>
                <rect *ngIf="sub.icon === 'local_shipping'" x="1" y="3" width="15" height="13"></rect>
                <path *ngIf="sub.icon === 'assignment'" d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"></path>
                <rect *ngIf="sub.icon === 'account_balance'" x="2" y="7" width="20" height="14" rx="2"></rect>
                <circle *ngIf="sub.icon === 'badge'" cx="12" cy="12" r="10"></circle>
                <path *ngIf="sub.icon === 'people'" d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
                <path *ngIf="sub.icon === 'security'" d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
                <circle *ngIf="!['receipt','add_circle','menu_book','format_list_bulleted','print','auto_stories','calculate','analytics','local_shipping','assignment','account_balance','badge','people','security'].includes(sub.icon)" cx="12" cy="12" r="10"></circle>
              </svg>
            </div>
          </div>

          <div class="sub-card-body">
            <h3 class="sub-card-title">
              {{ context.locale() === 'bn' ? sub.labelBn : sub.labelEn }}
            </h3>
            <p class="sub-card-desc">
              {{ context.locale() === 'bn' ? sub.descriptionBn : sub.descriptionEn }}
            </p>
          </div>

          <div class="sub-card-footer">
            <span class="sub-action-btn">
              <span>{{ context.locale() === 'bn' ? 'কার্যক্রম শুরু করুন' : 'Launch Feature' }}</span>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                <line x1="5" y1="12" x2="19" y2="12"></line>
                <polyline points="12 5 19 12 12 19"></polyline>
              </svg>
            </span>
          </div>
        </div>
      </div>

    </div>
  `,
  styles: [`
    .module-view-container {
      max-width: 1400px;
      margin: 0 auto;
      padding: 10px 0 40px 0;
    }

    /* Breadcrumb Bar */
    .breadcrumb-bar {
      display: flex;
      align-items: center;
      gap: 10px;
      margin-bottom: 20px;
      font-size: 0.88rem;
    }

    .back-link {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      color: var(--vat-teal);
      text-decoration: none;
      font-weight: 700;
      padding: 6px 12px;
      background: var(--bg-card);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-md);
      transition: all 0.2s ease;
    }
    .back-link:hover {
      background: var(--bg-card-subtle);
      border-color: var(--vat-emerald);
      transform: translateX(-2px);
    }

    .sep {
      color: var(--text-dim);
    }

    .current-module-crumb {
      color: var(--text-muted);
      font-weight: 600;
    }

    /* Module Hero Header */
    .module-hero-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: var(--radius-xl);
      padding: 28px 34px;
      margin-bottom: 30px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 24px;
      box-shadow: 0 4px 16px rgba(0, 0, 0, 0.04);
      flex-wrap: wrap;
    }

    .hero-left {
      display: flex;
      align-items: center;
      gap: 22px;
      flex: 1;
      min-width: 300px;
    }

    .hero-icon-box {
      width: 64px;
      height: 64px;
      border-radius: 16px;
      background: #e0f2fe;
      color: #0284c7;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 4px 12px rgba(2, 132, 199, 0.15);
      flex-shrink: 0;
    }

    .statutory-pill {
      font-size: 0.72rem;
      font-weight: 700;
      color: #0284c7;
      background: #f0f9ff;
      border: 1px solid #bae6fd;
      padding: 2px 10px;
      border-radius: var(--radius-full);
      display: inline-block;
      margin-bottom: 6px;
      text-transform: uppercase;
    }

    .module-title {
      font-size: 1.6rem;
      font-weight: 850;
      color: #0f172a;
      margin: 0 0 6px 0;
    }

    .module-subtitle {
      font-size: 0.9rem;
      color: #64748b;
      margin: 0;
      line-height: 1.5;
      max-width: 720px;
    }

    .hero-right {
      display: flex;
      align-items: center;
    }

    .user-access-box {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
      padding: 12px 18px;
      display: flex;
      flex-direction: column;
      gap: 3px;
      text-align: right;
    }

    .access-label {
      font-size: 0.72rem;
      color: #64748b;
      text-transform: uppercase;
    }

    .access-user {
      font-size: 0.88rem;
      color: #0f172a;
    }

    .access-branch {
      font-size: 0.75rem;
      color: #0284c7;
    }

    /* Section Header */
    .section-header-row {
      display: flex;
      align-items: flex-end;
      justify-content: space-between;
      margin-bottom: 20px;
      gap: 16px;
      flex-wrap: wrap;
    }

    .section-title {
      font-size: 1.25rem;
      font-weight: 800;
      color: var(--text-main);
      margin: 0 0 4px 0;
    }

    .section-subtitle {
      font-size: 0.85rem;
      color: var(--text-muted);
      margin: 0;
    }

    .authorized-count {
      font-size: 0.8rem;
      font-weight: 700;
      color: var(--vat-emerald);
      background: rgba(16, 185, 129, 0.1);
      border: 1px solid rgba(16, 185, 129, 0.25);
      padding: 4px 12px;
      border-radius: var(--radius-full);
    }

    /* Submenu Cards Grid */
    .submenu-cards-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 22px;
    }

    .submenu-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 14px;
      padding: 24px;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      min-height: 200px;
      cursor: pointer;
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.04);
      transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
    }

    .submenu-card:hover {
      transform: translateY(-4px);
      box-shadow: 0 12px 24px rgba(0, 0, 0, 0.08);
      border-color: #0ea5e9;
    }

    .sub-card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 14px;
    }

    .sub-number {
      font-size: 0.85rem;
      font-weight: 800;
      color: #94a3b8;
    }

    .sub-icon-badge {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      background: #f0f9ff;
      color: #0284c7;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: transform 0.2s ease;
    }

    .submenu-card:hover .sub-icon-badge {
      transform: scale(1.08);
      background: #0284c7;
      color: #ffffff;
    }

    .sub-card-body {
      flex: 1;
      margin-bottom: 16px;
    }

    .sub-card-title {
      font-size: 1.05rem;
      font-weight: 750;
      color: #0f172a;
      margin: 0 0 6px 0;
      line-height: 1.35;
    }

    .sub-card-desc {
      font-size: 0.84rem;
      color: #64748b;
      line-height: 1.5;
      margin: 0;
    }

    .sub-card-footer {
      border-top: 1px solid #f1f5f9;
      padding-top: 14px;
    }

    .sub-action-btn {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 0.85rem;
      font-weight: 700;
      color: #0ea5e9;
      transition: gap 0.2s ease;
    }

    .submenu-card:hover .sub-action-btn {
      gap: 10px;
      color: #0284c7;
    }

    @media (max-width: 768px) {
      .module-hero-card {
        padding: 20px;
      }
      .hero-left {
        flex-direction: column;
        align-items: flex-start;
      }
      .user-access-box {
        text-align: left;
      }
      .submenu-cards-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class ModuleViewComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private portalService = inject(PortalService);
  public context = inject(CompanyContextService);

  public moduleCode: string = '';
  public moduleData: ModuleCard | undefined;
  public authorizedSubmenus: ModuleSubmenu[] = [];

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      this.moduleCode = params.get('code') || '';
      this.loadModule();
    });
  }

  loadModule(): void {
    this.moduleData = this.portalService.getModuleByCode(this.moduleCode);
    if (!this.moduleData) {
      // Fallback
      this.router.navigate(['/app/portal']);
      return;
    }

    this.authorizedSubmenus = this.portalService.getAuthorizedSubmenus(this.moduleCode);
  }

  navigateToSubmenu(sub: ModuleSubmenu): void {
    this.router.navigateByUrl(sub.route);
  }
}
