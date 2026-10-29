import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { CompanyContextService } from '../../core/services/company-context.service';
import { MockDataService } from '../../core/services/mock-data.service';
import { WorkflowApiService } from '../../core/services/api/workflow-api.service';
import { CsvExportService } from '../../core/services/csv-export.service';
import { ToastService } from '../../core/services/toast.service';
import { BdtCurrencyPipe } from '../../shared/pipes/bdt-currency.pipe';
import { ApprovalTask } from '../../core/models/vat-models';

@Component({
  selector: 'app-workflow',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, BdtCurrencyPipe],
  template: `
    <div class="workflow-page">
      <!-- Page Header -->
      <div class="page-header">
        <div>
          <div class="breadcrumb-trail">
            <span>Governance</span> / <span>Workflow</span> / <span>Approval Workbench</span>
          </div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'অনুমোদন ওয়ার্কবেঞ্চ (Maker-Checker)' : 'Universal Approval Workbench' }}
          </h1>
          <p class="page-subtitle">
            {{ context.locale() === 'bn' 
              ? 'এনবিআর ভ্যাট আইন এবং মেকার-চেকার অনুমোদন ম্যাট্রিক্স ও অডিট ট্রেইল' 
              : 'Maker-Checker-Approver authorization matrix and immutable decision audit trail (PostgreSQL Live)' }}
          </p>
        </div>

        <div class="header-actions">
          <button class="btn btn-secondary" (click)="exportToCsv()" title="Export Workflow History (.csv)">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="7 10 12 15 17 10"></polyline><line x1="12" y1="15" x2="12" y2="3"></line></svg>
            Export Audit Trail (.csv)
          </button>
          <div class="branch-filter-box" *ngIf="context.isSuperAdmin()">
            <select class="vat-select select-compact" [(ngModel)]="selectedBranchFilter">
              <option value="All">All Branches (সকল শাখা)</option>
              <option *ngFor="let b of mockData.branches" [value]="b.id">🏢 {{ b.name }}</option>
            </select>
          </div>
          <div class="tab-pill-box">
            <button class="tab-btn" [class.active-tab]="selectedTab === 'Pending'" (click)="selectedTab = 'Pending'">
              Pending Tasks ({{ pendingCount }})
            </button>
            <button class="tab-btn" [class.active-tab]="selectedTab === 'History'" (click)="selectedTab = 'History'">
              Approved History ({{ approvedCount }})
            </button>
          </div>
        </div>
      </div>

      <!-- Live PostgreSQL Status Banner -->
      <div class="db-connection-banner">
        <div class="db-status-pill">
          <span class="pulse-dot"></span>
          <span>
            <strong>PostgreSQL Live Workbench:</strong> 
            {{ pendingCount }} {{ context.locale() === 'bn' ? 'টি অনুমোদনের অপেক্ষমাণ টাস্ক' : 'Pending Tasks' }} & 
            {{ approvedCount }} {{ context.locale() === 'bn' ? 'টি অনুমোদিত চালান' : 'Approved Invoices' }}
          </span>
        </div>
        <div class="db-meta-info font-mono">
          Tenant: 00000000-0000-0000-0000-000000000001 | Active: <strong>{{ context.userName() }}</strong> ({{ context.userRole() }})
        </div>
      </div>

      <!-- Quick Role Simulator Bar -->
      <div class="persona-chips-bar">
        <span class="chips-label">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>
          {{ context.locale() === 'bn' ? 'ভূমিকা সিমুলেটর (RBAC Switcher):' : 'Quick Role Simulator (RBAC):' }}
        </span>
        <button 
          class="chip-btn" 
          [class.active-chip]="context.currentUser().id === 'USR-8803'" 
          (click)="context.switchUser('USR-8803'); onUserSwitched('Nusrat Jahan (Checker)')">
          Nusrat Jahan (Checker)
        </button>
        <button 
          class="chip-btn" 
          [class.active-chip]="context.currentUser().id === 'USR-8804'" 
          (click)="context.switchUser('USR-8804'); onUserSwitched('Engr. Mahbubur Rahman (Approver)')">
          Engr. Mahbubur Rahman (Approver)
        </button>
        <button 
          class="chip-btn" 
          [class.active-chip]="context.currentUser().id === 'USR-8801'" 
          (click)="context.switchUser('USR-8801'); onUserSwitched('Tariqul Islam, FCA (TaxManager)')">
          Tariqul Islam, FCA (TaxManager)
        </button>
        <button 
          class="chip-btn" 
          [class.active-chip]="context.currentUser().id === 'USR-8802'" 
          (click)="context.switchUser('USR-8802'); onUserSwitched('Rafiqul Islam (Maker)')">
          Rafiqul Islam (Maker)
        </button>
      </div>

      <!-- Tasks List Card -->
      <div class="vat-card">
        <div class="vat-table-container">
          <table class="vat-table">
            <thead>
              <tr>
                <th>Document Type</th>
                <th>Branch</th>
                <th>Reference No</th>
                <th>Party / Description</th>
                <th>Requested By</th>
                <th>Requested At</th>
                <th class="text-right">Total Amount</th>
                <th class="text-right">VAT Amount</th>
                <th>Priority</th>
                <th>Status</th>
                <th class="text-right">Decision Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let t of displayedTasks">
                <td><span class="doc-badge">{{ t.documentType }}</span></td>
                <td>
                  <span class="branch-pill">
                    🏢 {{ getBranchName(t.branchId) }}
                  </span>
                </td>
                <td><strong class="font-mono text-emerald">{{ t.documentNumber }}</strong></td>
                <td>
                  <strong>{{ t.partyName }}</strong>
                  <br>
                  <small class="text-dim">{{ t.notes }}</small>
                </td>
                <td>
                  <span class="maker-tag" [class.self-maker]="isMakerOfTask(t)">
                    {{ t.requestedBy }} {{ isMakerOfTask(t) ? '(You)' : '' }}
                  </span>
                </td>
                <td>{{ t.requestedAt }}</td>
                <td class="text-right numeric font-bold">{{ t.amount | bdtCurrency:false }}</td>
                <td class="text-right numeric text-rose">{{ t.vatAmount | bdtCurrency:false }}</td>
                <td>
                  <span class="priority-pill" [class.urgent]="t.priority === 'Urgent'">{{ t.priority }}</span>
                </td>
                <td>
                  <span class="task-status" [ngClass]="t.status.toLowerCase()">{{ t.status }}</span>
                </td>
                <td class="text-right">
                  <div class="action-buttons" *ngIf="t.status === 'Pending'">
                    <!-- Segregation of duties: Maker cannot approve their own submission -->
                    <ng-container *ngIf="isMakerOfTask(t)">
                      <div class="maker-block">
                        <span class="badge-maker" title="Maker cannot approve own document under NBR VAT rules">
                          {{ context.locale() === 'bn' ? 'স্ব-অনুমোদন নিষিদ্ধ' : 'Maker Restricted' }}
                        </span>
                        <button class="btn-switch-checker" (click)="switchToChecker()" title="Switch to Checker (Nusrat Jahan) to approve">
                          {{ context.locale() === 'bn' ? 'চেকার পারসোনায় যান' : 'Switch to Checker' }}
                        </button>
                      </div>
                    </ng-container>

                    <!-- Normal approval action when not self and has permission -->
                    <ng-container *ngIf="!isMakerOfTask(t) && canApprove()">
                      <button class="btn-approve" (click)="openDecisionModal(t, 'Approve')">
                        {{ context.locale() === 'bn' ? 'অনুমোদন' : 'Approve' }}
                      </button>
                      <button class="btn-reject" (click)="openDecisionModal(t, 'Reject')">
                        {{ context.locale() === 'bn' ? 'বাতিল' : 'Reject' }}
                      </button>
                    </ng-container>

                    <span 
                      class="badge-restricted" 
                      *ngIf="!isMakerOfTask(t) && !canApprove()" 
                      [title]="'Approval restricted for ' + context.userRole() + ' role. Only Checker / Approver / TaxManager can approve.'">
                      Read Only ({{ context.userRole() }})
                    </span>
                  </div>
                  <span *ngIf="t.status !== 'Pending'" class="decided-label" [class.approved]="t.status === 'Approved'">
                    {{ t.status === 'Approved' ? '✓ Approved' : '✕ Rejected' }}
                  </span>
                </td>
              </tr>
              <tr *ngIf="displayedTasks.length === 0">
                <td colspan="10" class="empty-state">
                  {{ context.locale() === 'bn' 
                    ? 'এই ট্যাবে কোনো অপেক্ষমাণ টাস্ক নেই। নতুন ক্রয় চালান এন্ট্রি করতে "Purchases > New Purchase Entry" তে যান।' 
                    : 'No approval tasks found in this view. To create new purchases, navigate to Purchases > New Entry.' }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Decision Dialog Modal Backdrop -->
      <div class="modal-backdrop" *ngIf="showModal">
        <div class="modal-card">
          <div class="modal-header">
            <h3>{{ decisionAction === 'Approve' ? 'Confirm Approval (অনুমোদন নিশ্চিতকরণ)' : 'Confirm Rejection (প্রত্যাখ্যান)' }}</h3>
            <button class="btn-close" (click)="showModal = false">✕</button>
          </div>
          <div class="modal-body">
            <p class="modal-instruction">
              Document: <strong>{{ activeTask?.documentNumber }}</strong> ({{ activeTask?.partyName }})
            </p>
            <div class="modal-amount-summary">
              <span>Amount: <strong>{{ (activeTask?.amount || 0) | bdtCurrency }}</strong></span>
              <span class="vat-amt-pill">VAT: {{ (activeTask?.vatAmount || 0) | bdtCurrency }}</span>
            </div>
            <div class="form-group">
              <label class="vat-label">
                {{ decisionAction === 'Approve' ? 'Approval Remarks (ঐচ্ছিক মন্তব্য)' : 'Mandatory Rejection Reason (বাধ্যতামূলক কারণ) *' }}
              </label>
              <textarea
                class="vat-input textarea"
                [(ngModel)]="decisionRemarks"
                rows="3"
                [placeholder]="decisionAction === 'Approve' ? 'e.g. Validated with bank BEFTN/challan and supplier BIN.' : 'e.g. Discrepancy in supplier BIN or Section 46 rebate limit.'"
              ></textarea>
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn btn-secondary" (click)="showModal = false" [disabled]="isSubmittingDecision">Cancel</button>
            <button
              class="btn"
              [class.btn-success]="decisionAction === 'Approve'"
              [class.btn-danger]="decisionAction === 'Reject'"
              (click)="submitDecision()"
              [disabled]="isSubmittingDecision || (decisionAction === 'Reject' && !decisionRemarks.trim())"
            >
              <span *ngIf="isSubmittingDecision" class="spinner-small"></span>
              {{ isSubmittingDecision 
                ? 'Processing...' 
                : (decisionAction === 'Approve' ? 'Confirm Approval' : 'Confirm Rejection') }}
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .workflow-page { display: flex; flex-direction: column; gap: 20px; }
    .page-header { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 16px; }
    .breadcrumb-trail { font-size: 0.78rem; color: var(--text-dim); margin-bottom: 4px; }
    .page-title { font-size: 1.5rem; font-weight: 800; }
    .page-subtitle { font-size: 0.85rem; color: var(--text-muted); }
    .header-actions { display: flex; align-items: center; gap: 12px; }

    .tab-pill-box { display: flex; gap: 4px; background: var(--bg-card); padding: 4px; border-radius: var(--radius-md); border: 1px solid var(--border-card); }
    .tab-btn { background: transparent; border: none; color: var(--text-muted); padding: 6px 14px; border-radius: var(--radius-sm); font-size: 0.78rem; font-weight: 700; cursor: pointer; }
    .active-tab { background: var(--vat-emerald); color: #ffffff; }

    .db-connection-banner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 12px;
      padding: 10px 16px;
      background: rgba(16, 185, 129, 0.08);
      border: 1px solid rgba(16, 185, 129, 0.25);
      border-radius: var(--radius-md);
      font-size: 0.85rem;
    }

    .branch-filter-box select {
      padding: 6px 12px;
      font-size: 0.8rem;
    }

    .branch-pill {
      font-size: 0.72rem;
      font-weight: 700;
      padding: 2px 7px;
      border-radius: var(--radius-sm);
      background: rgba(14, 165, 233, 0.12);
      border: 1px solid rgba(14, 165, 233, 0.25);
      color: #38bdf8;
      display: inline-flex;
      align-items: center;
      gap: 4px;
      white-space: nowrap;
    }

    .db-status-pill {
      display: flex;
      align-items: center;
      gap: 8px;
      color: #34d399;
    }

    .pulse-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background-color: #10b981;
      box-shadow: 0 0 8px #10b981;
      animation: pulse 1.8s infinite;
    }

    @keyframes pulse {
      0% { transform: scale(0.95); opacity: 0.8; }
      50% { transform: scale(1.3); opacity: 1; }
      100% { transform: scale(0.95); opacity: 0.8; }
    }

    .db-meta-info {
      color: var(--text-dim);
      font-size: 0.75rem;
    }

    .persona-chips-bar {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 8px;
      padding: 10px 14px;
      background: var(--bg-card);
      border: 1px solid var(--border-card);
      border-radius: var(--radius-md);
    }

    .chips-label {
      font-size: 0.78rem;
      font-weight: 700;
      color: var(--text-muted);
      display: flex;
      align-items: center;
      gap: 6px;
      margin-right: 6px;
    }

    .chip-btn {
      background: rgba(255, 255, 255, 0.04);
      border: 1px solid var(--border-subtle);
      color: var(--text-dim);
      font-size: 0.72rem;
      font-weight: 600;
      padding: 4px 10px;
      border-radius: 9999px;
      cursor: pointer;
      transition: all 0.2s;
    }

    .chip-btn:hover {
      background: rgba(255, 255, 255, 0.08);
      color: var(--text-main);
    }

    .active-chip {
      background: rgba(16, 185, 129, 0.15);
      border-color: #10b981;
      color: #34d399;
      font-weight: 700;
    }

    .doc-badge { font-size: 0.7rem; font-weight: 700; background: rgba(59, 130, 246, 0.15); color: #60a5fa; padding: 2px 6px; border-radius: var(--radius-sm); }
    .priority-pill { font-size: 0.65rem; padding: 1px 6px; border-radius: 9999px; background: var(--bg-card-subtle); color: var(--text-muted); font-weight: 700; }
    .priority-pill.urgent { background: rgba(244, 63, 94, 0.15); color: var(--vat-rose); }
    .task-status { font-size: 0.72rem; font-weight: 700; text-transform: uppercase; padding: 2px 8px; border-radius: 9999px; }
    .task-status.pending { background: rgba(245, 158, 11, 0.15); color: var(--vat-amber); }
    .task-status.approved { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .task-status.rejected { background: rgba(244, 63, 94, 0.15); color: #fb7185; }

    .maker-tag { font-size: 0.75rem; color: var(--text-muted); }
    .maker-tag.self-maker { color: var(--vat-amber); font-weight: 700; }

    .action-buttons { display: flex; gap: 6px; justify-content: flex-end; align-items: center; }

    .maker-block {
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .badge-maker {
      font-size: 0.68rem;
      background: rgba(245, 158, 11, 0.12);
      border: 1px solid rgba(245, 158, 11, 0.3);
      color: var(--vat-amber);
      padding: 3px 6px;
      border-radius: var(--radius-sm);
      font-weight: 700;
    }

    .btn-switch-checker {
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.35);
      color: #34d399;
      font-size: 0.7rem;
      font-weight: 700;
      padding: 3px 8px;
      border-radius: var(--radius-sm);
      cursor: pointer;
    }
    .btn-switch-checker:hover {
      background: rgba(16, 185, 129, 0.3);
    }

    .btn-approve { background: #059669; color: #ffffff; border: none; padding: 5px 12px; border-radius: var(--radius-sm); font-size: 0.75rem; font-weight: 700; cursor: pointer; }
    .btn-approve:hover { background: #10b981; }
    .btn-reject { background: rgba(244, 63, 94, 0.15); border: 1px solid rgba(244, 63, 94, 0.3); color: #fb7185; padding: 5px 12px; border-radius: var(--radius-sm); font-size: 0.75rem; font-weight: 700; cursor: pointer; }
    .btn-reject:hover { background: #e11d48; color: #ffffff; }

    .decided-label { font-size: 0.75rem; color: var(--text-dim); }
    .decided-label.approved { color: #34d399; font-weight: 700; }

    .empty-state { text-align: center; padding: 36px; color: var(--text-muted); }
    .badge-restricted {
      font-size: 0.68rem;
      font-weight: 700;
      color: var(--text-dim);
      background: rgba(255, 255, 255, 0.05);
      border: 1px dashed var(--border-card);
      padding: 3px 8px;
      border-radius: var(--radius-sm);
      display: inline-flex;
      align-items: center;
    }

    .modal-backdrop { position: fixed; inset: 0; background: rgba(0, 0, 0, 0.7); backdrop-filter: blur(4px); display: flex; align-items: center; justify-content: center; z-index: 100; }
    .modal-card { background: var(--bg-card); border: 1px solid var(--border-subtle); border-radius: var(--radius-lg); width: 480px; max-width: 90vw; padding: 24px; box-shadow: var(--shadow-lg); }
    .modal-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .btn-close { background: transparent; border: none; color: var(--text-muted); font-size: 1.2rem; cursor: pointer; }
    .modal-instruction { font-size: 0.85rem; margin-bottom: 10px; color: var(--text-muted); }
    .modal-amount-summary {
      display: flex;
      justify-content: space-between;
      align-items: center;
      background: var(--bg-card-subtle);
      padding: 8px 12px;
      border-radius: var(--radius-sm);
      margin-bottom: 16px;
      font-size: 0.85rem;
    }
    .vat-amt-pill { color: #34d399; font-weight: 700; }
    .textarea { resize: vertical; }
    .modal-footer { display: flex; justify-content: flex-end; gap: 10px; margin-top: 20px; }

    .spinner-small {
      display: inline-block;
      width: 12px;
      height: 12px;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-radius: 50%;
      border-top-color: #fff;
      animation: spin 0.8s linear infinite;
      margin-right: 6px;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class WorkflowComponent implements OnInit {
  private csvExport = inject(CsvExportService);
  private toast = inject(ToastService);
  public mockData = inject(MockDataService);

  public tasks: ApprovalTask[] = [];
  public selectedTab: 'Pending' | 'History' = 'Pending';
  public selectedBranchFilter: string = 'All';

  public showModal: boolean = false;
  public activeTask: ApprovalTask | null = null;
  public decisionAction: 'Approve' | 'Reject' = 'Approve';
  public decisionRemarks: string = '';
  public isSubmittingDecision: boolean = false;

  constructor(
    public context: CompanyContextService,
    private workflowApi: WorkflowApiService
  ) {}

  ngOnInit(): void {
    this.loadTasks();
  }

  loadTasks(): void {
    this.workflowApi.getMyTasks().subscribe({
      next: (data) => {
        this.tasks = data;
      },
      error: (err) => {
        console.error('Failed to load workflow tasks:', err);
      }
    });
  }

  get pendingCount(): number {
    return this.tasks.filter(t => t.status === 'Pending').length;
  }

  get approvedCount(): number {
    return this.tasks.filter(t => t.status !== 'Pending').length;
  }

  get displayedTasks(): ApprovalTask[] {
    let list = this.tasks;
    if (!this.context.isSuperAdmin()) {
      const curBranchId = this.context.activeBranch().id;
      const curBranchGuid = this.context.branchGuid();
      list = list.filter(t => 
        !t.branchId || 
        t.branchId === curBranchId || 
        t.branchId === curBranchGuid ||
        (curBranchId === 'BR-01' && t.branchId.includes('31')) ||
        (curBranchId === 'BR-02' && t.branchId.includes('32')) ||
        (curBranchId === 'BR-03' && t.branchId.includes('33'))
      );
    } else if (this.selectedBranchFilter !== 'All') {
      const targetGuid = this.context.resolveBranchGuid(this.selectedBranchFilter);
      list = list.filter(t => 
        t.branchId === this.selectedBranchFilter || 
        t.branchId === targetGuid ||
        (this.selectedBranchFilter === 'BR-01' && t.branchId?.includes('31')) ||
        (this.selectedBranchFilter === 'BR-02' && t.branchId?.includes('32')) ||
        (this.selectedBranchFilter === 'BR-03' && t.branchId?.includes('33'))
      );
    }
    if (this.selectedTab === 'Pending') {
      return list.filter(t => t.status === 'Pending');
    }
    return list.filter(t => t.status !== 'Pending');
  }

  getBranchName(branchId?: string): string {
    if (!branchId) return 'Dhaka HQ';
    if (branchId === 'BR-02' || branchId.includes('32')) return 'Chittagong Depot';
    if (branchId === 'BR-03' || branchId.includes('33')) return 'Bogura Warehouse';
    return 'Dhaka HQ';
  }

  canApprove(): boolean {
    return this.context.hasPermission('workflow.task.approve');
  }

  isMakerOfTask(task: ApprovalTask): boolean {
    const curUserId = this.context.currentUser().id;
    return task.requestedBy === curUserId;
  }

  switchToChecker(): void {
    this.context.switchUser('USR-8803');
    this.toast.info(
      this.context.locale() === 'bn' 
        ? 'চেকার পারসোনায় পরিবর্তিত হয়েছে: নুসরাত জাহান (Checker)' 
        : 'Switched persona to Checker: Nusrat Jahan',
      'পারসোনা পরিবর্তিত (RBAC Switch)'
    );
  }

  onUserSwitched(name: string): void {
    this.toast.info(
      `সক্রিয় পারসোনা পরিবর্তিত: ${name}`,
      'RBAC Persona Switched'
    );
  }

  openDecisionModal(task: ApprovalTask, action: 'Approve' | 'Reject'): void {
    if (!this.canApprove()) {
      this.toast.error(
        `অনুমোদনের অনুমতি নেই। বর্তমান ভূমিকা: ${this.context.userRole()} (Checker/Approver/TaxManager required)`,
        'অননুমোদিত অ্যাকশন (RBAC Restriction)'
      );
      return;
    }
    if (this.isMakerOfTask(task)) {
      this.toast.error(
        'মেকার নিজের তৈরি চালান অনুমোদন করতে পারবেন না। চেকার বা অ্যাপ্রুভার পারসোনায় পরিবর্তন করুন।',
        'সেগ্রিগেশন অব ডিউটিজ (Segregation of Duties)'
      );
      return;
    }
    this.activeTask = task;
    this.decisionAction = action;
    this.decisionRemarks = '';
    this.showModal = true;
  }

  submitDecision(): void {
    if (!this.activeTask) return;

    if (!this.canApprove()) {
      this.toast.error(
        `অনুমোদনের অনুমতি নেই। বর্তমান ভূমিকা: ${this.context.userRole()}`,
        'অননুমোদিত অ্যাকশন'
      );
      this.showModal = false;
      return;
    }

    this.isSubmittingDecision = true;
    const docNo = this.activeTask.documentNumber;
    const taskId = this.activeTask.documentId || this.activeTask.id;

    if (this.decisionAction === 'Approve') {
      this.workflowApi.approveTask(taskId, this.decisionRemarks).subscribe({
        next: () => {
          this.isSubmittingDecision = false;
          this.toast.success(
            this.context.locale() === 'bn'
              ? `চালান ${docNo} সফলভাবে ডাটাবেজে অনুমোদিত হয়েছে!`
              : `Task ${docNo} approved successfully in database!`,
            this.context.locale() === 'bn' ? 'অনুমোদন সম্পন্ন' : 'Approval Completed'
          );
          this.showModal = false;
          this.loadTasks();
        },
        error: (err) => {
          this.isSubmittingDecision = false;
          const msg = err?.error?.detail || err?.error?.message || 'Approval failed. Maker cannot approve their own purchase (Segregation of Duties).';
          this.toast.error(msg, 'অনুমোদন ব্যর্থ (Failed)');
        }
      });
    } else {
      this.workflowApi.rejectTask(taskId, this.decisionRemarks || 'Rejected by approver').subscribe({
        next: () => {
          this.isSubmittingDecision = false;
          this.toast.warning(
            this.context.locale() === 'bn'
              ? `চালান ${docNo} সফলভাবে বাতিল/প্রত্যাখ্যাত করা হয়েছে।`
              : `Task ${docNo} rejected in database.`,
            this.context.locale() === 'bn' ? 'প্রত্যাখ্যাত (Rejected)' : 'Rejected'
          );
          this.showModal = false;
          this.loadTasks();
        },
        error: (err) => {
          this.isSubmittingDecision = false;
          const msg = err?.error?.detail || err?.error?.message || 'Rejection failed.';
          this.toast.error(msg, 'অ্যাকশন ব্যর্থ (Failed)');
        }
      });
    }
  }

  exportToCsv(): void {
    const headers = [
      'Document Type',
      'Reference No',
      'Party / Description',
      'Requested By',
      'Requested At',
      'Amount (BDT)',
      'VAT Amount (BDT)',
      'Priority',
      'Status',
      'Notes'
    ];

    const rows = this.tasks.map(t => [
      t.documentType,
      t.documentNumber,
      t.partyName,
      t.requestedBy,
      t.requestedAt,
      t.amount,
      t.vatAmount,
      t.priority,
      t.status,
      t.notes || ''
    ]);

    const compId = this.context.activeCompany().id;
    const filename = `Workflow_Approval_Audit_${compId}_${new Date().toISOString().slice(0, 10)}`;
    this.csvExport.exportToCsv(filename, headers, rows);
  }
}

