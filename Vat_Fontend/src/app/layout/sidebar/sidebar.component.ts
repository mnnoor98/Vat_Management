import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs/operators';
import { NavigationService } from '../../core/services/navigation.service';
import { CompanyContextService } from '../../core/services/company-context.service';
import { PortalService, ModuleCard, ModuleSubmenu } from '../../core/services/portal.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <aside class="sidebar" [class.collapsed]="navService.isSidebarCollapsed()">
      
      <!-- Top Action: Back to All Modules Launchpad -->
      <div class="sidebar-top-action">
        <a 
          routerLink="/app/portal" 
          class="back-to-portal-btn"
          [title]="contextService.locale() === 'bn' ? 'সকল মডিউল লঞ্চপ্যাডে ফিরে যান' : 'Back to All Modules Launchpad'"
        >
          <div class="portal-grid-icon">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
              <line x1="19" y1="12" x2="5" y2="12"></line>
              <polyline points="12 19 5 12 12 5"></polyline>
            </svg>
          </div>
          <div class="back-text" *ngIf="!navService.isSidebarCollapsed()">
            <span class="back-title">{{ contextService.locale() === 'bn' ? 'সকল মডিউল' : 'All Modules' }}</span>
            <span class="back-subtitle">{{ contextService.locale() === 'bn' ? 'লঞ্চপ্যাডে ফিরুন' : 'Return to Launchpad' }}</span>
          </div>
        </a>
      </div>

      <!-- Active Module Card Header -->
      <div class="active-module-banner" *ngIf="activeModule() && !navService.isSidebarCollapsed()">
        <div class="module-banner-pill" [ngClass]="activeModule()?.colorClass">
          <div class="module-banner-icon">
            <ng-container [ngSwitch]="activeModule()?.icon">
              <svg *ngSwitchCase="'shopping_cart'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="9" cy="21" r="1"></circle><circle cx="20" cy="21" r="1"></circle><path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path></svg>
              <svg *ngSwitchCase="'point_of_sale'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="5" width="20" height="14" rx="2"></rect><line x1="2" y1="10" x2="22" y2="10"></line></svg>
              <svg *ngSwitchCase="'factory'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 20a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V8l-7 5V8l-7 5V4a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2Z"></path></svg>
              <svg *ngSwitchCase="'inventory_2'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="21 8 21 21 3 21 3 8"></polyline><rect x="1" y="3" width="22" height="5"></rect><line x1="10" y1="12" x2="14" y2="12"></line></svg>
              <svg *ngSwitchCase="'verified_user'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path></svg>
              <svg *ngSwitchCase="'task_alt'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 22c5.523 0 10-4.477 10-10S17.523 2 12 2 2 6.477 2 12s4.477 10 10 10z"></path><path d="m9 12 2 2 4-4"></path></svg>
              <svg *ngSwitchCase="'dataset'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><ellipse cx="12" cy="5" rx="9" ry="3"></ellipse><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"></path></svg>
              <svg *ngSwitchCase="'bar_chart'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="12" y1="20" x2="12" y2="10"></line><line x1="18" y1="20" x2="18" y2="4"></line><line x1="6" y1="20" x2="6" y2="16"></line></svg>
              <svg *ngSwitchDefault width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"></circle><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path></svg>
            </ng-container>
          </div>
          <div class="module-banner-info">
            <span class="module-ref-tag">{{ activeModule()?.statutoryRef }}</span>
            <h2 class="module-header-title">
              {{ contextService.locale() === 'bn' ? activeModule()?.titleBn : activeModule()?.titleEn }}
            </h2>
          </div>
        </div>
      </div>

      <!-- Navigation Tree: ONLY Card-Wise Submenus for the Active Card -->
      <nav class="nav-container">
        <div class="menu-label-heading" *ngIf="!navService.isSidebarCollapsed()">
          {{ contextService.locale() === 'bn' ? 'অনুমোদিত কার্যক্রম ও মেনু' : 'CARD OPERATIONS' }}
        </div>

        <ul class="nav-list">
          <!-- CARD-WISE SUBMENU ITEMS -->
          <li *ngFor="let sub of activeSubmenus()" class="nav-item">
            <a
              [routerLink]="sub.route"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: false }"
              class="nav-link"
              [title]="contextService.locale() === 'bn' ? sub.labelBn : sub.labelEn"
            >
              <span class="nav-icon-wrapper">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
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
              </span>
              <span class="nav-label" *ngIf="!navService.isSidebarCollapsed()">
                {{ contextService.locale() === 'bn' ? sub.labelBn : sub.labelEn }}
              </span>
            </a>
          </li>
        </ul>
      </nav>

      <!-- Bottom User Scope Indicator -->
      <div class="sidebar-footer" *ngIf="!navService.isSidebarCollapsed()">
        <div class="user-pill-footer">
          <div class="footer-avatar">
            {{ contextService.currentUser().fullName.slice(0, 2).toUpperCase() }}
          </div>
          <div class="footer-user-meta">
            <strong class="footer-user-name">{{ contextService.userName() }}</strong>
            <span class="footer-branch font-mono">
              {{ contextService.isSuperAdmin() ? '🌐 All Branches' : '🔒 ' + contextService.activeBranch().code }}
            </span>
          </div>
        </div>
      </div>

    </aside>
  `,
  styles: [`
    .sidebar {
      width: 270px;
      background: var(--bg-sidebar);
      border-right: 1px solid var(--border-sidebar);
      display: flex;
      flex-direction: column;
      height: 100vh;
      position: sticky;
      top: 0;
      z-index: 50;
      transition: width 0.25s cubic-bezier(0.4, 0, 0.2, 1);
      box-shadow: 2px 0 12px rgba(0, 0, 0, 0.15);
    }

    .sidebar.collapsed {
      width: 68px;
    }

    /* Back to Portal Action */
    .sidebar-top-action {
      padding: 16px 14px 12px 14px;
      border-bottom: 1px solid var(--border-sidebar);
    }

    .back-to-portal-btn {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 12px;
      background: rgba(14, 165, 233, 0.12);
      border: 1px solid rgba(14, 165, 233, 0.3);
      border-radius: var(--radius-lg);
      color: #38bdf8;
      text-decoration: none;
      transition: all 0.2s ease;
    }
    .back-to-portal-btn:hover {
      background: #0284c7;
      color: #ffffff;
      transform: translateX(-2px);
      box-shadow: 0 4px 12px rgba(2, 132, 199, 0.3);
    }

    .portal-grid-icon {
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .back-text {
      display: flex;
      flex-direction: column;
      line-height: 1.25;
    }

    .back-title {
      font-size: 0.85rem;
      font-weight: 800;
    }

    .back-subtitle {
      font-size: 0.68rem;
      opacity: 0.8;
    }

    /* Active Module Banner */
    .active-module-banner {
      padding: 14px 14px 10px 14px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    }

    .module-banner-pill {
      background: rgba(255, 255, 255, 0.04);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: var(--radius-md);
      padding: 10px 12px;
      display: flex;
      align-items: center;
      gap: 10px;
    }

    .module-banner-icon {
      width: 32px;
      height: 32px;
      border-radius: 8px;
      background: rgba(14, 165, 233, 0.2);
      color: #38bdf8;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .module-banner-info {
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
    }

    .module-ref-tag {
      font-size: 0.65rem;
      font-weight: 700;
      color: var(--vat-teal);
      text-transform: uppercase;
      letter-spacing: 0.4px;
    }

    .module-header-title {
      font-size: 0.82rem;
      font-weight: 800;
      color: #ffffff;
      margin: 0;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    /* Nav Container */
    .nav-container {
      flex: 1;
      padding: 16px 10px;
      overflow-y: auto;
    }

    .menu-label-heading {
      font-size: 0.68rem;
      font-weight: 800;
      color: var(--text-dim);
      text-transform: uppercase;
      letter-spacing: 0.6px;
      padding: 0 10px 8px 10px;
    }

    .nav-list {
      list-style: none;
      padding: 0;
      margin: 0;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .nav-link {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 14px;
      border-radius: var(--radius-md);
      color: var(--text-muted);
      text-decoration: none;
      font-size: 0.86rem;
      font-weight: 600;
      transition: all 0.15s ease;
      position: relative;
    }

    .nav-link:hover {
      background: var(--bg-sidebar-hover);
      color: var(--text-main);
      transform: translateX(2px);
    }

    .nav-link.active {
      background: linear-gradient(90deg, rgba(14, 165, 233, 0.2) 0%, rgba(14, 165, 233, 0.05) 100%);
      color: #38bdf8;
      border-left: 3px solid #0ea5e9;
      font-weight: 750;
    }

    .nav-icon-wrapper {
      width: 20px;
      height: 20px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .nav-label {
      flex: 1;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      line-height: 1.35;
    }

    /* Footer */
    .sidebar-footer {
      padding: 14px;
      border-top: 1px solid var(--border-sidebar);
      background: rgba(0, 0, 0, 0.15);
    }

    .user-pill-footer {
      display: flex;
      align-items: center;
      gap: 10px;
    }

    .footer-avatar {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      background: var(--vat-teal);
      color: #ffffff;
      font-size: 0.75rem;
      font-weight: 800;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .footer-user-meta {
      display: flex;
      flex-direction: column;
      line-height: 1.25;
      min-width: 0;
    }

    .footer-user-name {
      font-size: 0.8rem;
      color: var(--text-main);
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .footer-branch {
      font-size: 0.7rem;
      color: #38bdf8;
    }
  `]
})
export class SidebarComponent {
  public navService = inject(NavigationService);
  public contextService = inject(CompanyContextService);
  private portalService = inject(PortalService);
  private router = inject(Router);

  // Track active URL reactively
  private currentUrl = toSignal(
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map((e: any) => e.urlAfterRedirects || e.url)
    ),
    { initialValue: this.router.url }
  );

  // Detect active module strictly for current URL
  public activeModule = computed(() => {
    const url = this.currentUrl() || this.router.url;
    return this.portalService.getModuleForUrl(url);
  });

  // Card-wise submenus matching the active module (permission-filtered)
  public activeSubmenus = computed(() => {
    this.portalService.matrixVersion();
    this.contextService.currentUser();
    const mod = this.activeModule();
    if (!mod) return [];
    return this.portalService.getAuthorizedSubmenus(mod.code);
  });
}
