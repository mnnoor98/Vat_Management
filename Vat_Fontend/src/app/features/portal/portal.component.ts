import { Component, OnInit, inject, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PortalService, ModuleCard } from '../../core/services/portal.service';
import { CompanyContextService } from '../../core/services/company-context.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-portal',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule],
  template: `
    <div class="portal-container animate-fade-in">
      
      <!-- Top Welcome & Search Banner -->
      <div class="portal-hero">
        <div class="hero-content">
          <div class="gov-seal-row">
            <span class="gov-pill">
              <span class="active-dot"></span>
              {{ context.locale() === 'bn' ? 'গণপ্রজাতন্ত্রী বাংলাদেশ সরকার' : 'Government of the People\'s Republic of Bangladesh' }}
            </span>
            <span class="branch-pill" [class.super-pill]="context.isSuperAdmin()">
              {{ context.isSuperAdmin() ? '🌐 All Branches (সকল শাখা উন্মুক্ত)' : '🔒 ' + context.activeBranch().name }}
            </span>
          </div>
          
          <h1 class="hero-title">
            {{ context.locale() === 'bn' ? 'ভ্যাট ব্যবস্থাপনা ও সংবিধিবদ্ধ পোর্টাল' : 'Enterprise VAT Management Portal' }}
          </h1>
          <p class="hero-subtitle">
            {{ context.locale() === 'bn' 
              ? 'স্বাগতম, ' + context.userName() + ' (' + context.userRole() + ')! আপনার অনুমোদিত কার্যক্রমের কার্ড নির্বাচন করুন।' 
              : 'Welcome, ' + context.userName() + ' (' + context.userRole() + ')! Select an authorized module card below to access your statutory tools.' }}
          </p>
        </div>

        <!-- Quick Module Search Filter -->
        <div class="search-bar-wrapper">
          <div class="search-box">
            <svg class="search-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="11" cy="11" r="8"></circle>
              <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
            </svg>
            <input 
              type="text" 
              [(ngModel)]="searchQuery" 
              (ngModelChange)="filterModules()"
              class="search-input" 
              [placeholder]="context.locale() === 'bn' ? 'মডিউল বা চালান অনুসন্ধান করুন...' : 'Search modules, registers, or statutory challans...'" 
            />
            <span *ngIf="searchQuery" class="clear-btn" (click)="searchQuery = ''; filterModules()">✕</span>
          </div>
          <div class="module-counter-badge">
            <strong>{{ filteredModules.length }}</strong> {{ context.locale() === 'bn' ? 'টি মডিউল উন্মুক্ত' : 'Authorized Modules' }}
          </div>
        </div>
      </div>

      <!-- Module Cards Grid (Exactly as in user's design reference) -->
      <div class="cards-grid">
        <div 
          *ngFor="let mod of filteredModules" 
          class="module-card animate-fade-in"
          [ngClass]="mod.colorClass"
          (click)="openModule(mod)"
        >
          <!-- Card Header: Round Icon Badge & Status -->
          <div class="card-top-row">
            <div class="icon-circle">
              <!-- SVG Icons mapped by identifier -->
              <ng-container [ngSwitch]="mod.icon">
                <svg *ngSwitchCase="'shopping_cart'" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <circle cx="9" cy="21" r="1"></circle><circle cx="20" cy="21" r="1"></circle>
                  <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path>
                </svg>
                <svg *ngSwitchCase="'point_of_sale'" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <rect x="2" y="5" width="20" height="14" rx="2"></rect>
                  <line x1="2" y1="10" x2="22" y2="10"></line>
                </svg>
                <svg *ngSwitchCase="'factory'" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M2 20a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V8l-7 5V8l-7 5V4a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z"></path>
                </svg>
                <svg *ngSwitchCase="'inventory_2'" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <polyline points="21 8 21 21 3 21 3 8"></polyline>
                  <rect x="1" y="3" width="22" height="5"></rect>
                  <line x1="10" y1="12" x2="14" y2="12"></line>
                </svg>
                <svg *ngSwitchCase="'verified_user'" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
                  <path d="m9 12 2 2 4-4"></path>
                </svg>
                <svg *ngSwitchCase="'task_alt'" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M12 22c5.523 0 10-4.477 10-10S17.523 2 12 2 2 6.477 2 12s4.477 10 10 10z"></path>
                  <path d="m9 12 2 2 4-4"></path>
                </svg>
                <svg *ngSwitchCase="'dataset'" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <ellipse cx="12" cy="5" rx="9" ry="3"></ellipse>
                  <path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"></path>
                  <path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"></path>
                </svg>
                <svg *ngSwitchCase="'bar_chart'" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <line x1="12" y1="20" x2="12" y2="10"></line>
                  <line x1="18" y1="20" x2="18" y2="4"></line>
                  <line x1="6" y1="20" x2="6" y2="16"></line>
                </svg>
                <svg *ngSwitchDefault width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <circle cx="12" cy="12" r="3"></circle>
                  <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path>
                </svg>
              </ng-container>
            </div>

            <span class="submenus-pill">
              {{ mod.submenus.length }} {{ context.locale() === 'bn' ? 'টি মেনু' : 'Menus' }}
            </span>
          </div>

          <!-- Card Content -->
          <div class="card-body">
            <h3 class="card-title">
              {{ context.locale() === 'bn' ? mod.titleBn : mod.titleEn }}
            </h3>
            <p class="card-desc">
              {{ context.locale() === 'bn' ? mod.descriptionBn : mod.descriptionEn }}
            </p>
          </div>

          <!-- Card Footer -->
          <div class="card-footer">
            <span class="statutory-badge">{{ mod.statutoryRef }}</span>
            <div class="enter-btn">
              <span>{{ context.locale() === 'bn' ? 'প্রবেশ করুন' : 'Open' }}</span>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                <line x1="5" y1="12" x2="19" y2="12"></line>
                <polyline points="12 5 19 12 12 19"></polyline>
              </svg>
            </div>
          </div>
        </div>
      </div>

    </div>
  `,
  styles: [`
    .portal-container {
      max-width: 1400px;
      margin: 0 auto;
      padding: 10px 0 40px 0;
    }

    /* Hero / Header Section */
    .portal-hero {
      background: linear-gradient(135deg, rgba(16, 185, 129, 0.08) 0%, rgba(14, 165, 233, 0.05) 100%);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: var(--radius-xl);
      padding: 30px 36px;
      margin-bottom: 28px;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
      position: relative;
      overflow: hidden;
    }

    .gov-seal-row {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 12px;
      flex-wrap: wrap;
    }

    .gov-pill {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 12px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.3);
      border-radius: var(--radius-full);
      font-size: 0.75rem;
      font-weight: 700;
      color: var(--vat-emerald);
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .branch-pill {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 12px;
      background: rgba(14, 165, 233, 0.12);
      border: 1px solid rgba(14, 165, 233, 0.25);
      border-radius: var(--radius-full);
      font-size: 0.75rem;
      font-weight: 700;
      color: #38bdf8;
    }
    .branch-pill.super-pill {
      background: rgba(168, 85, 247, 0.15);
      border-color: rgba(168, 85, 247, 0.3);
      color: #c084fc;
    }

    .hero-title {
      font-size: 1.85rem;
      font-weight: 900;
      color: #ffffff;
      margin: 0 0 6px 0;
      letter-spacing: -0.5px;
    }

    .hero-subtitle {
      font-size: 0.95rem;
      color: var(--text-muted);
      margin: 0 0 22px 0;
      line-height: 1.5;
    }

    /* Search Bar */
    .search-bar-wrapper {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      flex-wrap: wrap;
    }

    .search-box {
      position: relative;
      flex: 1;
      max-width: 580px;
    }

    .search-icon {
      position: absolute;
      left: 14px;
      top: 50%;
      transform: translateY(-50%);
      color: var(--text-dim);
    }

    .search-input {
      width: 100%;
      background: var(--bg-card);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-lg);
      padding: 12px 38px 12px 42px;
      font-size: 0.9rem;
      color: var(--text-main);
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
      transition: all 0.2s ease;
    }
    .search-input:focus {
      outline: none;
      border-color: var(--vat-teal);
      box-shadow: 0 0 0 3px rgba(16, 185, 129, 0.2);
    }

    .clear-btn {
      position: absolute;
      right: 14px;
      top: 50%;
      transform: translateY(-50%);
      color: var(--text-dim);
      cursor: pointer;
      font-size: 0.85rem;
    }

    .module-counter-badge {
      font-size: 0.85rem;
      color: var(--text-dim);
    }
    .module-counter-badge strong {
      color: var(--vat-emerald);
      font-size: 1.1rem;
    }

    /* Cards Grid (Exactly as in uploaded reference) */
    .cards-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(310px, 1fr));
      gap: 22px;
    }

    .module-card {
      background: #ffffff;
      border: 1px solid #e2e8f0;
      border-radius: 14px;
      padding: 24px;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      min-height: 220px;
      cursor: pointer;
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.04);
      transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
      position: relative;
      overflow: hidden;
    }

    /* Dark Mode Palette Harmonization */
    :host-context(body), body {
      /* In dark theme, card has crisp premium contrast */
    }

    .module-card:hover {
      transform: translateY(-4px);
      box-shadow: 0 12px 24px rgba(0, 0, 0, 0.08);
      border-color: #0ea5e9;
    }

    /* Top Row in Card */
    .card-top-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 14px;
    }

    .icon-circle {
      width: 48px;
      height: 48px;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.2s ease;
    }

    /* Card Color Themes */
    .card-blue .icon-circle {
      background: #e0f2fe;
      color: #0284c7;
    }
    .card-cyan .icon-circle {
      background: #cffafe;
      color: #0891b2;
    }
    .card-indigo .icon-circle {
      background: #e0e7ff;
      color: #4f46e5;
    }
    .card-amber .icon-circle {
      background: #fef3c7;
      color: #d97706;
    }
    .card-emerald .icon-circle {
      background: #d1fae5;
      color: #059669;
    }
    .card-violet .icon-circle {
      background: #ede9fe;
      color: #7c3aed;
    }
    .card-rose .icon-circle {
      background: #ffe4e6;
      color: #e11d48;
    }
    .card-slate .icon-circle {
      background: #f1f5f9;
      color: #475569;
    }

    .module-card:hover .icon-circle {
      transform: scale(1.08);
    }

    .submenus-pill {
      font-size: 0.72rem;
      font-weight: 700;
      padding: 3px 10px;
      border-radius: var(--radius-full);
      background: #f1f5f9;
      color: #64748b;
    }

    /* Card Body */
    .card-body {
      flex: 1;
      margin-bottom: 16px;
    }

    .card-title {
      font-size: 1.08rem;
      font-weight: 750;
      color: #0f172a;
      margin: 0 0 8px 0;
      line-height: 1.35;
    }

    .card-desc {
      font-size: 0.85rem;
      color: #64748b;
      line-height: 1.5;
      margin: 0;
    }

    /* Card Footer */
    .card-footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      border-top: 1px solid #f1f5f9;
      padding-top: 14px;
      margin-top: 4px;
    }

    .statutory-badge {
      font-size: 0.72rem;
      font-weight: 600;
      color: #0284c7;
      background: #f0f9ff;
      padding: 2px 8px;
      border-radius: 6px;
    }

    .enter-btn {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      font-size: 0.8rem;
      font-weight: 700;
      color: #0ea5e9;
      transition: gap 0.2s ease;
    }
    .module-card:hover .enter-btn {
      gap: 8px;
      color: #0284c7;
    }

    @media (max-width: 768px) {
      .portal-hero {
        padding: 20px;
      }
      .hero-title {
        font-size: 1.4rem;
      }
      .cards-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class PortalComponent implements OnInit {
  private portalService = inject(PortalService);
  public context = inject(CompanyContextService);
  private router = inject(Router);

  public allModules: ModuleCard[] = [];
  public filteredModules: ModuleCard[] = [];
  public searchQuery: string = '';

  constructor() {
    effect(() => {
      this.portalService.matrixVersion();
      this.context.currentUser();
      this.loadModules();
    });
  }

  ngOnInit(): void {
    this.loadModules();
  }

  loadModules(): void {
    this.allModules = this.portalService.getAuthorizedModules();
    this.filterModules();
  }

  filterModules(): void {
    if (!this.searchQuery.trim()) {
      this.filteredModules = [...this.allModules];
      return;
    }

    const q = this.searchQuery.toLowerCase();
    this.filteredModules = this.allModules.filter(m => 
      m.titleEn.toLowerCase().includes(q) ||
      m.titleBn.toLowerCase().includes(q) ||
      m.descriptionEn.toLowerCase().includes(q) ||
      m.descriptionBn.toLowerCase().includes(q) ||
      m.submenus.some(s => s.labelEn.toLowerCase().includes(q) || s.labelBn.toLowerCase().includes(q))
    );
  }

  openModule(mod: ModuleCard): void {
    // Navigate directly to the module's functional view (sidebar will show card-wise menu, no cards on page)
    this.router.navigateByUrl(mod.route);
  }
}
