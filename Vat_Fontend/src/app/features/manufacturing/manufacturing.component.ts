import { Component, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { CompanyContextService } from '../../core/services/company-context.service';
import { CoefficientResult } from '../../core/services/api/manufacturing-api.service';
import { CoefficientDeclarationComponent } from './components/coefficient-declaration/coefficient-declaration.component';
import { BomListComponent } from './components/bom-list/bom-list.component';
import { NewDeclarationComponent } from './components/new-declaration/new-declaration.component';

@Component({
  selector: 'app-manufacturing',
  standalone: true,
  imports: [
    CommonModule, 
    RouterModule, 
    CoefficientDeclarationComponent, 
    BomListComponent, 
    NewDeclarationComponent
  ],
  template: `
    <div class="manufacturing-page">
      <div class="page-header no-print">
        <div>
          <div class="breadcrumb-trail">
            <span>Manufacturing & 4.3</span> / <span>{{ getTabTitle() }}</span>
          </div>
          <h1 class="page-title">
            {{ context.locale() === 'bn' ? 'মূসক-৪.৩: উপকরণ-উৎপাদ সহগ ও বিওএম কেন্দ্র' : 'Mushak 4.3: Input-Output Coefficient & BOM Hub' }}
          </h1>
          <p class="page-subtitle">
            [বিধি ২১ দ্রষ্টব্য] — পণ্য উৎপাদনের কাঁচামাল অনুপাত, অপচয় (Wastage %) ও মূল্য সংযোজন ঘোষণা (PostgreSQL Live)
          </p>
        </div>

        <div class="header-actions">
          <a routerLink="/app/manufacturing/execution">Production orders and actual WIP costing</a>
          <div class="tab-pill-box">
            <button class="tab-btn" [class.active-tab]="activeTab === 'declaration'" (click)="activeTab = 'declaration'">
              মূসক ৪.৩ ফরম
            </button>
            <button class="tab-btn" [class.active-tab]="activeTab === 'bom'" (click)="activeTab = 'bom'">
              BOM তালিকা
            </button>
            <button class="tab-btn highlight-tab" [class.active-tab]="activeTab === 'newDeclaration'" (click)="activeTab = 'newDeclaration'">
              + New 4.3 BOM
            </button>
          </div>
        </div>
      </div>

      <!-- Success Notification Banner -->
      <div *ngIf="successMsg" class="alert-banner alert-success no-print">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
          <polyline points="22 4 12 14.01 9 11.01"></polyline>
        </svg>
        <span>{{ successMsg }}</span>
        <button class="alert-close" (click)="successMsg = ''">&times;</button>
      </div>

      <!-- TAB 1: MUSHAK 4.3 STATUTORY DECLARATION FORM -->
      <app-coefficient-declaration 
        #declarationComp
        *ngIf="activeTab === 'declaration'"
      ></app-coefficient-declaration>

      <!-- TAB 2: BILL OF MATERIALS (BOM) REGISTRY -->
      <app-bom-list 
        *ngIf="activeTab === 'bom'"
        (declarationSelected)="onDeclarationSelected($event)"
      ></app-bom-list>

      <!-- TAB 3: NEW 4.3 DECLARATION ENTRY FORM -->
      <app-new-declaration 
        *ngIf="activeTab === 'newDeclaration'"
        (declarationCreated)="onDeclarationCreated($event)"
        (cancelled)="activeTab = 'declaration'"
      ></app-new-declaration>
    </div>
  `,
  styles: [`
    .manufacturing-page { display: flex; flex-direction: column; gap: 20px; }
    .page-header { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 16px; }
    .page-title { font-size: 1.5rem; font-weight: 800; }
    .page-subtitle { font-size: 0.85rem; color: var(--text-muted); }
    .breadcrumb-trail { font-size: 0.8rem; color: var(--text-dim); margin-bottom: 4px; }
    .header-actions { display: flex; align-items: center; gap: 12px; }

    .tab-pill-box { display: flex; gap: 4px; background: var(--bg-card); padding: 4px; border-radius: var(--radius-md); border: 1px solid var(--border-card); }
    .tab-btn { display: inline-flex; align-items: center; gap: 6px; background: transparent; border: none; color: var(--text-muted); padding: 7px 14px; border-radius: var(--radius-sm); font-size: 0.78rem; font-weight: 700; cursor: pointer; transition: all 0.2s ease; }
    .active-tab { background: var(--vat-emerald) !important; color: #ffffff !important; }
    .highlight-tab { color: var(--vat-teal); }

    .alert-banner { display: flex; align-items: center; gap: 10px; padding: 12px 18px; border-radius: var(--radius-md); font-size: 0.9rem; }
    .alert-success { background: rgba(16, 185, 129, 0.15); border: 1px solid #10b981; color: #34d399; }
    .alert-close { margin-left: auto; background: transparent; border: none; font-size: 1.2rem; color: inherit; cursor: pointer; }
  `]
})
export class ManufacturingComponent implements OnInit {
  @ViewChild('declarationComp') declarationComp?: CoefficientDeclarationComponent;

  public activeTab: 'declaration' | 'bom' | 'newDeclaration' = 'declaration';
  public successMsg = '';

  private router = inject(Router);
  public context = inject(CompanyContextService);

  ngOnInit(): void {
    const url = this.router.url;
    if (url.includes('bom')) {
      this.activeTab = 'bom';
    } else {
      this.activeTab = 'declaration';
    }
  }

  public getTabTitle(): string {
    switch (this.activeTab) {
      case 'declaration': return 'Mushak 4.3 Declaration Form';
      case 'bom': return 'Bill of Materials (BOM) Registry';
      case 'newDeclaration': return 'New 4.3 Declaration Entry';
    }
  }

  public onDeclarationSelected(dec: CoefficientResult): void {
    this.activeTab = 'declaration';
    setTimeout(() => {
      if (this.declarationComp) {
        this.declarationComp.setDeclaration(dec);
      }
    });
  }

  public onDeclarationCreated(declarationNumber: string): void {
    this.successMsg = `Coefficient draft '${declarationNumber}' saved. Submit and approve it separately in the register.`;
    this.activeTab = 'declaration';
  }
}
