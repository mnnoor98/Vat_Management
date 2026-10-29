import { Injectable, signal, computed, inject } from '@angular/core';
import { Company, Branch, FiscalPeriod, UserProfile } from '../models/vat-models';
import { MockDataService } from './mock-data.service';

@Injectable({
  providedIn: 'root'
})
export class CompanyContextService {
  private mockData = inject(MockDataService);

  // Signals for state
  public readonly activeCompany = signal<Company>(this.mockData.companies[0]);
  public readonly activeBranch = signal<Branch>(this.mockData.branches[0]);
  public readonly activeFiscalPeriod = signal<FiscalPeriod>(this.mockData.fiscalPeriods[0]);
  public readonly currentUser = signal<UserProfile>(this.mockData.availableUsers[0]);
  public readonly locale = signal<'en' | 'bn'>('en');

  // Computed properties
  public readonly fullBin = computed(() => {
    const comp = this.activeCompany();
    const branch = this.activeBranch();
    return `${comp.bin} (${branch.code})`;
  });

  public readonly companyName = computed(() => {
    return this.locale() === 'bn' ? this.activeCompany().nameBn : this.activeCompany().name;
  });

  public readonly branchName = computed(() => {
    return this.locale() === 'bn' ? this.activeBranch().nameBn : this.activeBranch().name;
  });

  public readonly periodName = computed(() => {
    return this.locale() === 'bn' ? this.activeFiscalPeriod().monthBn : `${this.activeFiscalPeriod().month} ${this.activeFiscalPeriod().fiscalYear}`;
  });

  public readonly userName = computed(() => {
    return this.locale() === 'bn' ? this.currentUser().fullNameBn : this.currentUser().fullName;
  });

  public readonly userRoles = computed(() => {
    const u = this.currentUser();
    return (u.roles && u.roles.length > 0) ? u.roles : [u.role];
  });

  public readonly userRole = computed(() => {
    const roles = this.userRoles();
    return roles.length > 1 ? roles.join(' + ') : (this.currentUser().role || 'Maker');
  });

  public readonly isSuperAdmin = computed(() => {
    return this.userRoles().includes('SuperAdmin');
  });

  public readonly companyGuid = computed(() => {
    const id = this.activeCompany().id;
    if (/^[0-9a-fA-F-]{36}$/.test(id)) return id;
    if (id === 'COMP-002') return '22222222-2222-2222-2222-222222222222';
    return '11111111-1111-1111-1111-111111111111';
  });

  public resolveBranchGuid(branchIdOrCode?: string): string {
    if (!branchIdOrCode) return '33333333-3333-3333-3333-333333333331';
    if (/^[0-9a-fA-F-]{36}$/.test(branchIdOrCode)) return branchIdOrCode;
    if (branchIdOrCode === 'BR-02' || branchIdOrCode.includes('CTG') || branchIdOrCode.includes('32')) {
      return '33333333-3333-3333-3333-333333333332';
    }
    if (branchIdOrCode === 'BR-03' || branchIdOrCode.includes('BGR') || branchIdOrCode.includes('33')) {
      return '33333333-3333-3333-3333-333333333333';
    }
    return '33333333-3333-3333-3333-333333333331';
  }

  public readonly branchGuid = computed(() => {
    return this.resolveBranchGuid(this.activeBranch().id);
  });

  public switchCompany(companyId: string): void {
    // If not super admin, non-super users can only be in their assigned company
    if (!this.isSuperAdmin() && this.currentUser().accessibleCompanyIds?.length) {
      if (!this.currentUser().accessibleCompanyIds.includes(companyId)) {
        return;
      }
    }
    const found = this.mockData.companies.find(c => c.id === companyId);
    if (found) {
      this.activeCompany.set(found);
      const firstBranch = this.mockData.branches.find(b => b.companyId === found.id) || this.mockData.branches[0];
      this.activeBranch.set(firstBranch);
    }
  }

  public switchBranch(branchId: string): void {
    // If not super admin, non-super users are strictly locked to their assigned branch
    if (!this.isSuperAdmin() && this.currentUser().branchId) {
      const allowed = this.currentUser().branchId;
      if (allowed !== branchId && 
          !(allowed?.includes('31') && branchId === 'BR-01') && 
          !(allowed?.includes('32') && branchId === 'BR-02') &&
          !(allowed?.includes('33') && branchId === 'BR-03')) {
        return;
      }
    }
    const found = this.mockData.branches.find(b => b.id === branchId || 
      (branchId.includes('33') ? b.id === 'BR-03' : 
       branchId.includes('32') ? b.id === 'BR-02' : 
       branchId.includes('31') ? b.id === 'BR-01' : false)
    );
    if (found) {
      this.activeBranch.set(found);
    }
  }

  public switchFiscalPeriod(periodId: string): void {
    const found = this.mockData.fiscalPeriods.find(p => p.id === periodId);
    if (found) {
      this.activeFiscalPeriod.set(found);
    }
  }

  public switchUser(userId: string): void {
    const found = this.mockData.availableUsers.find(u => u.id === userId);
    if (found) {
      this.currentUser.set(found);
    }
  }

  public hasPermission(permission: string): boolean {
    if (this.isSuperAdmin() || this.currentUser().permissions?.includes('all')) return true;
    return this.currentUser().permissions?.includes(permission) || false;
  }

  public toggleLocale(): void {
    this.locale.update(cur => cur === 'en' ? 'bn' : 'en');
  }
}
