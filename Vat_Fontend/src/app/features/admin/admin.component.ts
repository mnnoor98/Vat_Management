import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CompanyContextService } from '../../core/services/company-context.service';
import { MockDataService } from '../../core/services/mock-data.service';
import { AuthService, UserAccount, RegisterUserRequest } from '../../core/services/auth.service';
import { PortalService, ModuleCard, RoleMenuConfig } from '../../core/services/portal.service';
import { ToastService } from '../../core/services/toast.service';
import { EnterpriseRole } from '../../core/models/vat-models';
import { PurchaseApiService } from '../../core/services/api/purchase-api.service';
import { SalesApiService } from '../../core/services/api/sales-api.service';
import { InventoryApiService } from '../../core/services/api/inventory-api.service';
import { CsvExportService } from '../../core/services/csv-export.service';

interface RoleDefinition {
  id: EnterpriseRole;
  titleBn: string;
  titleEn: string;
  descBn: string;
}

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="admin-page">
      <!-- Page Top Header -->
      <div class="page-header">
        <div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'প্রশাসন, সিকিউরিটি ও আরবেসি গভর্ন্যান্স' : 'Administration & RBAC Governance' }}
          </h1>
          <p class="page-subtitle">
            {{ context.locale() === 'bn' 
              ? 'ব্যবহারকারীর একাধিক রোল ও মডিউল নির্ধারণ, রোলভিত্তিক মেনু সেটিং এবং এনবিআর অডিট ট্রেইল' 
              : 'Multi-role user assignments, role-wise menu settings, and NBR statutory audit trail' }}
          </p>
        </div>

        <div class="header-actions">
          <div class="tab-pill-box">
            <button class="tab-btn" [class.active-tab]="activeTab === 'users'" (click)="activeTab = 'users'">
              {{ context.locale() === 'bn' ? 'ব্যবহারকারী ও একাধিক রোল' : 'Users & Multiple Roles' }}
              <span class="tab-badge">{{ users.length }}</span>
            </button>
            <button class="tab-btn" [class.active-tab]="activeTab === 'rbac'" (click)="activeTab = 'rbac'">
              {{ context.locale() === 'bn' ? 'রোলভিত্তিক মেনু সেটিং' : 'Role-Wise Menu Settings' }}
            </button>
            <button class="tab-btn" [class.active-tab]="activeTab === 'company'" (click)="activeTab = 'company'">
              {{ context.locale() === 'bn' ? 'কোম্পানি পরিচিতি' : 'Company Profile' }}
            </button>
            <button class="tab-btn" [class.active-tab]="activeTab === 'audit'" (click)="activeTab = 'audit'">
              {{ context.locale() === 'bn' ? 'অডিট ট্রেইল' : 'NBR Audit Trail' }}
            </button>
          </div>
        </div>
      </div>

      <!-- ========================================================= -->
      <!-- TAB 1: USERS & MULTI-ROLE ASSIGNMENTS (ব্যবহারকারী ও মাল্টি-রোল) -->
      <!-- ========================================================= -->
      <div *ngIf="activeTab === 'users'" class="vat-card animate-fade-in">
        <div class="section-top-row">
          <div>
            <h2 class="form-title font-bengali">
              ব্যবহারকারী তালিকা ও মাল্টি-রোল ব্যবস্থাপনা (Users & Multi-Role Governance)
            </h2>
            <p class="section-desc">
              প্রতিটি কর্মকর্তাকে একক বা একাধিক রোল (যেমন: Maker + Checker) বরাদ্দ করা যায়। ব্যবহারকারী তার সকল রোলের সম্মিলিত মেনু ও অনুমোদিত মডিউলসমূহ ব্যবহার করতে পারেন।
            </p>
          </div>
          <button class="btn btn-primary" (click)="openNewUserModal()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="12" y1="5" x2="12" y2="19"></line>
              <line x1="5" y1="12" x2="19" y2="12"></line>
            </svg>
            {{ context.locale() === 'bn' ? '+ নতুন ব্যবহারকারী যোগ করুন' : '+ Add New User' }}
          </button>
        </div>

        <div class="vat-table-container">
          <table class="vat-table">
            <thead>
              <tr>
                <th class="text-center">SL</th>
                <th>কর্মকর্তা ও ইউজারনেম (User Details)</th>
                <th>বরাদ্দকৃত রোলসমূহ (Assigned Roles - Multiple)</th>
                <th>শাখা (Branch)</th>
                <th>অনুমোদিত মডিউলসমূহ (Assigned Modules)</th>
                <th>মোবাইল ও ইমেইল</th>
                <th>অবস্থা (Status)</th>
                <th class="text-center">কার্যক্রম (Actions)</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let u of users; let i = index">
                <td class="text-center font-mono">{{ i + 1 }}</td>
                <td>
                  <div class="user-cell">
                    <div class="user-avatar" [ngClass]="getRoleBadgeClass(getUserRoles(u)[0])">
                      {{ u.fullName.slice(0, 2).toUpperCase() }}
                    </div>
                    <div>
                      <strong class="user-fullname">{{ u.fullName }}</strong>
                      <div class="user-fullname-bn font-bengali">{{ u.fullNameBn }}</div>
                      <span class="user-uname-sub font-mono text-emerald">&#64;{{ u.username }}</span>
                    </div>
                  </div>
                </td>
                <td>
                  <!-- Multi-Role Badges -->
                  <div class="user-roles-cell">
                    <span *ngFor="let r of getUserRoles(u)" class="badge" [ngClass]="getRoleBadgeClass(r)">
                      {{ r }}
                    </span>
                  </div>
                </td>
                <td>
                  <div class="branch-tag font-mono">
                    {{ isSuperAdminUser(u) ? '🌐 All Branches (সকল শাখা উন্মুক্ত)' : '🔒 ' + (u.branchName || 'Dhaka Central Plant') }}
                  </div>
                </td>
                <td>
                  <div class="module-chips-wrap">
                    <ng-container *ngIf="isSuperAdminUser(u) || (u.assignedModuleCodes && u.assignedModuleCodes.length >= 9)">
                      <span class="mod-pill mod-pill-all">★ সকল ৯টি মডিউল (All 9 Modules)</span>
                    </ng-container>
                    <ng-container *ngIf="!isSuperAdminUser(u) && (!u.assignedModuleCodes || u.assignedModuleCodes.length < 9)">
                      <span *ngFor="let mCode of u.assignedModuleCodes" class="mod-pill" [ngClass]="getModulePillClass(mCode)">
                        {{ getModuleShortLabel(mCode) }}
                      </span>
                      <span *ngIf="!u.assignedModuleCodes || u.assignedModuleCodes.length === 0" class="text-dim text-xs">
                        সম্মিলিত রোলের ডিফল্ট
                      </span>
                    </ng-container>
                  </div>
                </td>
                <td>
                  <div class="email-text">{{ u.email }}</div>
                  <div class="phone-text font-mono font-dim">{{ u.phone || '+880 1700-000000' }}</div>
                </td>
                <td>
                  <span class="status-chip" [class.status-active]="u.isActive" [class.status-inactive]="!u.isActive">
                    {{ u.isActive ? (context.locale() === 'bn' ? 'সক্রিয়' : 'Active') : (context.locale() === 'bn' ? 'নিষ্ক্রিয়' : 'Inactive') }}
                  </span>
                </td>
                <td class="text-center">
                  <div class="action-btn-group">
                    <!-- Edit User & Multi-Role Modal -->
                    <button class="btn-icon text-sky" title="Edit User, Roles & Modules (একাধিক রোল ও মডিউল সম্পাদন)" (click)="openEditUserModal(u)">
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
                        <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
                      </svg>
                    </button>
                    <!-- Toggle Active / Inactive -->
                    <button 
                      class="btn-icon" 
                      [title]="u.isActive ? 'Deactivate User' : 'Activate User'"
                      (click)="toggleStatus(u)"
                    >
                      <svg *ngIf="u.isActive" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="10"></circle>
                        <line x1="4.93" y1="4.93" x2="19.07" y2="19.07"></line>
                      </svg>
                      <svg *ngIf="!u.isActive" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                        <polyline points="22 4 12 14.01 9 11.01"></polyline>
                      </svg>
                    </button>
                    <!-- Reset Password -->
                    <button class="btn-icon" title="Reset Password (vat123456)" (click)="resetPassword(u)">
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                        <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
                      </svg>
                    </button>
                    <!-- Delete User -->
                    <button 
                      class="btn-icon text-rose" 
                      title="Delete User"
                      [disabled]="u.id === authService.currentUser()?.id"
                      (click)="deleteUser(u)"
                    >
                      <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="3 6 5 6 21 6"></polyline>
                        <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path>
                      </svg>
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- ======================================================== -->
      <!-- TAB 2: ROLE-WISE MENU SETTING (রোলভিত্তিক মেনু সেটিং) -->
      <!-- ======================================================== -->
      <div *ngIf="activeTab === 'rbac'" class="vat-card animate-fade-in">
        <div class="section-top-row">
          <div>
            <h2 class="form-title font-bengali">
              রোলভিত্তিক মেনু সেটিং ও কার্যক্রম কনফিগারেশন (Role-Wise Menu Settings)
            </h2>
            <p class="section-desc">
              প্রতিটি রোলের (Maker, Checker, TaxManager ইত্যাদি) জন্য অনুমোদিত মূসক মডিউল ও সাবমেনুসমূহ কনফিগার করুন। এখানে কোনো পরিবর্তন সংরক্ষণ করলে সেই রোলের সকল কর্মকর্তার সাইডবার ও পোর্টাল মেনুতে তা অবিলম্বে প্রতিফলিত হবে।
            </p>
          </div>
          
          <div class="rbac-save-toolbar">
            <button class="btn btn-secondary" (click)="resetToDefaultsForRole()">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"></path>
                <path d="M3 3v5h5"></path>
              </svg>
              ডিফল্ট রিসেট (Reset Defaults)
            </button>
            <button class="btn btn-primary" (click)="saveRbacMatrix()" [disabled]="isSavingRbac">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
                <polyline points="17 21 17 13 7 13 7 21"></polyline>
                <polyline points="7 3 7 8 15 8"></polyline>
              </svg>
              {{ isSavingRbac ? 'সংরক্ষণ হচ্ছে...' : 'মেনু সেটিং সংরক্ষণ করুন (Save Menu Settings)' }}
            </button>
          </div>
        </div>

        <!-- Role Selector Tab Strip -->
        <div class="role-selector-strip">
          <button 
            *ngFor="let r of rbacRoles"
            class="role-select-btn"
            [class.active-role-btn]="selectedRbacRole === r"
            (click)="selectRbacRole(r)"
          >
            <span class="role-indicator-dot" [ngClass]="getRoleBadgeClass(r)"></span>
            <strong>{{ r }}</strong>
            <span class="role-stat-counter" *ngIf="selectedRbacRole === r">
              {{ currentRoleConfig.allowedModuleCodes.length }} মডিউল সক্রিয়
            </span>
          </button>
        </div>

        <!-- Role Scope Summary Banner -->
        <div class="role-info-banner">
          <div class="role-info-left">
            <span class="badge" [ngClass]="getRoleBadgeClass(selectedRbacRole)">{{ selectedRbacRole }}</span>
            <div class="role-desc-text">
              <strong>{{ getRoleDescription(selectedRbacRole).title }}</strong>
              <p>{{ getRoleDescription(selectedRbacRole).details }}</p>
            </div>
          </div>
          <div class="role-quick-actions">
            <button class="btn-sm-action" (click)="selectAllForRole()">✓ সকল মডিউল ও মেনু নির্বাচন</button>
            <button class="btn-sm-action text-rose" (click)="clearAllForRole()">✕ সম্পূর্ণ খালি করুন</button>
          </div>
        </div>

        <!-- Card-Wise Modules and Submenus List -->
        <div class="rbac-modules-grid">
          <div 
            *ngFor="let mod of portalService.modules" 
            class="rbac-module-card"
            [class.mod-card-enabled]="isModuleEnabledForRole(mod.code)"
          >
            <!-- Module Master Header Checkbox -->
            <div class="rbac-mod-header">
              <label class="checkbox-container main-mod-label">
                <input 
                  type="checkbox" 
                  [checked]="isModuleEnabledForRole(mod.code)" 
                  (change)="toggleModuleForRole(mod.code)"
                />
                <span class="mod-title-box">
                  <strong class="mod-name-bn font-bengali">{{ mod.titleBn }}</strong>
                  <span class="mod-name-en">{{ mod.titleEn }}</span>
                </span>
              </label>
              <div class="mod-header-right">
                <span class="mod-ref-badge">{{ mod.statutoryRef }}</span>
                <span class="sub-counter-badge">
                  {{ getEnabledSubmenuCountForModule(mod) }}/{{ mod.submenus.length }} সাবমেনু
                </span>
              </div>
            </div>

            <!-- Submenus Checklist -->
            <div class="rbac-submenus-list" *ngIf="isModuleEnabledForRole(mod.code)">
              <div class="submenus-heading-row">
                <span class="submenus-heading">
                  {{ context.locale() === 'bn' ? 'অনুমোদিত সাবমেনু ও কার্যক্রমসমূহ:' : 'Permitted Submenus & Actions:' }}
                </span>
                <div class="sub-toggle-quick">
                  <button type="button" class="btn-sub-quick" (click)="enableAllSubmenusForModule(mod)">সব সিলেক্ট</button>
                  <button type="button" class="btn-sub-quick text-rose" (click)="disableAllSubmenusForModule(mod)">সব আনচেক</button>
                </div>
              </div>

              <div 
                *ngFor="let sub of mod.submenus" 
                class="submenu-checkbox-row"
                [class.sub-checked]="isSubmenuEnabledForRole(sub.code)"
              >
                <label class="checkbox-container sub-label">
                  <input 
                    type="checkbox" 
                    [checked]="isSubmenuEnabledForRole(sub.code)" 
                    (change)="toggleSubmenuForRole(mod.code, sub.code)"
                  />
                  <span class="sub-label-wrap">
                    <strong class="font-bengali">{{ sub.labelBn }}</strong>
                    <span class="sub-label-en">{{ sub.labelEn }}</span>
                  </span>
                </label>

                <div class="sub-meta-tags">
                  <span class="route-tag font-mono">{{ sub.route }}</span>
                  <span class="perm-tag font-mono" *ngIf="sub.requiredPermission">{{ sub.requiredPermission }}</span>
                </div>
              </div>
            </div>

            <div class="mod-disabled-notice" *ngIf="!isModuleEnabledForRole(mod.code)">
              <span>🔒 রোল '{{ selectedRbacRole }}' এর জন্য এই মডিউলটি নিষ্ক্রিয় করা আছে</span>
            </div>
          </div>
        </div>
      </div>

      <!-- ========================================== -->
      <!-- TAB 3: COMPANY & TAXPAYER PROFILE -->
      <!-- ========================================== -->
      <div *ngIf="activeTab === 'company'" class="vat-card animate-fade-in">
        <h2 class="form-title">Registered Taxpayer Details</h2>
        <div class="form-grid">
          <div class="form-group">
            <label class="vat-label">Company Legal Name (English)</label>
            <input type="text" class="vat-input" [value]="context.activeCompany().name" readonly />
          </div>
          <div class="form-group">
            <label class="vat-label">Company Name (বাংলা)</label>
            <input type="text" class="vat-input font-bengali" [value]="context.activeCompany().nameBn" readonly />
          </div>
          <div class="form-group">
            <label class="vat-label">NBR 13-Digit BIN</label>
            <input type="text" class="vat-input font-mono" [value]="context.activeCompany().bin" readonly />
          </div>
          <div class="form-group">
            <label class="vat-label">e-TIN</label>
            <input type="text" class="vat-input font-mono" [value]="context.activeCompany().tin" readonly />
          </div>
          <div class="form-group">
            <label class="vat-label">VAT Circle</label>
            <input type="text" class="vat-input" [value]="context.activeCompany().vatCircle" readonly />
          </div>
          <div class="form-group">
            <label class="vat-label">VAT Commissionerate</label>
            <input type="text" class="vat-input" [value]="context.activeCompany().vatCommissioner" readonly />
          </div>
        </div>
      </div>

      <!-- ========================================== -->
      <!-- TAB 4: AUDIT TRAIL LOGS -->
      <!-- ========================================== -->
      <div *ngIf="activeTab === 'audit'" class="vat-card animate-fade-in">
        <div class="card-toolbar" style="display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; margin-bottom: 16px; flex-wrap: wrap;">
          <div>
            <h2 class="form-title" style="margin-bottom: 4px;">NBR Statutory Activity & Security Audit Trail (বিধি ১১২)</h2>
            <p class="text-dim" style="font-size: 0.8rem; margin: 0;">
              Immutable electronic logs of all transaction postings, approvals, goods movements, and access authorizations.
            </p>
          </div>
          <div class="header-actions" style="display: flex; gap: 10px; align-items: center;">
            <button class="btn btn-secondary btn-sm" (click)="exportAuditTrailToCsv()" title="Export Audit Trail (.csv)">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
              Export Audit Trail (.csv)
            </button>
            <button class="btn btn-primary btn-sm" (click)="loadLiveAuditLogs()">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="23 4 23 10 17 10"></polyline><polyline points="1 20 1 14 7 14"></polyline><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path></svg>
              Refresh
            </button>
          </div>
        </div>

        <div class="filters-row" style="display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center;">
          <div class="search-box" style="flex: 1; max-width: 400px; display: flex; align-items: center; gap: 8px; background: var(--bg-card-subtle); border: 1px solid var(--border-card); border-radius: var(--radius-md); padding: 6px 12px;">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
            <input 
              type="text" 
              class="search-input" 
              [(ngModel)]="auditSearchQuery" 
              placeholder="Filter by User, Entity Ref, Action..." 
              style="background: transparent; border: none; color: var(--text-main); font-size: 0.85rem; outline: none; width: 100%;"
            />
          </div>

          <div style="display: flex; align-items: center; gap: 6px;">
            <label class="vat-label-inline font-bengali" style="font-size: 0.8rem; color: var(--text-muted); font-weight: 600;">অ্যাকশন ক্যাটাগরি:</label>
            <select class="vat-select select-compact" [(ngModel)]="auditCategoryFilter" style="width: auto; padding: 6px 12px; font-size: 0.8rem;">
              <option value="ALL">সকল অ্যাকশন (All Activities)</option>
              <option value="PURCHASE">ক্রয় ও দাখিলা (Purchases & 6.1)</option>
              <option value="SALES">বিক্রয় ও চালান (Sales & 6.3)</option>
              <option value="TRANSFER">শাখা স্থানান্তর (Mushak 6.5)</option>
              <option value="COMPLIANCE">রিটার্ন ও সমন্বয় (Mushak 9.1)</option>
              <option value="SECURITY">সিকিউরিটি ও ব্যবহারকারী (RBAC & Auth)</option>
            </select>
          </div>

          <div class="summary-kpis" style="margin-left: auto; display: flex; align-items: center; gap: 10px;">
            <span class="kpi-pill" style="font-size: 0.8rem; background: var(--bg-card-subtle); padding: 6px 12px; border-radius: var(--radius-sm); border: 1px solid var(--border-card); color: var(--text-muted);">
              Total Events: <strong>{{ filteredAuditLogs.length }}</strong>
            </span>
          </div>
        </div>

        <div class="vat-table-container">
          <table class="vat-table">
            <thead>
              <tr>
                <th>Timestamp</th>
                <th>Operator / User</th>
                <th>Role</th>
                <th>Action Scope</th>
                <th>Entity Reference</th>
                <th>IP Address</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let a of filteredAuditLogs">
                <td class="font-mono text-dim">{{ a.timestamp }}</td>
                <td><strong>{{ a.userName }}</strong></td>
                <td><span class="role-badge" [class]="getRoleBadgeClass(a.role)">{{ a.role }}</span></td>
                <td>{{ a.action }}</td>
                <td><strong class="font-mono text-emerald">{{ a.entityRef }}</strong></td>
                <td class="font-mono text-dim">{{ a.ipAddress }}</td>
                <td><span class="status-badge-ok">{{ a.status }}</span></td>
              </tr>
              <tr *ngIf="filteredAuditLogs.length === 0">
                <td colspan="7" class="text-center py-4 text-dim">No audit records match the filter criteria.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- ===================================================================== -->
    <!-- USER ADD / EDIT MODAL (একক ব্যবহারকারীকে একাধিক রোল ও মডিউল বরাদ্দকরণ) -->
    <!-- ===================================================================== -->
    <div *ngIf="showUserModal" class="modal-backdrop animate-fade-in" (click)="closeUserModal()">
      <div class="modal-dialog user-modal-lg" (click)="$event.stopPropagation()">
        <div class="modal-header">
          <div>
            <h3 class="modal-title font-bengali">
              {{ isEditMode ? 'ব্যবহারকারীর তথ্য, একাধিক রোল ও মডিউল সম্পাদন' : 'নতুন ব্যবহারকারী যোগ ও একাধিক রোল বরাদ্দ' }}
            </h3>
            <span class="modal-subtitle">
              {{ isEditMode ? 'Edit profile, multiple assigned roles, branch mapping & module access' : 'Create new officer with multiple role privileges and custom module scope' }}
            </span>
          </div>
          <button class="modal-close-btn" (click)="closeUserModal()">&times;</button>
        </div>

        <form (ngSubmit)="submitUserForm()" class="modal-form">
          <!-- User Details Form Grid -->
          <div class="modal-grid">
            <div class="form-group">
              <label class="field-label">Full Name (English) *</label>
              <input type="text" [(ngModel)]="userData.fullName" name="fullName" required class="vat-input" placeholder="e.g. Asif Mahmud, ACA" />
            </div>
            <div class="form-group">
              <label class="field-label">নাম (বাংলায়)</label>
              <input type="text" [(ngModel)]="userData.fullNameBn" name="fullNameBn" class="vat-input font-bengali" placeholder="যেমন: আসিফ মাহমুদ" />
            </div>
            <div class="form-group">
              <label class="field-label">Username *</label>
              <input type="text" [(ngModel)]="userData.username" name="username" [disabled]="isEditMode" required class="vat-input font-mono" placeholder="e.g. asif.vat" />
            </div>
            <div class="form-group">
              <label class="field-label">Official Email *</label>
              <input type="email" [(ngModel)]="userData.email" name="email" required class="vat-input" placeholder="asif@padmagroup.com.bd" />
            </div>
            <div class="form-group">
              <label class="field-label">Phone / Mobile</label>
              <input type="text" [(ngModel)]="userData.phone" name="phone" class="vat-input font-mono" placeholder="+880 1700-000000" />
            </div>
            <div class="form-group">
              <label class="field-label">{{ isEditMode ? 'Password (পরিবর্তন করতে চাইলে লিখুন)' : 'Password *' }}</label>
              <input type="password" [(ngModel)]="userData.password" name="password" [required]="!isEditMode" class="vat-input" placeholder="••••••••" />
            </div>
          </div>

          <!-- MULTIPLE ROLES ASSIGNMENT SECTION (একক ব্যবহারকারীকে একাধিক রোল) -->
          <div class="multi-role-section">
            <div class="roles-label-row">
              <div>
                <label class="field-label-bold font-bengali">
                  কর্মকর্তার ভূমিকা / রোলসমূহ (Assigned Roles - একাধিক রোল নির্বাচনযোগ্য) *
                </label>
                <p class="section-subtext">একক ব্যবহারকারীকে একাধিক রোল দেওয়া যাবে। কর্মকর্তা নির্বাচিত সকল রোলের সম্মিলিত মেনু ও অনুমতি পাবেন।</p>
              </div>
              <div class="role-quick-presets">
                <button type="button" class="btn-role-preset" (click)="setPresetRoles(['Maker'])">Maker</button>
                <button type="button" class="btn-role-preset" (click)="setPresetRoles(['Maker', 'Checker'])">Maker + Checker</button>
                <button type="button" class="btn-role-preset" (click)="setPresetRoles(['Checker', 'Approver'])">Checker + Approver</button>
                <button type="button" class="btn-role-preset" (click)="setPresetRoles(['TaxManager', 'Approver'])">TaxManager + Approver</button>
              </div>
            </div>

            <!-- Role Checkbox Cards Grid -->
            <div class="multi-role-selector-grid">
              <div 
                *ngFor="let r of allAvailableRoles" 
                class="role-option-card"
                [class.role-card-active]="isRoleSelectedInModal(r.id)"
                (click)="toggleRoleInModal(r.id)"
              >
                <div class="role-check-col">
                  <input 
                    type="checkbox" 
                    [checked]="isRoleSelectedInModal(r.id)" 
                    (click)="$event.stopPropagation()"
                    (change)="toggleRoleInModal(r.id)"
                  />
                </div>
                <div class="role-info-col">
                  <div class="role-title-row">
                    <span class="badge" [ngClass]="getRoleBadgeClass(r.id)">{{ r.id }}</span>
                    <strong class="role-bn-name font-bengali">{{ r.titleBn }}</strong>
                  </div>
                  <p class="role-scope-p">{{ r.descBn }}</p>
                </div>
              </div>
            </div>
          </div>

          <!-- Branch Binding Section -->
          <div class="branch-assign-section" *ngIf="!userData.roles.includes('SuperAdmin')">
            <label class="field-label-bold font-bengali">কর্মকর্তার শাখা নির্ধারণ (Assigned Branch) *</label>
            <select [(ngModel)]="userData.branchId" name="branchId" class="vat-select">
              <option value="33333333-3333-3333-3333-333333333331">DHK-CENTRAL (Dhaka Central Plant & HQ)</option>
              <option value="33333333-3333-3333-3333-333333333332">CTG-DEPOT (Chittagong Port Depot)</option>
              <option value="33333333-3333-3333-3333-333333333333">BGR-DEPOT (Bogura Regional Warehouse)</option>
            </select>
            <small class="field-help text-emerald">🔒 সুপার অ্যাডমিন ব্যতীত বাকি সকল ইউজারের কার্যক্রম নির্দিষ্ট শাখায় সীমাবদ্ধ থাকবে।</small>
          </div>
          <div class="branch-assign-section" *ngIf="userData.roles.includes('SuperAdmin')">
            <label class="field-label-bold font-bengali">Branch Access Scope</label>
            <input type="text" class="vat-input font-bold text-emerald" value="🌐 All Branches & Central HQ (সুপার অ্যাডমিনের জন্য সকল শাখা উন্মুক্ত)" readonly />
          </div>

          <!-- USER-WISE MODULE ASSIGNMENTS SECTION -->
          <div class="user-modules-section">
            <div class="section-banner-row">
              <div>
                <strong class="section-heading font-bengali">অনুমোদিত মূসক মডিউলসমূহ (User-Wise Module Access):</strong>
                <p class="section-subtext">ব্যবহারকারী লগইন করার পর পোর্টাল স্ক্রিনে শুধুমাত্র এই টিকচিহ্নিত মডিউল কার্ডসমূহ দেখতে পাবেন।</p>
              </div>
              <div class="quick-sel-buttons">
                <button type="button" class="btn-xs" (click)="selectAllUserModules()">✓ সকল নির্বাচন</button>
                <button type="button" class="btn-xs" (click)="deselectAllUserModules()">✕ সম্পূর্ণ খালি</button>
                <button type="button" class="btn-xs btn-xs-emerald" (click)="loadRoleDefaultModules()">⟲ রোলের ডিফল্ট লোড</button>
              </div>
            </div>

            <div class="user-mod-checklist-grid">
              <div 
                *ngFor="let mod of portalService.modules" 
                class="user-mod-check-card"
                [class.mod-selected]="isModuleAssignedToUser(mod.code)"
                (click)="toggleUserModule(mod.code)"
              >
                <div class="mod-check-box">
                  <input 
                    type="checkbox" 
                    [checked]="isModuleAssignedToUser(mod.code)" 
                    (click)="$event.stopPropagation()"
                    (change)="toggleUserModule(mod.code)"
                  />
                </div>
                <div class="mod-check-info">
                  <div class="mod-title-bn font-bengali">{{ mod.titleBn }}</div>
                  <div class="mod-title-en">{{ mod.titleEn }}</div>
                  <span class="mod-ref-tag">{{ mod.statutoryRef }}</span>
                </div>
              </div>
            </div>
          </div>

          <!-- Modal Action Buttons -->
          <div class="modal-footer">
            <button type="button" class="btn btn-secondary" (click)="closeUserModal()">বাতিল (Cancel)</button>
            <button type="submit" class="btn btn-primary" [disabled]="isSavingUser">
              {{ isSavingUser ? 'সংরক্ষণ হচ্ছে...' : (isEditMode ? 'আপডেট সংরক্ষণ করুন' : 'নতুন ব্যবহারকারী তৈরি করুন') }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .admin-page { display: flex; flex-direction: column; gap: 20px; }
    .page-header { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 16px; }
    .page-title { font-size: 1.5rem; font-weight: 800; }
    .page-subtitle { font-size: 0.85rem; color: var(--text-muted); }
    .tab-pill-box { display: flex; gap: 4px; background: var(--bg-card); padding: 4px; border-radius: var(--radius-md); border: 1px solid var(--border-card); }
    .tab-btn { background: transparent; border: none; color: var(--text-muted); padding: 7px 15px; border-radius: var(--radius-sm); font-size: 0.8rem; font-weight: 700; cursor: pointer; display: flex; align-items: center; gap: 6px; }
    .active-tab { background: var(--vat-emerald); color: #ffffff; }
    .tab-badge { background: rgba(0,0,0,0.25); color: #ffffff; padding: 1px 7px; border-radius: 10px; font-size: 0.72rem; }
    .form-title { font-size: 1.05rem; font-weight: 700; margin-bottom: 4px; }
    .section-top-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 18px; flex-wrap: wrap; gap: 14px; }
    .section-desc { font-size: 0.82rem; color: var(--text-muted); margin: 0; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; }
    
    /* User Table Styles */
    .user-cell { display: flex; align-items: center; gap: 10px; }
    .user-avatar { width: 36px; height: 36px; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-weight: 800; font-size: 0.75rem; color: #ffffff; flex-shrink: 0; }
    .user-fullname { display: block; font-size: 0.88rem; }
    .user-fullname-bn { font-size: 0.75rem; color: var(--text-muted); }
    .user-uname-sub { font-size: 0.72rem; }
    .user-roles-cell { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; max-width: 180px; }
    .branch-tag { font-size: 0.75rem; color: #38bdf8; }
    .font-dim { color: var(--text-dim); }
    .email-text { font-size: 0.82rem; }
    .phone-text { font-size: 0.72rem; }

    /* Module Chips */
    .module-chips-wrap { display: flex; flex-wrap: wrap; gap: 4px; max-width: 320px; }
    .mod-pill { font-size: 0.7rem; font-weight: 700; padding: 2px 7px; border-radius: 4px; background: rgba(56, 189, 248, 0.12); color: #38bdf8; border: 1px solid rgba(56, 189, 248, 0.25); white-space: nowrap; }
    .mod-pill-all { background: rgba(16, 185, 129, 0.15); color: #10b981; border-color: rgba(16, 185, 129, 0.3); font-weight: 800; }
    .mod-cyan { background: rgba(6, 182, 212, 0.12); color: #06b6d4; }
    .mod-blue { background: rgba(37, 99, 235, 0.12); color: #60a5fa; }
    .mod-indigo { background: rgba(99, 102, 241, 0.12); color: #818cf8; }
    .mod-amber { background: rgba(245, 158, 11, 0.12); color: #fbbf24; }
    .mod-emerald { background: rgba(16, 185, 129, 0.12); color: #34d399; }
    .mod-violet { background: rgba(139, 92, 246, 0.12); color: #a78bfa; }
    .mod-rose { background: rgba(244, 63, 94, 0.12); color: #fb7185; }
    .mod-slate { background: rgba(100, 116, 139, 0.12); color: #94a3b8; }

    .status-chip { font-size: 0.72rem; font-weight: 700; padding: 3px 8px; border-radius: 9999px; }
    .status-active { background: rgba(16, 185, 129, 0.15); color: #10b981; }
    .status-inactive { background: rgba(239, 68, 68, 0.15); color: #ef4444; }

    .action-btn-group { display: flex; gap: 4px; justify-content: center; }
    .btn-icon { background: transparent; border: 1px solid var(--border-subtle); color: var(--text-muted); width: 30px; height: 30px; border-radius: 6px; cursor: pointer; display: flex; align-items: center; justify-content: center; transition: all 0.15s; }
    .btn-icon:hover { background: var(--bg-card-hover); color: var(--text-main); }
    .btn-icon:disabled { opacity: 0.4; cursor: not-allowed; }
    .text-sky { color: #38bdf8; }

    .role-superadmin { background: #e11d48; }
    .role-taxmanager { background: #059669; }
    .role-maker { background: #0284c7; }
    .role-checker { background: #d97706; }
    .role-approver { background: #6366f1; }
    .role-auditor { background: #7c3aed; }

    /* ========================================== */
    /* ROLE-WISE MENU RBAC STYLES */
    /* ========================================== */
    .rbac-save-toolbar { display: flex; gap: 10px; align-items: center; }
    .role-selector-strip { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 16px; padding: 6px; background: rgba(0,0,0,0.2); border-radius: var(--radius-md); }
    .role-select-btn { display: flex; align-items: center; gap: 8px; background: var(--bg-card); border: 1px solid var(--border-card); color: var(--text-main); padding: 8px 16px; border-radius: var(--radius-sm); font-size: 0.85rem; cursor: pointer; transition: all 0.15s; }
    .role-select-btn:hover { border-color: #38bdf8; }
    .active-role-btn { background: #0284c7; color: #ffffff; border-color: #0284c7; font-weight: 700; }
    .role-indicator-dot { width: 10px; height: 10px; border-radius: 50%; display: inline-block; }
    .role-stat-counter { background: rgba(0,0,0,0.3); padding: 2px 7px; border-radius: 10px; font-size: 0.72rem; }

    .role-info-banner { display: flex; justify-content: space-between; align-items: center; background: rgba(14, 165, 233, 0.08); border: 1px solid rgba(14, 165, 233, 0.25); border-radius: var(--radius-md); padding: 12px 18px; margin-bottom: 20px; flex-wrap: wrap; gap: 12px; }
    .role-info-left { display: flex; align-items: center; gap: 14px; }
    .role-desc-text strong { display: block; font-size: 0.88rem; color: #38bdf8; }
    .role-desc-text p { margin: 2px 0 0 0; font-size: 0.78rem; color: var(--text-muted); }
    .role-quick-actions { display: flex; gap: 8px; }
    .btn-sm-action { background: rgba(255,255,255,0.06); border: 1px solid var(--border-card); color: var(--text-main); font-size: 0.75rem; font-weight: 600; padding: 5px 12px; border-radius: 6px; cursor: pointer; }
    .btn-sm-action:hover { background: rgba(255,255,255,0.12); }

    .rbac-modules-grid { display: flex; flex-direction: column; gap: 14px; }
    .rbac-module-card { background: var(--bg-card); border: 1px solid var(--border-card); border-radius: var(--radius-md); overflow: hidden; transition: all 0.2s; }
    .mod-card-enabled { border-color: rgba(14, 165, 233, 0.4); box-shadow: 0 4px 14px rgba(0,0,0,0.15); }
    .rbac-mod-header { display: flex; justify-content: space-between; align-items: center; padding: 14px 18px; background: rgba(255,255,255,0.02); border-bottom: 1px solid var(--border-card); }
    .main-mod-label { display: flex; align-items: center; gap: 12px; cursor: pointer; }
    .mod-title-box { display: flex; flex-direction: column; }
    .mod-name-bn { font-size: 0.95rem; font-weight: 700; color: var(--text-main); }
    .mod-name-en { font-size: 0.78rem; color: var(--text-muted); }
    .mod-header-right { display: flex; align-items: center; gap: 8px; }
    .mod-ref-badge { font-size: 0.72rem; color: #38bdf8; background: rgba(56, 189, 248, 0.1); padding: 3px 8px; border-radius: 4px; font-weight: 600; }
    .sub-counter-badge { font-size: 0.72rem; color: #10b981; background: rgba(16, 185, 129, 0.12); padding: 3px 8px; border-radius: 4px; font-weight: 700; }

    .rbac-submenus-list { padding: 14px 18px; display: flex; flex-direction: column; gap: 10px; background: rgba(0,0,0,0.1); }
    .submenus-heading-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: 4px; }
    .submenus-heading { font-size: 0.75rem; font-weight: 700; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.5px; }
    .sub-toggle-quick { display: flex; gap: 6px; }
    .btn-sub-quick { background: transparent; border: 1px solid var(--border-card); color: var(--text-main); font-size: 0.7rem; padding: 2px 8px; border-radius: 4px; cursor: pointer; }
    .btn-sub-quick:hover { background: rgba(255,255,255,0.1); }
    .submenu-checkbox-row { display: flex; justify-content: space-between; align-items: center; padding: 8px 12px; border-radius: 6px; background: var(--bg-card); border: 1px solid var(--border-card); transition: all 0.15s; }
    .submenu-checkbox-row:hover { border-color: #38bdf8; }
    .sub-checked { background: rgba(14, 165, 233, 0.05); border-color: rgba(14, 165, 233, 0.3); }
    .sub-label { display: flex; align-items: center; gap: 10px; cursor: pointer; flex: 1; }
    .sub-label-wrap { display: flex; align-items: center; gap: 8px; }
    .sub-label-wrap strong { font-size: 0.85rem; }
    .sub-label-en { font-size: 0.75rem; color: var(--text-muted); }
    .sub-meta-tags { display: flex; gap: 6px; align-items: center; }
    .route-tag { font-size: 0.7rem; color: #94a3b8; background: rgba(0,0,0,0.25); padding: 2px 6px; border-radius: 4px; }
    .perm-tag { font-size: 0.7rem; color: #60a5fa; background: rgba(96, 165, 250, 0.15); padding: 2px 6px; border-radius: 4px; }
    .mod-disabled-notice { padding: 12px 18px; font-size: 0.8rem; color: var(--text-dim); }

    /* Custom Checkboxes */
    .checkbox-container { position: relative; display: flex; align-items: center; user-select: none; }
    .checkbox-container input { width: 18px; height: 18px; margin: 0; cursor: pointer; accent-color: var(--vat-emerald); }

    /* ========================================== */
    /* USER MODAL STYLES (MULTI-ROLE) */
    /* ========================================== */
    .modal-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.75); backdrop-filter: blur(5px); display: flex; align-items: center; justify-content: center; z-index: 1000; padding: 20px; }
    .modal-dialog { background: var(--bg-card); border: 1px solid var(--border-card); border-radius: var(--radius-lg); width: 100%; box-shadow: 0 24px 48px rgba(0,0,0,0.6); overflow: hidden; max-height: 90vh; display: flex; flex-direction: column; }
    .user-modal-lg { max-width: 820px; }
    .modal-header { display: flex; justify-content: space-between; align-items: flex-start; padding: 18px 24px; border-bottom: 1px solid var(--border-card); background: rgba(255,255,255,0.02); }
    .modal-title { font-size: 1.15rem; font-weight: 800; margin: 0; }
    .modal-subtitle { font-size: 0.75rem; color: var(--text-muted); }
    .modal-close-btn { background: none; border: none; font-size: 1.5rem; color: var(--text-muted); cursor: pointer; }
    .modal-form { padding: 22px; display: flex; flex-direction: column; gap: 18px; overflow-y: auto; }
    .modal-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; }
    .field-label { font-size: 0.8rem; font-weight: 600; color: var(--text-muted); margin-bottom: 4px; display: block; }
    .field-label-bold { font-size: 0.88rem; font-weight: 700; color: #38bdf8; margin-bottom: 4px; display: block; }
    .vat-select { width: 100%; background-color: var(--bg-input, #0f172a); border: 1px solid var(--border-card, #334155); border-radius: var(--radius-sm, 6px); padding: 8px 36px 8px 12px; color: var(--text-main, #f8fafc); font-size: 0.85rem; appearance: none; -webkit-appearance: none; background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 24 24' fill='none' stroke='%2334d399' stroke-width='2.2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpolyline points='6 9 12 15 18 9'%3E%3C/polyline%3E%3C/svg%3E"); background-repeat: no-repeat; background-position: calc(100% - 10px) center; background-size: 14px 14px; cursor: pointer; color-scheme: dark; }
    .vat-select option { background-color: #1e293b; color: #f8fafc; padding: 8px 12px; }

    /* Multi-Role Selector Box */
    .multi-role-section { border-top: 1px solid var(--border-card); padding-top: 14px; display: flex; flex-direction: column; gap: 12px; }
    .roles-label-row { display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 8px; }
    .role-quick-presets { display: flex; gap: 5px; flex-wrap: wrap; }
    .btn-role-preset { background: rgba(255,255,255,0.06); border: 1px solid var(--border-card); color: var(--text-muted); font-size: 0.72rem; font-weight: 600; padding: 3px 8px; border-radius: 4px; cursor: pointer; }
    .btn-role-preset:hover { background: rgba(255,255,255,0.12); color: #ffffff; }

    .multi-role-selector-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
    .role-option-card { display: flex; align-items: flex-start; gap: 10px; padding: 10px 12px; background: rgba(0,0,0,0.25); border: 1px solid var(--border-card); border-radius: var(--radius-sm); cursor: pointer; transition: all 0.15s; }
    .role-option-card:hover { border-color: #38bdf8; }
    .role-card-active { background: rgba(14, 165, 233, 0.1); border-color: #38bdf8; box-shadow: 0 0 10px rgba(56, 189, 248, 0.15); }
    .role-check-col input { margin-top: 3px; cursor: pointer; accent-color: var(--vat-emerald); }
    .role-info-col { flex: 1; }
    .role-title-row { display: flex; align-items: center; gap: 8px; margin-bottom: 2px; }
    .role-bn-name { font-size: 0.82rem; font-weight: 700; }
    .role-scope-p { margin: 0; font-size: 0.72rem; color: var(--text-muted); line-height: 1.25; }

    .branch-assign-section { border-top: 1px solid var(--border-card); padding-top: 12px; }

    /* User Module Checklist in Modal */
    .user-modules-section { border-top: 1px solid var(--border-card); padding-top: 14px; display: flex; flex-direction: column; gap: 12px; }
    .section-banner-row { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 10px; }
    .section-heading { font-size: 0.9rem; color: #38bdf8; display: block; }
    .section-subtext { font-size: 0.75rem; color: var(--text-muted); margin: 2px 0 0 0; }
    .quick-sel-buttons { display: flex; gap: 6px; }
    .btn-xs { background: rgba(255,255,255,0.06); border: 1px solid var(--border-card); color: var(--text-main); font-size: 0.72rem; font-weight: 600; padding: 3px 8px; border-radius: 4px; cursor: pointer; }
    .btn-xs:hover { background: rgba(255,255,255,0.12); }
    .btn-xs-emerald { background: rgba(16, 185, 129, 0.15); color: #10b981; border-color: rgba(16, 185, 129, 0.3); }

    .user-mod-checklist-grid { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 10px; max-height: 220px; overflow-y: auto; padding-right: 4px; }
    .user-mod-check-card { display: flex; align-items: flex-start; gap: 10px; padding: 10px; background: rgba(0,0,0,0.25); border: 1px solid var(--border-card); border-radius: var(--radius-sm); cursor: pointer; transition: all 0.15s; }
    .user-mod-check-card:hover { border-color: #38bdf8; }
    .mod-selected { background: rgba(14, 165, 233, 0.08); border-color: #38bdf8; }
    .mod-check-box input { margin-top: 2px; cursor: pointer; accent-color: var(--vat-emerald); }
    .mod-check-info { flex: 1; min-width: 0; }
    .mod-title-bn { font-size: 0.8rem; font-weight: 700; line-height: 1.25; margin-bottom: 2px; }
    .mod-title-en { font-size: 0.68rem; color: var(--text-muted); line-height: 1.2; margin-bottom: 4px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .mod-ref-tag { font-size: 0.65rem; color: #38bdf8; background: rgba(56, 189, 248, 0.1); padding: 1px 5px; border-radius: 3px; display: inline-block; }

    .modal-footer { display: flex; justify-content: flex-end; gap: 10px; margin-top: 10px; border-top: 1px solid var(--border-card); padding-top: 14px; }

    .perm-pill { background: rgba(59, 130, 246, 0.15); color: #60a5fa; font-size: 0.72rem; padding: 2px 6px; border-radius: var(--radius-sm); font-family: var(--font-mono); }
    .role-badge { background: rgba(139, 92, 246, 0.15); color: #a78bfa; font-size: 0.72rem; font-weight: 700; padding: 2px 8px; border-radius: var(--radius-sm); }
    .status-badge-ok { background: rgba(16, 185, 129, 0.15); color: #34d399; font-size: 0.72rem; font-weight: 700; padding: 2px 8px; border-radius: 9999px; }

    @media (max-width: 768px) {
      .modal-grid { grid-template-columns: 1fr; }
      .multi-role-selector-grid { grid-template-columns: 1fr; }
      .user-mod-checklist-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class AdminComponent implements OnInit {
  public authService = inject(AuthService);
  public portalService = inject(PortalService);
  public context = inject(CompanyContextService);
  public mockData = inject(MockDataService);
  private toast = inject(ToastService);
  private purchaseApi = inject(PurchaseApiService);
  private salesApi = inject(SalesApiService);
  private inventoryApi = inject(InventoryApiService);
  private csvExport = inject(CsvExportService);

  public activeTab: 'users' | 'rbac' | 'company' | 'audit' = 'users';
  public users: UserAccount[] = [];

  // ==========================================
  // AVAILABLE ROLES DEFINITION
  // ==========================================
  public readonly allAvailableRoles: RoleDefinition[] = [
    {
      id: 'Maker',
      titleBn: 'মেকার (প্রস্তুতকারী)',
      titleEn: 'Maker (Data Entry)',
      descBn: 'স্থানীয় ও আমদানি ক্রয় চালান এন্ট্রি, মূসক ৬.৩ বিক্রয় কর চালান তৈরি ও গুদাম মজুদ স্থানান্তর।'
    },
    {
      id: 'Checker',
      titleBn: 'চেকার (যাচাইকারী)',
      titleEn: 'Checker (Verification)',
      descBn: 'মেকার কর্তৃক প্রেরিত ক্রয় ও বিক্রয় চালানের গাণিতিক শুদ্ধতা ও কর হার যাচাইকরণ।'
    },
    {
      id: 'Approver',
      titleBn: 'অনুমোদনকারী (বিওএম ও সহগ)',
      titleEn: 'Approver (Authorization)',
      descBn: 'উৎপাদন সহগ ঘোষণা (মূসক ৪.৩) ও বিশেষায়িত ক্রয়-বিক্রয় অনুমোদন।'
    },
    {
      id: 'TaxManager',
      titleBn: 'ট্যাক্স ম্যানেজার (কর কর্তৃপক্ষ)',
      titleEn: 'Tax Manager (Rebate & 9.1)',
      descBn: 'ধারা ৪৬ রেয়াত অনুমোদন, লেজার অটো-পোস্টিং, মূসক ৯.১ মাসিক রিটার্ন দাখিল।'
    },
    {
      id: 'Auditor',
      titleBn: 'নিরীক্ষক (বিধি ১১২ অডিট)',
      titleEn: 'Auditor (Audit & Ledgers)',
      descBn: 'বিধি ১১২ অনুযায়ী অডিট ট্রেইল, লেজার ও সংবিধিবদ্ধ খতিয়ান পরীক্ষণ (Read-Only)।'
    },
    {
      id: 'SuperAdmin',
      titleBn: 'সুপার অ্যাডমিন (পূর্ণ নিয়ন্ত্রণ)',
      titleEn: 'Super Admin (Enterprise)',
      descBn: 'সকল মডিউল, আরবেসি পলিসি নির্ধারণ, ইউজার অ্যাকাউন্ট ও শাখা ম্যাপিং।'
    }
  ];

  // ==========================================
  // USER MODAL STATE (MULTI-ROLE)
  // ==========================================
  public showUserModal: boolean = false;
  public isEditMode: boolean = false;
  public editingUserId: string | null = null;
  public isSavingUser: boolean = false;

  public userData: {
    username: string;
    fullName: string;
    fullNameBn: string;
    email: string;
    phone: string;
    password: string;
    role: EnterpriseRole;
    roles: EnterpriseRole[];
    companyId: string;
    branchId: string;
    assignedModuleCodes: string[];
  } = {
    username: '',
    fullName: '',
    fullNameBn: '',
    email: '',
    phone: '',
    password: '',
    role: 'Maker',
    roles: ['Maker'],
    companyId: '11111111-1111-1111-1111-111111111111',
    branchId: '33333333-3333-3333-3333-333333333331',
    assignedModuleCodes: []
  };

  // ==========================================
  // ROLE-WISE MENU RBAC STATE
  // ==========================================
  public selectedRbacRole: EnterpriseRole = 'Maker';
  public readonly rbacRoles: EnterpriseRole[] = ['Maker', 'Checker', 'Approver', 'TaxManager', 'Auditor', 'SuperAdmin'];
  public currentRoleConfig: RoleMenuConfig = {
    role: 'Maker',
    allowedModuleCodes: [],
    allowedSubmenuCodes: []
  };
  public isSavingRbac: boolean = false;

  public auditSearchQuery: string = '';
  public auditCategoryFilter: string = 'ALL';
  public liveAuditLogs: any[] = [];

  public readonly baseAuditLogs = [
    { timestamp: '2026-10-23 11:20:15', userName: 'Tariqul Islam, FCA', role: 'TaxManager', action: 'Approved Purchase Invoice with Section 46 Rebate', entityRef: 'PUR-1024', category: 'PURCHASE', ipAddress: '192.168.10.45', status: 'Success' },
    { timestamp: '2026-10-23 10:45:00', userName: 'Rafiqul Islam', role: 'Maker', action: 'Issued Mushak 6.3 Tax Invoice with QR Code', entityRef: 'CH-DHK-2026-00142', category: 'SALES', ipAddress: '192.168.10.62', status: 'Success' },
    { timestamp: '2026-10-22 17:15:30', userName: 'Nusrat Jahan', role: 'Maker', action: 'Created Mushak 6.5 Central Goods Transfer', entityRef: 'TRF-6.5-2026-0034', category: 'TRANSFER', ipAddress: '192.168.10.88', status: 'Success' },
    { timestamp: '2026-10-22 14:00:22', userName: 'Engr. Mahbubur Rahman', role: 'Approver', action: 'Submitted Mushak 4.3 Input-Output BOM Declaration', entityRef: 'DEC-4.3/2026/04', category: 'COMPLIANCE', ipAddress: '192.168.10.99', status: 'Success' },
    { timestamp: '2026-10-21 09:30:10', userName: 'Tariqul Islam, FCA', role: 'TaxManager', action: 'Reconciled Part 3 & 4 of Mushak 9.1 Monthly Return', entityRef: 'RET-2026-10', category: 'COMPLIANCE', ipAddress: '192.168.10.45', status: 'Success' },
    { timestamp: '2026-10-20 08:15:00', userName: 'System Security Admin', role: 'SuperAdmin', action: 'Assigned Multi-Role Privileges & Submenu Scope', entityRef: 'USR-8805', category: 'SECURITY', ipAddress: '127.0.0.1', status: 'Success' }
  ];

  ngOnInit(): void {
    this.loadUsers();
    this.selectRbacRole(this.selectedRbacRole);
    this.loadLiveAuditLogs();
  }

  public loadLiveAuditLogs(): void {
    const list: any[] = [...this.baseAuditLogs];

    // Merge live transfers
    this.inventoryApi.getTransfers().subscribe({
      next: (trfs) => {
        trfs.forEach(t => {
          list.push({
            timestamp: t.createdAt ? t.createdAt.replace('T', ' ').slice(0, 19) : `${t.dispatchDate} ${t.dispatchTime || '14:00:00'}`,
            userName: t.createdBy || 'Tariqul Islam, FCA',
            role: 'TaxManager',
            action: `Issued Mushak 6.5 Central Goods Transfer (${t.fromBranch || t.fromWarehouseCode} ➔ ${t.toBranch || t.toWarehouseCode})`,
            entityRef: t.transferNumber,
            category: 'TRANSFER',
            ipAddress: '192.168.10.45',
            status: t.status || 'Posted'
          });
        });
        this.liveAuditLogs = this.sortAuditLogs(list);
      },
      error: () => {
        this.liveAuditLogs = this.sortAuditLogs(list);
      }
    });

    // Merge live purchases
    this.purchaseApi.getInvoices().subscribe({
      next: (purchases) => {
        purchases.filter(p => p.status === 'Posted' || p.status === 'Approved').forEach(p => {
          list.push({
            timestamp: p.createdAt ? p.createdAt.replace('T', ' ').slice(0, 19) : `${p.invoiceDate} 11:30:00`,
            userName: p.approvedBy || p.createdBy || 'Tariqul Islam, FCA',
            role: 'TaxManager',
            action: `Approved & Posted Purchase Invoice with Section 46 Rebate (৳${p.grandTotal})`,
            entityRef: p.supplierInvoiceNumber || p.invoiceNumber,
            category: 'PURCHASE',
            ipAddress: '192.168.10.45',
            status: 'Posted'
          });
        });
        this.liveAuditLogs = this.sortAuditLogs(list);
      }
    });

    // Merge live sales
    this.salesApi.getInvoices().subscribe({
      next: (sales) => {
        sales.filter(s => s.status === 'Posted' || s.status === 'Approved').forEach(s => {
          list.push({
            timestamp: s.createdAt ? s.createdAt.replace('T', ' ').slice(0, 19) : `${s.deliveryDate || '2026-10-23'} 10:45:00`,
            userName: s.createdBy || 'Rafiqul Islam',
            role: 'Maker',
            action: `Issued & Posted Mushak 6.3 Tax Invoice with QR Code (৳${s.grandTotal})`,
            entityRef: s.challanNumber63 || s.invoiceNumber,
            category: 'SALES',
            ipAddress: '192.168.10.62',
            status: 'Posted'
          });
        });
        this.liveAuditLogs = this.sortAuditLogs(list);
      }
    });
  }

  private sortAuditLogs(logs: any[]): any[] {
    const seen = new Set<string>();
    const unique = logs.filter(item => {
      const key = `${item.entityRef}-${item.action}`;
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    });
    return unique.sort((a, b) => (b.timestamp || '').localeCompare(a.timestamp || ''));
  }

  get filteredAuditLogs(): any[] {
    let list = this.liveAuditLogs.length > 0 ? this.liveAuditLogs : this.baseAuditLogs;
    if (this.auditCategoryFilter !== 'ALL') {
      list = list.filter(a => a.category === this.auditCategoryFilter);
    }
    if (!this.auditSearchQuery) return list;
    const q = this.auditSearchQuery.toLowerCase();
    return list.filter(a =>
      a.userName?.toLowerCase().includes(q) ||
      a.entityRef?.toLowerCase().includes(q) ||
      a.action?.toLowerCase().includes(q) ||
      a.role?.toLowerCase().includes(q) ||
      a.ipAddress?.toLowerCase().includes(q)
    );
  }

  public exportAuditTrailToCsv(): void {
    const headers = [
      'Timestamp',
      'Operator / User',
      'Role',
      'Action Scope',
      'Entity Reference',
      'Category',
      'IP Address',
      'Status'
    ];

    const rows = this.filteredAuditLogs.map(a => [
      a.timestamp,
      a.userName,
      a.role,
      a.action,
      a.entityRef,
      a.category,
      a.ipAddress,
      a.status
    ]);

    const compId = this.context.activeCompany().id;
    const filename = `NBR_Statutory_Audit_Trail_Rule_112_${compId}_${new Date().toISOString().slice(0, 10)}`;
    this.csvExport.exportToCsv(filename, headers, rows);
  }


  public loadUsers(): void {
    this.users = this.authService.getUsers();
  }

  public getUserRoles(u: UserAccount): EnterpriseRole[] {
    if (u.roles && u.roles.length > 0) {
      return u.roles;
    }
    return [u.role || 'Maker'];
  }

  public isSuperAdminUser(u: UserAccount): boolean {
    const roles = this.getUserRoles(u);
    return roles.includes('SuperAdmin');
  }

  // ==========================================
  // USER MODAL ACTIONS (MULTI-ROLE)
  // ==========================================
  public openNewUserModal(): void {
    this.isEditMode = false;
    this.editingUserId = null;
    this.userData = {
      username: '',
      fullName: '',
      fullNameBn: '',
      email: '',
      phone: '',
      password: '',
      role: 'Maker',
      roles: ['Maker'],
      companyId: '11111111-1111-1111-1111-111111111111',
      branchId: '33333333-3333-3333-3333-333333333331',
      assignedModuleCodes: [...this.authService.getDefaultModulesForRoles(['Maker'])]
    };
    this.showUserModal = true;
  }

  public openEditUserModal(u: UserAccount): void {
    this.isEditMode = true;
    this.editingUserId = u.id;
    const userRoles = this.getUserRoles(u);
    this.userData = {
      username: u.username,
      fullName: u.fullName,
      fullNameBn: u.fullNameBn || '',
      email: u.email,
      phone: u.phone || '',
      password: '',
      role: userRoles[0] || 'Maker',
      roles: [...userRoles],
      companyId: u.companyId,
      branchId: u.branchId,
      assignedModuleCodes: u.assignedModuleCodes && u.assignedModuleCodes.length > 0 
        ? [...u.assignedModuleCodes] 
        : [...this.authService.getDefaultModulesForRoles(userRoles)]
    };
    this.showUserModal = true;
  }

  public closeUserModal(): void {
    this.showUserModal = false;
  }

  public isRoleSelectedInModal(role: EnterpriseRole): boolean {
    return this.userData.roles.includes(role);
  }

  public toggleRoleInModal(role: EnterpriseRole): void {
    const idx = this.userData.roles.indexOf(role);
    if (idx > -1) {
      if (this.userData.roles.length === 1) {
        this.toast.showWarning('কমপক্ষে একটি রোল নির্বাচন করতে হবে!');
        return;
      }
      this.userData.roles.splice(idx, 1);
    } else {
      this.userData.roles.push(role);
    }
    this.userData.role = this.userData.roles[0];
    // Automatically load merged default modules for the selected roles
    this.loadRoleDefaultModules();
  }

  public setPresetRoles(roles: EnterpriseRole[]): void {
    this.userData.roles = [...roles];
    this.userData.role = roles[0];
    this.loadRoleDefaultModules();
  }

  public loadRoleDefaultModules(): void {
    this.userData.assignedModuleCodes = [...this.authService.getDefaultModulesForRoles(this.userData.roles)];
  }

  public isModuleAssignedToUser(modCode: string): boolean {
    return this.userData.assignedModuleCodes.includes(modCode);
  }

  public toggleUserModule(modCode: string): void {
    const idx = this.userData.assignedModuleCodes.indexOf(modCode);
    if (idx > -1) {
      this.userData.assignedModuleCodes.splice(idx, 1);
    } else {
      this.userData.assignedModuleCodes.push(modCode);
    }
  }

  public selectAllUserModules(): void {
    this.userData.assignedModuleCodes = this.portalService.modules.map(m => m.code);
  }

  public deselectAllUserModules(): void {
    this.userData.assignedModuleCodes = [];
  }

  public submitUserForm(): void {
    if (!this.userData.fullName || !this.userData.email) {
      this.toast.showError('অনুগ্রহ করে নাম এবং ইমেইল পূরণ করুন।');
      return;
    }

    if (this.userData.roles.length === 0) {
      this.toast.showError('অনুগ্রহ করে অন্তত একটি রোল নির্বাচন করুন।');
      return;
    }

    if (!this.isEditMode && (!this.userData.username || !this.userData.password)) {
      this.toast.showError('নতুন ব্যবহারকারীর জন্য ইউজারনেম এবং পাসওয়ার্ড আবশ্যক।');
      return;
    }

    this.isSavingUser = true;

    if (this.isEditMode && this.editingUserId) {
      // Update existing user with multiple roles & modules
      const updates: Partial<UserAccount> = {
        fullName: this.userData.fullName.trim(),
        fullNameBn: this.userData.fullNameBn?.trim() || this.userData.fullName.trim(),
        email: this.userData.email.trim(),
        phone: this.userData.phone?.trim() || '+880 1700-000000',
        role: this.userData.roles[0],
        roles: [...this.userData.roles],
        branchId: this.userData.branchId,
        branchName: this.userData.branchId.includes('33') ? 'Bogura Regional Warehouse' : (this.userData.branchId.includes('32') ? 'Chittagong Port Depot' : 'Dhaka Central Plant & HQ'),
        assignedModuleCodes: [...this.userData.assignedModuleCodes]
      };

      if (this.userData.password && this.userData.password.trim().length >= 6) {
        updates.password = this.userData.password.trim();
      }

      const ok = this.authService.updateUser(this.editingUserId, updates);
      this.isSavingUser = false;
      if (ok) {
        this.showUserModal = false;
        this.loadUsers();
        this.toast.showSuccess(`'${this.userData.fullName}' এর রোল (${this.userData.roles.join(', ')}) ও মডিউল পারমিশন সফলভাবে আপডেট হয়েছে।`);
      } else {
        this.toast.showError('ব্যবহারকারী আপডেট ব্যর্থ হয়েছে।');
      }
    } else {
      // Create new user with multiple roles
      const req: RegisterUserRequest = {
        username: this.userData.username.trim(),
        fullName: this.userData.fullName.trim(),
        fullNameBn: this.userData.fullNameBn?.trim() || this.userData.fullName.trim(),
        email: this.userData.email.trim(),
        phone: this.userData.phone?.trim(),
        password: this.userData.password.trim(),
        role: this.userData.roles[0],
        roles: [...this.userData.roles],
        companyId: this.userData.companyId,
        branchId: this.userData.branchId,
        assignedModuleCodes: [...this.userData.assignedModuleCodes]
      };

      this.authService.register(req).subscribe({
        next: (user) => {
          this.isSavingUser = false;
          this.showUserModal = false;
          this.loadUsers();
          this.toast.showSuccess(`নতুন ব্যবহারকারী '${user.fullName}' (${user.roles?.join(', ')}) সফলভাবে সিস্টেমে যুক্ত হয়েছে।`);
        },
        error: (err) => {
          this.isSavingUser = false;
          this.toast.showError(err.message || 'ব্যবহারকারী তৈরিতে ব্যর্থ হয়েছে।');
        }
      });
    }
  }

  public toggleStatus(u: UserAccount): void {
    const ok = this.authService.toggleUserStatus(u.id);
    if (ok) {
      this.loadUsers();
      this.toast.showInfo(`'${u.username}' এর স্থিতি পরিবর্তন করা হয়েছে।`);
    }
  }

  public resetPassword(u: UserAccount): void {
    const ok = this.authService.resetUserPassword(u.id, 'vat123456');
    if (ok) {
      this.toast.showSuccess(`'${u.username}' এর পাসওয়ার্ড রিসেট করে 'vat123456' করা হয়েছে।`);
    }
  }

  public deleteUser(u: UserAccount): void {
    if (confirm(`আপনি কি নিশ্চিত যে '${u.fullName}' (${u.username}) ব্যবহারকারীকে মুছে ফেলতে চান?`)) {
      const ok = this.authService.deleteUser(u.id);
      if (ok) {
        this.loadUsers();
        this.toast.showWarning(`'${u.fullName}' ব্যবহারকারী মুছে ফেলা হয়েছে।`);
      }
    }
  }

  // ==========================================
  // ROLE-WISE MENU RBAC ACTIONS
  // ==========================================
  public selectRbacRole(role: EnterpriseRole): void {
    this.selectedRbacRole = role;
    const cfg = this.portalService.getRoleMenuConfig(role);
    this.currentRoleConfig = {
      role,
      allowedModuleCodes: [...(cfg.allowedModuleCodes || [])],
      allowedSubmenuCodes: [...(cfg.allowedSubmenuCodes || [])]
    };
  }

  public isModuleEnabledForRole(modCode: string): boolean {
    return this.currentRoleConfig.allowedModuleCodes.includes(modCode);
  }

  public toggleModuleForRole(modCode: string): void {
    const mod = this.portalService.getModuleByCode(modCode);
    const modIdx = this.currentRoleConfig.allowedModuleCodes.indexOf(modCode);

    if (modIdx > -1) {
      // Disable module & remove all its submenus
      this.currentRoleConfig.allowedModuleCodes.splice(modIdx, 1);
      if (mod) {
        const subCodes = mod.submenus.map(s => s.code);
        this.currentRoleConfig.allowedSubmenuCodes = this.currentRoleConfig.allowedSubmenuCodes.filter(c => !subCodes.includes(c));
      }
    } else {
      // Enable module & include all its submenus
      this.currentRoleConfig.allowedModuleCodes.push(modCode);
      if (mod) {
        for (const sub of mod.submenus) {
          if (!this.currentRoleConfig.allowedSubmenuCodes.includes(sub.code)) {
            this.currentRoleConfig.allowedSubmenuCodes.push(sub.code);
          }
        }
      }
    }
  }

  public isSubmenuEnabledForRole(subCode: string): boolean {
    return this.currentRoleConfig.allowedSubmenuCodes.includes(subCode);
  }

  public toggleSubmenuForRole(modCode: string, subCode: string): void {
    const subIdx = this.currentRoleConfig.allowedSubmenuCodes.indexOf(subCode);
    if (subIdx > -1) {
      this.currentRoleConfig.allowedSubmenuCodes.splice(subIdx, 1);
    } else {
      this.currentRoleConfig.allowedSubmenuCodes.push(subCode);
      // Ensure parent module is also enabled
      if (!this.currentRoleConfig.allowedModuleCodes.includes(modCode)) {
        this.currentRoleConfig.allowedModuleCodes.push(modCode);
      }
    }
  }

  public enableAllSubmenusForModule(mod: ModuleCard): void {
    if (!this.currentRoleConfig.allowedModuleCodes.includes(mod.code)) {
      this.currentRoleConfig.allowedModuleCodes.push(mod.code);
    }
    for (const sub of mod.submenus) {
      if (!this.currentRoleConfig.allowedSubmenuCodes.includes(sub.code)) {
        this.currentRoleConfig.allowedSubmenuCodes.push(sub.code);
      }
    }
  }

  public disableAllSubmenusForModule(mod: ModuleCard): void {
    const subCodes = mod.submenus.map(s => s.code);
    this.currentRoleConfig.allowedSubmenuCodes = this.currentRoleConfig.allowedSubmenuCodes.filter(c => !subCodes.includes(c));
  }

  public getEnabledSubmenuCountForModule(mod: ModuleCard): number {
    return mod.submenus.filter(s => this.currentRoleConfig.allowedSubmenuCodes.includes(s.code)).length;
  }

  public selectAllForRole(): void {
    this.currentRoleConfig.allowedModuleCodes = this.portalService.modules.map(m => m.code);
    const allSubs: string[] = [];
    this.portalService.modules.forEach(m => m.submenus.forEach(s => allSubs.push(s.code)));
    this.currentRoleConfig.allowedSubmenuCodes = allSubs;
  }

  public clearAllForRole(): void {
    this.currentRoleConfig.allowedModuleCodes = [];
    this.currentRoleConfig.allowedSubmenuCodes = [];
  }

  public resetToDefaultsForRole(): void {
    const defaults = this.portalService.getDefaultMatrix();
    const def = defaults[this.selectedRbacRole];
    if (def) {
      this.currentRoleConfig = {
        role: this.selectedRbacRole,
        allowedModuleCodes: [...def.allowedModuleCodes],
        allowedSubmenuCodes: [...def.allowedSubmenuCodes]
      };
      this.toast.showInfo(`রোল '${this.selectedRbacRole}' এর ডিফল্ট কনফিগারেশন লোড করা হয়েছে।`);
    }
  }

  public saveRbacMatrix(): void {
    this.isSavingRbac = true;
    this.portalService.saveRoleMenuConfig(
      this.selectedRbacRole,
      this.currentRoleConfig.allowedModuleCodes,
      this.currentRoleConfig.allowedSubmenuCodes
    );
    this.isSavingRbac = false;
    this.toast.showSuccess(`রোল '${this.selectedRbacRole}' এর মেনু সেটিং সফলভাবে সংরক্ষিত ও কার্যকর হয়েছে!`);
  }

  public getRoleDescription(role: EnterpriseRole): { title: string; details: string } {
    const found = this.allAvailableRoles.find(r => r.id === role);
    if (found) {
      return {
        title: `${found.titleBn} (${found.titleEn})`,
        details: found.descBn
      };
    }
    return {
      title: role,
      details: 'এনবিআর আরবেসি গভার্নেন্স।'
    };
  }

  public getRoleBadgeClass(role: string): string {
    switch (role?.toLowerCase()) {
      case 'superadmin': return 'role-superadmin';
      case 'taxmanager': return 'role-taxmanager';
      case 'maker': return 'role-maker';
      case 'checker': return 'role-checker';
      case 'approver': return 'role-approver';
      case 'auditor': return 'role-auditor';
      default: return 'role-maker';
    }
  }

  public getModuleShortLabel(modCode: string): string {
    switch (modCode) {
      case 'MOD_PURCHASES': return 'ক্রয় (৬.১)';
      case 'MOD_SALES': return 'বিক্রয় (৬.৩)';
      case 'MOD_MANUFACTURING': return 'সহগ (৪.৩)';
      case 'MOD_INVENTORY': return 'মজুদ (৬.৫)';
      case 'MOD_COMPLIANCE': return 'রিটার্ন (৯.১)';
      case 'MOD_WORKFLOW': return 'ওয়ার্কফ্লো';
      case 'MOD_MASTERDATA': return 'মাস্টার ডাটা';
      case 'MOD_REPORTS': return 'রিপোর্ট';
      case 'MOD_ADMIN': return 'অ্যাডমিন';
      default: return modCode;
    }
  }

  public getModulePillClass(modCode: string): string {
    switch (modCode) {
      case 'MOD_PURCHASES': return 'mod-cyan';
      case 'MOD_SALES': return 'mod-blue';
      case 'MOD_MANUFACTURING': return 'mod-indigo';
      case 'MOD_INVENTORY': return 'mod-amber';
      case 'MOD_COMPLIANCE': return 'mod-emerald';
      case 'MOD_WORKFLOW': return 'mod-violet';
      case 'MOD_MASTERDATA': return 'mod-cyan';
      case 'MOD_REPORTS': return 'mod-rose';
      case 'MOD_ADMIN': return 'mod-slate';
      default: return '';
    }
  }
}
