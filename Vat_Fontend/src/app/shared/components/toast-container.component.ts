import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService, ToastItem } from '../../core/services/toast.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-container no-print" *ngIf="toastService.toasts().length > 0">
      <div 
        *ngFor="let t of toastService.toasts()" 
        class="toast-item" 
        [ngClass]="'toast-' + t.type"
      >
        <div class="toast-icon">
          <!-- Success -->
          <svg *ngIf="t.type === 'success'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
            <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
            <polyline points="22 4 12 14.01 9 11.01"></polyline>
          </svg>
          <!-- Error -->
          <svg *ngIf="t.type === 'error'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
            <circle cx="12" cy="12" r="10"></circle>
            <line x1="15" y1="9" x2="9" y2="15"></line>
            <line x1="9" y1="9" x2="15" y2="15"></line>
          </svg>
          <!-- Warning -->
          <svg *ngIf="t.type === 'warning'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
            <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"></path>
            <line x1="12" y1="9" x2="12" y2="13"></line>
            <line x1="12" y1="17" x2="12.01" y2="17"></line>
          </svg>
          <!-- Info -->
          <svg *ngIf="t.type === 'info'" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
            <circle cx="12" cy="12" r="10"></circle>
            <line x1="12" y1="16" x2="12" y2="12"></line>
            <line x1="12" y1="8" x2="12.01" y2="8"></line>
          </svg>
        </div>

        <div class="toast-content">
          <div class="toast-title" *ngIf="t.title">{{ t.title }}</div>
          <div class="toast-message">{{ t.message }}</div>
        </div>

        <button class="toast-close" (click)="toastService.remove(t.id)">&times;</button>
      </div>
    </div>
  `,
  styles: [`
    .toast-container {
      position: fixed;
      bottom: 24px;
      right: 24px;
      z-index: 9999;
      display: flex;
      flex-direction: column;
      gap: 10px;
      max-width: 420px;
      pointer-events: none;
    }

    .toast-item {
      pointer-events: auto;
      display: flex;
      align-items: flex-start;
      gap: 12px;
      padding: 14px 18px;
      border-radius: var(--radius-md);
      box-shadow: 0 10px 25px rgba(0, 0, 0, 0.4), 0 0 0 1px rgba(255, 255, 255, 0.08);
      backdrop-filter: blur(12px);
      background: rgba(26, 35, 50, 0.95);
      color: #ffffff;
      animation: slideIn 0.3s cubic-bezier(0.16, 1, 0.3, 1);
      transition: all 0.2s ease;
    }

    @keyframes slideIn {
      from {
        transform: translateY(20px) scale(0.95);
        opacity: 0;
      }
      to {
        transform: translateY(0) scale(1);
        opacity: 1;
      }
    }

    .toast-icon {
      margin-top: 1px;
      flex-shrink: 0;
    }

    .toast-content {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .toast-title {
      font-size: 0.85rem;
      font-weight: 700;
      letter-spacing: 0.02em;
    }

    .toast-message {
      font-size: 0.8rem;
      color: var(--text-muted);
      line-height: 1.4;
    }

    .toast-close {
      background: transparent;
      border: none;
      color: var(--text-dim);
      font-size: 1.2rem;
      line-height: 1;
      cursor: pointer;
      padding: 0 4px;
      margin-left: 6px;
    }
    .toast-close:hover {
      color: #ffffff;
    }

    /* Types */
    .toast-success {
      border-left: 4px solid var(--vat-emerald);
    }
    .toast-success .toast-icon {
      color: var(--vat-emerald);
    }

    .toast-error {
      border-left: 4px solid var(--vat-rose);
    }
    .toast-error .toast-icon {
      color: var(--vat-rose);
    }

    .toast-warning {
      border-left: 4px solid var(--vat-amber);
    }
    .toast-warning .toast-icon {
      color: var(--vat-amber);
    }

    .toast-info {
      border-left: 4px solid var(--vat-teal);
    }
    .toast-info .toast-icon {
      color: var(--vat-teal);
    }
  `]
})
export class ToastContainerComponent {
  constructor(public toastService: ToastService) {}
}
