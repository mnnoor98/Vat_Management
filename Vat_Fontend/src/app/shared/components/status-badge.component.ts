import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DocumentStatus } from '../../core/models/vat-models';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span class="status-badge" [ngClass]="statusClass">
      <span class="dot"></span>
      {{ status }}
    </span>
  `,
  styles: [`
    .status-badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 3px 10px;
      border-radius: 9999px;
      font-size: 0.72rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }
    .dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: currentColor;
    }
  `]
})
export class StatusBadgeComponent {
  @Input({ required: true }) status: DocumentStatus | string = 'Draft';

  get statusClass(): string {
    const s = this.status.toLowerCase();
    if (s.includes('draft')) return 'draft';
    if (s.includes('submit')) return 'submitted';
    if (s.includes('verif')) return 'verified';
    if (s.includes('approv') || s.includes('post')) return 'approved';
    if (s.includes('reject') || s.includes('cancel')) return 'rejected';
    return 'warning';
  }
}
