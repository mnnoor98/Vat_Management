import { environment } from '../../../environments/environment';
import { Injectable, signal, computed, inject } from '@angular/core';
import { Company, Branch, FiscalPeriod, UserProfile } from '../models/vat-models';
import { MockDataService } from './mock-data.service';

@Injectable({
  providedIn: 'root'
})
export class CompanyContextService {
  private mockData = inject(MockDataService);

  // Signals for state
  public readonly activeCompany = signal<Company>(this.mockData.companies[0] || { id: '', name: 'Select company', nameBn: 'Select company', bin: '', tin: '', address: '', addressBn: '', vatCircle: '', vatDivision: '', vatCommissioner: '', currency: 'BDT', isActive: false });
  public readonly activeBranch = signal<Branch>(this.mockData.branches[0] || { id: '', companyId: '', code: '', name: 'Select branch', nameBn: 'Select branch', binExtension: '', address: '', isHeadOffice: false });
  public readonly activeFiscalPeriod = signal<FiscalPeriod>(this.mockData.fiscalPeriods[0] || { id: '', fiscalYear: '', month: 'Select period', monthBn: 'Select period', periodCode: '', startDate: '', endDate: '', isClosed: true, dueDate: '' });
  public readonly currentUser = signal<UserProfile>(this.mockData.availableUsers[0] || { id: '', username: '', fullName: '', fullNameBn: '', email: '', role: 'Maker', accessibleCompanyIds: [], permissions: [], assignedModuleCodes: [] });
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

  public readonly companyGuid = computed(() => this.activeCompany().id);

  public resolveBranchGuid(branchIdOrCode?: string): string {
    const id = branchIdOrCode || this.activeBranch().id;
    if (/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(id)) return id;
    throw new Error('Select a verified company branch before saving this document.');
  }

  clearVerifiedContext(): void {
    this.mockData.companies.length = 0;
    this.mockData.branches.length = 0;
    this.mockData.fiscalPeriods.length = 0;
    this.activeCompany.set({ id: '', name: 'Select company', nameBn: 'Select company', bin: '', tin: '', address: '', addressBn: '', vatCircle: '', vatDivision: '', vatCommissioner: '', currency: 'BDT', isActive: false });
    this.activeBranch.set({ id: '', companyId: '', code: '', name: 'Select branch', nameBn: 'Select branch', binExtension: '', address: '', isHeadOffice: false });
    this.activeFiscalPeriod.set({ id: '', fiscalYear: '', month: 'Select period', monthBn: 'Select period', periodCode: '', startDate: '', endDate: '', isClosed: true, dueDate: '' });
    this.currentUser.set({ id: '', username: '', fullName: '', fullNameBn: '', email: '', role: 'User', accessibleCompanyIds: [], permissions: [], assignedModuleCodes: [] });
  }

  async loadVerifiedCompanies(token: string, companyIds: string[]): Promise<void> {
    try {
      const fetchedCompanies: typeof this.mockData.companies = [];
      const fetchedBranches: typeof this.mockData.branches = [];

      for (const id of companyIds) {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 1500);
        try {
          const response = await fetch(`${environment.apiGatewayUrl}/companies/${encodeURIComponent(id)}`, {
            headers: { Authorization: `Bearer ${token}` },
            cache: 'no-store',
            signal: controller.signal
          });
          if (!response.ok) continue;
          const value = await response.json() as { id: string; name: string; registrationReference: string };
          fetchedCompanies.push({
            id: value.id, name: value.name, nameBn: value.name,
            bin: value.registrationReference, tin: '', address: '245 Tejgaon I/A, Dhaka-1208', addressBn: '২৪৫ তেজগাঁও শিল্প এলাকা, ঢাকা-১২০৮',
            vatCircle: 'Circle-54', vatDivision: 'Tejgaon', vatCommissioner: 'Dhaka North', currency: 'BDT', isActive: true
          });

          const branches = await fetch(`${environment.apiGatewayUrl}/companies/${id}/branches`, {
            headers: { Authorization: `Bearer ${token}` },
            cache: 'no-store',
            signal: controller.signal
          });
          if (branches.ok) {
            const values = await branches.json() as { id: string; companyId: string; name: string }[];
            fetchedBranches.push(...values.filter(b => b.companyId === id).map((b, idx) => ({
              id: b.id, companyId: id, name: b.name, nameBn: b.name, code: 'ACI-HQ', binExtension: '0101', address: '245 Tejgaon I/A, Dhaka', isHeadOffice: idx === 0
            })));
          }
        } catch {
          // Timeout or fetch error -> keep initial seeded data
        } finally {
          clearTimeout(timeoutId);
        }
      }

      if (fetchedCompanies.length > 0) {
        this.mockData.companies.splice(0, this.mockData.companies.length, ...fetchedCompanies);
      }
      if (fetchedBranches.length > 0) {
        this.mockData.branches.splice(0, this.mockData.branches.length, ...fetchedBranches);
      }
      const first = this.mockData.companies[0];
      if (first) this.switchCompany(first.id);
    } catch {
      // Safe fallback
    }
  }

  public readonly branchGuid = computed(() => {
    return this.resolveBranchGuid(this.activeBranch().id);
  });

  public switchCompany(companyId: string): void {
    // If not super admin, non-super users can only be in their assigned company
    if (this.currentUser().accessibleCompanyIds?.length) {
      if (!this.currentUser().accessibleCompanyIds.includes(companyId)) {
        return;
      }
    }
    const found = this.mockData.companies.find(c => c.id === companyId);
    if (found) {
      this.activeCompany.set(found);
      const firstBranch = this.mockData.branches.find(b => b.companyId === found.id) || { id: '', companyId: found.id, code: '', name: 'Select branch', nameBn: 'Select branch', binExtension: '', address: '', isHeadOffice: false };
      this.activeBranch.set(firstBranch);
    }
  }

  public switchBranch(branchId: string): void {
    const found = this.mockData.branches.find(b => b.id === branchId && b.companyId === this.activeCompany().id);
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
    if (this.currentUser().permissions?.includes('all')) return true;
    return this.currentUser().permissions?.includes(permission) || false;
  }

  public toggleLocale(): void {
    this.locale.update(cur => cur === 'en' ? 'bn' : 'en');
  }
}
