import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router, NavigationEnd } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs/operators';
import { TopbarComponent } from '../topbar/topbar.component';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { ToastContainerComponent } from '../../shared/components/toast-container.component';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterModule, TopbarComponent, SidebarComponent, ToastContainerComponent],
  template: `
    <div class="app-layout">
      <!-- Sidebar is shown ONLY when inside a module, completely hidden on /app/portal -->
      <app-sidebar *ngIf="showSidebar()"></app-sidebar>
      
      <div class="main-wrapper" [class.no-sidebar]="!showSidebar()">
        <app-topbar></app-topbar>
        <main class="page-content animate-fade-in" [class.portal-full-width]="!showSidebar()">
          <router-outlet></router-outlet>
        </main>
      </div>
      <app-toast-container></app-toast-container>
    </div>
  `,
  styles: [`
    .app-layout {
      display: flex;
      min-height: 100vh;
      background: var(--bg-app);
    }

    .main-wrapper {
      flex: 1;
      display: flex;
      flex-direction: column;
      min-width: 0;
      background: var(--bg-app);
      transition: all 0.25s ease;
    }

    .page-content {
      flex: 1;
      padding: 24px 30px 40px 30px;
      max-width: 1600px;
      width: 100%;
      margin: 0 auto;
    }

    .portal-full-width {
      max-width: 1560px;
      padding: 26px 36px 48px 36px;
    }

    @media (max-width: 768px) {
      .page-content, .portal-full-width {
        padding: 16px;
      }
    }
  `]
})
export class AppShellComponent {
  private router = inject(Router);

  // Track active URL reactively
  private currentUrl = toSignal(
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map((e: any) => e.urlAfterRedirects || e.url)
    ),
    { initialValue: this.router.url }
  );

  // Sidebar is hidden on /app/portal, shown inside modules!
  public showSidebar = computed(() => {
    const url = this.currentUrl() || this.router.url;
    return !url.includes('/app/portal');
  });
}
