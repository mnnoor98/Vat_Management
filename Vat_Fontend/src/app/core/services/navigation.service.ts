import { Injectable, signal, computed, inject } from '@angular/core';
import { MenuItem } from '../models/vat-models';
import { MockDataService } from './mock-data.service';
import { CompanyContextService } from './company-context.service';

@Injectable({
  providedIn: 'root'
})
export class NavigationService {
  private mockData = inject(MockDataService);
  private context = inject(CompanyContextService);

  public readonly menuTree = signal<MenuItem[]>([]);
  public readonly isSidebarCollapsed = signal<boolean>(false);
  public readonly searchQuery = signal<string>('');

  constructor() {
    this.menuTree.set(this.mockData.menuItems);
  }

  /**
   * Permission-filtered menu tree based on current user role and privileges
   */
  public readonly authorizedMenuTree = computed(() => {
    const role = this.context.userRole();
    const isSuper = this.context.isSuperAdmin();
    const all = this.menuTree();

    if (isSuper) {
      return all;
    }

    return all.filter(item => {
      // Administration is restricted to SuperAdmin
      if (item.code === 'MENU_ADMIN') return false;

      // Role specific filtering
      if (role === 'Maker') {
        if (item.code === 'MENU_COMPLIANCE' || item.code === 'MENU_WORKFLOW') return false;
      }
      if (role === 'Checker') {
        if (item.code === 'MENU_MANUFACTURING' || item.code === 'MENU_COMPLIANCE') return false;
      }
      if (role === 'Auditor') {
        if (item.code === 'MENU_MANUFACTURING' || item.code === 'MENU_WORKFLOW') return false;
      }
      return true;
    });
  });

  public toggleSidebar(): void {
    this.isSidebarCollapsed.update(val => !val);
  }

  public filterMenu(query: string): void {
    this.searchQuery.set(query.trim().toLowerCase());
  }

  public updateBadgeCount(menuCode: string, delta: number): void {
    this.menuTree.update(items => {
      return items.map(item => {
        if (item.code === menuCode) {
          return { ...item, badgeCount: Math.max(0, (item.badgeCount || 0) + delta) };
        }
        return item;
      });
    });
  }

  public setBadgeCount(menuCode: string, count: number): void {
    this.menuTree.update(items => {
      return items.map(item => {
        if (item.code === menuCode) {
          return { ...item, badgeCount: Math.max(0, count) };
        }
        return item;
      });
    });
  }
}
